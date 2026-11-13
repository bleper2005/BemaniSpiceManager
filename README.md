# Bemani Spice Manager

A plain Windows app for setting up and running BEMANI arcade data with
[spice2x](https://spice2x.github.io/) and a local e-amusement server
([Asphyxia CORE](https://github.com/asphyxia-core/core) or
[bemaniutils](https://github.com/DragonMinded/bemaniutils)).

Everything works with the mouse: drop an archive on the window, pick a server, and press **Launch**.

> This project contains no game data and never will. You need your own game files.

## What it does

- **Drag and drop import.** Drop a `.rar`, `.7z` or `.zip` (multi-part sets such as
  `.part1.rar` and `.7z.001` work too) on the window. The app:
  1. reads the archive's unpacked size and checks the library drive has enough free space
     (plus 2 GB spare); if not, nothing is extracted;
  2. extracts everything into your library folder (with 7-Zip if it is installed, which is
     much faster on 30-50 GB archives, otherwise with the built-in extractor);
  3. detects the game (game DLL in `modules\`, then model/datecode from `prop\ea3-config.xml`,
     the folder name, or the spice2x patch file);
  4. adds it to the game list, and only then tells you the archive is safe to delete
     (with an optional "move to Recycle Bin" button). If extraction fails or is cancelled,
     the partial files are removed and you're told to keep the archive.
- **Per-game settings.** Server, PCBID and card number (with "New random" buttons and
  "same card as another game"), plus every spice2x option, grouped by spice2x's own
  categories with checkboxes and drop-downs. The option list is generated from spice2x's
  `launcher/options.cpp`. 64-bit games run with `spice64.exe` and 32-bit games with `spice.exe`,
  chosen automatically from the game DLL.
- **Launch.** Starts the game's server if it isn't running, then spice2x from the game folder
  with `-url`, `-p` and `-card0`. You can copy the command line or save it as a `.bat`.
- **Shortcuts.** Each game page can add a desktop or Start menu shortcut that starts the
  server and the game in one click (`BemaniSpiceManager.exe --launch <game id>`, with no window).
  The Settings tab adds shortcuts for the app itself.
- **spice2x.** Point it at your spice2x folder or download the latest release from GitHub.
  "Button / key bindings" opens `spicecfg.exe` for the selected game.
- **Asphyxia CORE.** Start/stop, download or update (your `config.ini`, plugins and savedata
  are kept), edit `config.ini` with checkboxes, and edit each plugin's settings (such as the
  `[popn@asphyxia]` section), read straight from the plugin's own `R.Config` definitions.
  "Add official plugins" installs missing plugins and never overwrites one you have.
- **bemaniutils.** Pick the checkout folder and Python, start/stop the services, create the
  database tables, and edit `config/server.yaml` (comments are kept). "Set up for this PC"
  points the advertised address at `127.0.0.1`.

A note on servers: spice2x's own `-ea` option only boots the game. It has no card login and
saves nothing, so the app never adds `-ea` when a real server is chosen.

## Supported games

Detection follows spice2x's own rules (`launcher.cpp`): pop'n music (with version names by
datecode), HELLO! Pop'n Music, beatmania IIDX, SOUND VOLTEX, DanceDanceRevolution, jubeat,
GITADORA, REFLEC BEAT, MÚSECA, NOSTALGIA, DANCERUSH STARDOM, BeatStream, DanceEvolution and
Polaris Chord. More are easy to add (see below).

## Getting it

Build it yourself (needs the [.NET 10 SDK](https://dotnet.microsoft.com/download)):

```powershell
powershell -ExecutionPolicy Bypass -File tools\publish.ps1
```

This writes a single self-contained `dist\BemaniSpiceManager.exe`.

Settings and the game list are stored in `%LocalAppData%\BemaniSpiceManager`. To run portably,
put an empty `portable.txt` next to the exe; data then goes in a `data` folder beside it.

## Adding features

The code is split into small modules that are discovered automatically. There's nothing to
register.

| To add…        | Do this |
|----------------|---------|
| a game         | Add a class deriving from `GameModule` in `src/BemaniSpiceManager/Modules/Games/` (one file per game). Set its DLL names, the spice2x game name and model codes. Override `Match` for special checks and `DisplayName` for version names. |
| a server       | Add a class deriving from `ServerModule` in `Modules/Servers/`. It gets a page in the Servers tab and appears in every game's server list. |
| a tool         | Add a static helper in `Modules/Tools/` (see `Spice2x.cs`, `ArchiveExtractor.cs`, `GameDetector.cs`). |
| a new spice2x  | Run `python tools/gen_spice_options.py <spice2x>/src/spice2x/launcher/options.cpp` to refresh `Data/spice-options.json`. |

Layout:

```
src/BemaniSpiceManager/
  Core/        state (JSON), importer, launcher, INI/YAML editors, helpers
  Modules/     Games/ (one per game), Servers/ (Asphyxia, bemaniutils), Tools/
  Pages/       game page, spice2x options window, spice2x and settings tabs
  Ui/          small helpers for building plain settings pages in code
tests/         xUnit tests (import, detection, configs, Asphyxia start/stop, archive→launch)
tools/         option-list generator, publish script
```

Run the tests with `dotnet test`. They never start a game: the final spice2x launch is captured.
The Asphyxia tests run only if Asphyxia is found at `C:\PopnArchive\asphyxia`, and they start a
private copy on another port.

## License

GPL-3.0, the same as spice2x. `Data/spice-options.json` contains option names and descriptions
from spice2x.
