using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules;
using BemaniSpiceManager.Modules.Tools;
using static BemaniSpiceManager.Ui.Ui;

namespace BemaniSpiceManager.Pages;

/// <summary>Details, server, card/PCBID, spice2x options and launch for one game.</summary>
public sealed class GamePage : ScrollViewer
{
    public sealed record Callbacks(Action Save, Action<string> Status, Action Remove);

    readonly GameEntry _g;
    readonly AppState _state;
    readonly Callbacks _cb;
    readonly StackPanel _root = new() { Margin = new Thickness(16, 8, 16, 16) };
    TextBox? _cmd;
    TextBlock? _serverNote;

    public GamePage(GameEntry g, AppState state, Callbacks cb)
    {
        _g = g; _state = state; _cb = cb;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Content = _root;
        Build();
    }

    void Changed()
    {
        _cb.Save();
        if (_cmd != null) _cmd.Text = GameLauncher.CommandLine(_g, _state);
        if (_serverNote != null)
        {
            var note = ServerNote();
            _serverNote.Text = note ?? "";
            _serverNote.Visibility = note == null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    string? ServerNote() => _g.ServerId switch
    {
        "none" => "spice2x's built-in server only boots the game: no card login and nothing is saved.",
        "custom" => null,
        _ => ModuleRegistry.Server(_g.ServerId)?.NoteFor(_g),
    };

    void Build()
    {
        _root.Children.Clear();
        var module = ModuleRegistry.Game(_g.ModuleId);

        // title + launch
        var launch = new Button
        {
            Content = "▶  Launch", FontSize = 16, Padding = new Thickness(22, 6, 22, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), Foreground = Brushes.White,
        };
        launch.Click += async (_, _) =>
        {
            launch.IsEnabled = false;
            try
            {
                var err = await GameLauncher.LaunchAsync(_g, _state, _cb.Status);
                if (err != null) Error(err); else _cb.Status($"{_g.Name} started.");
            }
            catch (Exception ex) { Error("Couldn't launch: " + ex.Message); }
            finally { launch.IsEnabled = true; }
        };
        var missing = !Directory.Exists(_g.RootPath);
        if (missing) { launch.IsEnabled = false; ToolTipService.SetShowOnDisabled(launch, true); launch.ToolTip = "The game folder is missing."; }
        var title = new TextBlock { Text = _g.Name, FontSize = 22, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        _root.Children.Add(Row(title, launch));

        if (missing)
        {
            var box = new StackPanel();
            box.Children.Add(new TextBlock { Text = "The game folder is missing", FontWeight = FontWeights.SemiBold, Foreground = Brushes.Firebrick });
            box.Children.Add(Note($"{_g.RootPath} doesn't exist any more (deleted, moved, or on a drive that isn't connected). " +
                                  "Your settings, card and PCBID are kept. Point this entry at the game's new folder, or drop the archive on the window again."));
            box.Children.Add(Row(Button("Locate folder…", LocateFolder), Button("Remove from list", () => { if (Confirm($"Remove {_g.Name} from the list?")) _cb.Remove(); })));
            _root.Children.Add(new Border
            {
                Child = box, Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0xF0)), BorderBrush = Brushes.Firebrick, BorderThickness = new Thickness(1),
            });
        }

        var info = $"{module?.Name ?? _g.ModuleId}   •   model {Empty(_g.Model)}   •   datecode {Empty(_g.DateCode)}   •   {(_g.Is64Bit ? "64-bit (spice64.exe)" : "32-bit (spice.exe)")}   •   {_g.GameDll}";
        _root.Children.Add(Note(info));

        _root.Children.Add(Header("Game"));
        _root.Children.Add(Setting("Name in list", Text(_g.Name, v => { _g.Name = v; Changed(); }, 320)));
        var folderRow = Row(new TextBlock { Text = _g.RootPath, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        if (!missing)
        {
            folderRow.Children.Add(Button("Open", () => Shell.Open(_g.RootPath)));
            folderRow.Children.Add(Button("Detect again", Redetect, "Re-read model and datecode from the folder"));
        }
        folderRow.Children.Add(Button("Change folder…", LocateFolder, "Use a different folder for this game (keeps card, PCBID and options)"));
        _root.Children.Add(Setting("Folder", folderRow));

        // server
        _root.Children.Add(Header("Network"));
        var servers = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        var choices = ModuleRegistry.Servers.Select(s => (id: s.Id, label: s.Name + (s.IsInstalled ? "" : "  (not set up)"))).ToList();
        choices.Add(("custom", "Other server (enter URL)"));
        choices.Add(("none", "spice2x built-in (boots only, saves nothing)"));
        foreach (var c in choices) servers.Items.Add(new ComboBoxItem { Content = c.label, Tag = c.id });
        servers.SelectedIndex = Math.Max(0, choices.FindIndex(c => c.id == _g.ServerId));
        var custom = Text(_g.CustomUrl, v => { _g.CustomUrl = v; Changed(); }, 320);
        var customRow = Setting("Server URL", custom, "e.g. http://192.168.1.10:8083");
        customRow.Visibility = _g.ServerId == "custom" ? Visibility.Visible : Visibility.Collapsed;
        servers.SelectionChanged += (_, _) =>
        {
            _g.ServerId = (string)((ComboBoxItem)servers.SelectedItem).Tag;
            customRow.Visibility = _g.ServerId == "custom" ? Visibility.Visible : Visibility.Collapsed;
            Changed();
        };
        _root.Children.Add(Setting("Server", servers));
        _root.Children.Add(customRow);
        _serverNote = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkOrange, Margin = new Thickness(220, 0, 0, 4) };
        _root.Children.Add(_serverNote);

        var pcbid = Text(_g.Pcbid, v => { _g.Pcbid = v.Trim().ToUpperInvariant(); Changed(); }, 220);
        _root.Children.Add(Setting("PCBID", Row(pcbid, Button("New random", () => pcbid.Text = Ids.NewPcbid())),
            "20 hex digits identifying this machine to the server."));
        var card = Text(_g.Card, v => { _g.Card = v.Trim().ToUpperInvariant(); Changed(); }, 220);
        var others = _state.Games.Where(x => x != _g && !string.IsNullOrEmpty(x.Card)).Select(x => x.Card).Distinct().ToList();
        var cardRow = Row(card, Button("New random", () => card.Text = Ids.NewCard()));
        if (others.Count > 0)
        {
            var pick = new ComboBox { Width = 170, ToolTip = "Use a card from another game, so all your games share one profile" };
            pick.Items.Add(new ComboBoxItem { Content = "Same as another game…", IsEnabled = false });
            foreach (var o in others) pick.Items.Add(o);
            pick.SelectedIndex = 0;
            pick.SelectionChanged += (_, _) => { if (pick.SelectedItem is string s) card.Text = s; };
            cardRow.Children.Add(pick);
        }
        _root.Children.Add(Setting("Card (player 1)", cardRow, "16 hex digits starting with E004. The server creates a profile for it on first use."));

        // spice options
        _root.Children.Add(Header("spice2x options"));
        var set = _g.SpiceOptions.Keys.ToList();
        var known = Spice2x.Options.ToDictionary(o => o.Name);
        _root.Children.Add(Note(set.Count == 0 ? "No options changed; spice2x uses its defaults (and whatever you set in spicecfg)."
            : "Changed: " + string.Join(", ", set.Select(k => known.TryGetValue(k, out var o) ? o.Title : k))));
        _root.Children.Add(Row(
            Button("Edit spice2x options…", () =>
            {
                new SpiceOptionsWindow(_g, module?.SpiceGameName) { Owner = Window.GetWindow(this) }.ShowDialog();
                _cb.Save();
                Build();
            }),
            Button("Button / key bindings (spicecfg)", OpenSpiceCfg, "Opens spice2x's own configurator for controls"),
            Button("Windowed", () => { _g.SpiceOptions["w"] = "1"; Changed(); Build(); }, "Quick toggle: run in a window"),
            Button("Fullscreen", () => { _g.SpiceOptions.Remove("w"); Changed(); Build(); })));

        if (module?.Presets.Count > 0)
        {
            _root.Children.Add(Header("Quick setup"));
            foreach (var p in module.Presets)
            {
                var active = p.Set.All(kv => _g.SpiceOptions.TryGetValue(kv.Key, out var v) && v == kv.Value)
                             && (p.Clear ?? Array.Empty<string>()).All(k => !_g.SpiceOptions.ContainsKey(k));
                _root.Children.Add(Setting(p.Name, Row(
                    Button(active ? "✓ In use" : "Use this", () =>
                    {
                        foreach (var k in p.Clear ?? Array.Empty<string>()) _g.SpiceOptions.Remove(k);
                        foreach (var (k, v) in p.Set) _g.SpiceOptions[k] = v;
                        Changed(); Build();
                    })), p.Description));
            }
        }

        _root.Children.Add(Header("Command line"));
        _cmd = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(4), Background = Brushes.WhiteSmoke };
        _root.Children.Add(_cmd);
        _root.Children.Add(Row(Button("Copy", () => Clipboard.SetText(_cmd.Text)),
            Button("Save as .bat in game folder", SaveBat, "Writes a launcher you can run without this app")));

        _root.Children.Add(Header("Shortcuts"));
        _root.Children.Add(Note("A shortcut starts the server and the game in one click, without opening this window."));
        _root.Children.Add(Row(
            Button("Add desktop shortcut", () => MakeShortcut(Shortcuts.Desktop)),
            Button("Add Start menu shortcut", () => MakeShortcut(Shortcuts.StartMenu))));

        _root.Children.Add(Header("Remove"));
        _root.Children.Add(Row(
            Button("Remove from list", () => { if (Confirm($"Remove {_g.Name} from the list? Its files stay on disk.")) _cb.Remove(); }),
            Button("Delete game files…", DeleteFiles, "Moves the game folder to the Recycle Bin")));
        Changed();
    }

    static string Empty(string s) => string.IsNullOrEmpty(s) ? "?" : s;

    void Redetect()
    {
        var d = GameDetector.TryDetect(_g.RootPath);
        if (d == null) { Error("No game was recognised in the folder any more."); return; }
        Apply(d);
    }

    void Apply(GameEntry d)
    {
        _g.RootPath = d.RootPath;
        _g.ModuleId = d.ModuleId; _g.Model = d.Model; _g.DateCode = d.DateCode; _g.GameDll = d.GameDll;
        _g.Is64Bit = d.Is64Bit; _g.Dest = d.Dest; _g.Spec = d.Spec; _g.Rev = d.Rev;
        _cb.Save(); Build();
    }

    /// <summary>Re-point this entry at another folder, keeping card, PCBID, server and options.</summary>
    void LocateFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = $"Find the folder for {_g.Name}" };
        var parent = Path.GetDirectoryName(_g.RootPath);
        if (parent != null && Directory.Exists(parent)) dlg.InitialDirectory = parent;
        if (dlg.ShowDialog() != true) return;
        var found = GameDetector.Detect(dlg.FolderName);
        if (found.Count == 0) { Error("No spice2x game was found in that folder (looked for a modules folder with a game DLL)."); return; }
        var pick = found.FirstOrDefault(f => f.ModuleId == _g.ModuleId) ?? found[0];
        if (pick.ModuleId != _g.ModuleId && !Confirm($"That folder holds {pick.Name}, not {_g.Name}. Use it anyway?")) return;
        Apply(pick);
        _cb.Status($"{_g.Name} now uses {pick.RootPath}.");
    }

    void OpenSpiceCfg()
    {
        // like spice64.exe, spicecfg finds the game relative to its own location, so run the game folder's copy
        var err = GameLauncher.SyncSpice(_g, _state);
        if (err != null) { Error(err); return; }
        var cfg = Path.Combine(_g.RootPath, "spicecfg.exe");
        if (!File.Exists(cfg)) { Error("spicecfg.exe wasn't found. Set up spice2x in the spice2x tab."); return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cfg) { WorkingDirectory = _g.RootPath, UseShellExecute = false });
    }

    void MakeShortcut(string folder)
    {
        try { _cb.Status("Created " + Shortcuts.CreateForGame(_g, folder, Spice2x.Exe(_state.SpiceFolder, _g.Is64Bit))); }
        catch (Exception ex) { Error("Couldn't create the shortcut: " + ex.Message); }
    }

    void SaveBat()
    {
        var bat = Path.Combine(_g.RootPath, "launch-bsm.bat");
        var server = ModuleRegistry.Server(_g.ServerId);
        var lines = new List<string> { "@echo off", $"rem Written by Bemani Spice Manager for {_g.Name}", "cd /d \"%~dp0\"" };
        if (server != null) lines.Add($"rem Start {server.Name} first ({server.ServiceUrl}).");
        lines.Add(GameLauncher.CommandLine(_g, _state));
        try { File.WriteAllLines(bat, lines); _cb.Status("Saved " + bat); }
        catch (Exception ex) { Error("Couldn't save the .bat: " + ex.Message); }
    }

    void DeleteFiles()
    {
        if (!Directory.Exists(_g.RootPath)) { Error("The game folder is already gone."); return; }
        if (!Confirm($"Move this folder to the Recycle Bin?\n\n{_g.RootPath}\n\nThe game is also removed from the list.")) return;
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(_g.RootPath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            _cb.Remove();
        }
        catch (Exception ex) { Error("Couldn't delete: " + ex.Message); }
    }
}
