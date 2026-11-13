using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules;

namespace BemaniSpiceManager.Tests;

/// <summary>
/// Archive → extract → detect → edit options → launch through Asphyxia, with the final
/// spice2x process captured instead of started (so no game window or audio).
/// </summary>
public class EndToEndTests
{
    [Fact]
    public async Task MissingFolder_RefusesToLaunch()
    {
        var state = new AppState { AsphyxiaFolder = @"C:\nowhere-asphyxia", SpiceFolder = @"C:\nowhere-spice" };
        foreach (var s in ModuleRegistry.Servers) s.Attach(state);
        var g = new GameEntry { Name = "gone", RootPath = @"C:\definitely\not\here\M39-2026041500", ServerId = "asphyxia" };
        Assert.False(g.FolderExists);
        Assert.Contains("folder missing", g.Subtitle);
        var started = false;
        var old = GameLauncher.StartProcess;
        GameLauncher.StartProcess = _ => { started = true; return null; };
        try
        {
            var err = await GameLauncher.LaunchAsync(g, state, _ => { });
            Assert.NotNull(err);
            Assert.Contains("missing", err);
        }
        finally { GameLauncher.StartProcess = old; }
        Assert.False(started);
    }

    [Fact]
    public async Task ArchiveToLaunch()
    {
        const string realAsphyxia = @"C:\PopnArchive\asphyxia\asphyxia-core-x64.exe";
        if (!File.Exists(realAsphyxia)) return;
        using var t = new TempDir();

        // archive with a fake game inside
        var root = t.MakeFakePopn();
        var archive = Path.Combine(t.Path, "M39-2026041500.zip");
        ZipFile.CreateFromDirectory(Path.GetDirectoryName(root)!, archive);

        // private Asphyxia copy on port 8094 and a fake spice2x folder
        var asph = Path.Combine(t.Path, "asphyxia");
        Directory.CreateDirectory(asph);
        File.Copy(realAsphyxia, Path.Combine(asph, "asphyxia-core-x64.exe"));
        File.WriteAllText(Path.Combine(asph, "config.ini"), "port=8094\nbind=localhost\nwebui_on_startup=false\n");
        var spice = Path.Combine(t.Path, "spice2x");
        Directory.CreateDirectory(spice);
        File.WriteAllText(Path.Combine(spice, "spice64.exe"), "not a real exe");

        var state = new AppState { LibraryPath = Path.Combine(t.Path, "lib"), AsphyxiaFolder = asph, SpiceFolder = spice };
        foreach (var s in ModuleRegistry.Servers) s.Attach(state);

        var result = await Importer.ImportAsync(archive, state, new Progress<(double, string)>(_ => { }), CancellationToken.None);
        var game = Assert.Single(result.Games);
        game.ServerId = "asphyxia";
        game.Pcbid = "012010000004EF09FC10";
        game.Card = "E00467D170D97EAF";
        game.SpiceOptions["w"] = "1";

        ProcessStartInfo? captured = null;
        var old = GameLauncher.StartProcess;
        GameLauncher.StartProcess = psi => { captured = psi; return null; };
        var server = ModuleRegistry.Server("asphyxia")!;
        try
        {
            var err = await GameLauncher.LaunchAsync(game, state, _ => { });
            Assert.True(err == null, err);
            Assert.True(server.IsRunning); // launching started the server first
        }
        finally
        {
            GameLauncher.StartProcess = old;
            server.Stop();
        }

        Assert.NotNull(captured);
        // spice2x is copied into the game folder and run from there (it finds modules\ next to itself)
        Assert.Equal(Path.Combine(game.RootPath, "spice64.exe"), captured!.FileName);
        Assert.Equal(File.ReadAllText(Path.Combine(spice, "spice64.exe")), File.ReadAllText(captured.FileName));
        Assert.Equal(game.RootPath, captured.WorkingDirectory);
        Assert.Equal(new[] { "-url", "http://localhost:8094", "-p", "012010000004EF09FC10", "-card0", "E00467D170D97EAF", "-w" },
            captured.ArgumentList.ToArray());
    }
}
