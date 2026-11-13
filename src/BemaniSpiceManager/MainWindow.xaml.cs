using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules;
using BemaniSpiceManager.Modules.Tools;
using BemaniSpiceManager.Pages;
using static BemaniSpiceManager.Ui.Ui;

namespace BemaniSpiceManager;

public partial class MainWindow : Window
{
    readonly AppState _state = AppState.Load();
    readonly ObservableCollection<GameEntry> _games;
    CancellationTokenSource? _importCts;

    public MainWindow()
    {
        InitializeComponent();
        if (string.IsNullOrEmpty(_state.LibraryPath))
            _state.LibraryPath = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\", "BemaniLibrary");
        foreach (var s in ModuleRegistry.Servers) s.Attach(_state);

        _games = new ObservableCollection<GameEntry>(_state.Games);
        GameList.ItemsSource = _games;
        ServerList.ItemsSource = ModuleRegistry.Servers;
        if (ModuleRegistry.Servers.Count > 0) ServerList.SelectedIndex = 0;
        SpicePageHost.Content = SpicePage.Create(_state, Save);
        SettingsPageHost.Content = SettingsPage.Create(_state, Save);
        if (_games.Count > 0) GameList.SelectedIndex = 0;
        Title = $"Bemani Spice Manager {typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}";
        SetStatus($"Ready. {_games.Count} game(s) loaded from {AppState.DataFolder}  ({Environment.ProcessPath})");
        WriteDiagnostics();
        Closing += OnClosing;
    }

    /// <summary>Writes startup.log in the data folder, to debug "my games are missing".</summary>
    void WriteDiagnostics()
    {
        var file = Path.Combine(AppState.DataFolder, "state.json");
        string read;
        try { read = File.Exists(file) ? $"exists, {new FileInfo(file).Length} bytes, readable={File.ReadAllText(file).Length > 0}" : "missing"; }
        catch (Exception ex) { read = "error: " + ex.Message; }
        var text = string.Join(Environment.NewLine,
            $"time={DateTime.Now:o}", $"machine={Environment.MachineName}", $"user={Environment.UserName}",
            $"pid={Environment.ProcessId}", $"session={System.Diagnostics.Process.GetCurrentProcess().SessionId}",
            $"exe={Environment.ProcessPath}", $"data={AppState.DataFolder}", $"state.json={read}",
            $"games={_games.Count}", $"loadError={AppState.LastLoadError}",
            $"desktop={Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}");
        try { File.WriteAllText(Path.Combine(AppState.DataFolder, "startup.log"), text); } catch { }
    }

    void Save()
    {
        _state.Games = _games.ToList();
        try { _state.Save(); } catch (Exception ex) { SetStatus("Couldn't save settings: " + ex.Message); }
    }

    void SetStatus(string text) => StatusText.Text = text;

    // ---- games list ----------------------------------------------------------------------

    void OnGameSelected(object sender, SelectionChangedEventArgs e) => ShowGame(GameList.SelectedItem as GameEntry);

    void ShowGame(GameEntry? g)
    {
        EmptyHint.Visibility = g == null ? Visibility.Visible : Visibility.Collapsed;
        GamePageHost.Content = g == null ? null : new GamePage(g, _state, new GamePage.Callbacks(
            Save: () => { Save(); GameList.Items.Refresh(); },
            Status: SetStatus,
            Remove: () =>
            {
                _games.Remove(g); Save();
                GameList.SelectedIndex = _games.Count > 0 ? 0 : -1;
            }));
    }

    void AddGames(IEnumerable<GameEntry> games)
    {
        GameEntry? last = null;
        foreach (var g in games)
        {
            var existing = _games.FirstOrDefault(x => string.Equals(x.RootPath, g.RootPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { last = existing; continue; }
            g.Pcbid = Ids.NewPcbid();
            g.Card = _games.FirstOrDefault(x => !string.IsNullOrEmpty(x.Card))?.Card ?? Ids.NewCard();
            g.ServerId = ModuleRegistry.Server("asphyxia") != null ? "asphyxia" : "none";
            _games.Add(g);
            last = g;
        }
        Save();
        if (last != null) { Tabs.SelectedIndex = 0; GameList.SelectedItem = last; }
    }

    // ---- servers -------------------------------------------------------------------------

    void OnServerSelected(object sender, SelectionChangedEventArgs e) =>
        ServerPageHost.Content = (ServerList.SelectedItem as ServerModule)?.CreateView(Save);

    // ---- importing -----------------------------------------------------------------------

    void OnChooseArchive(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog
        {
            Title = "Choose a game archive",
            Filter = "Archives|*.rar;*.7z;*.zip;*.001|All files|*.*",
        };
        if (d.ShowDialog() == true) _ = ImportArchive(d.FileName);
    }

    void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var d = new OpenFolderDialog { Title = "Choose an extracted game folder" };
        if (d.ShowDialog() != true) return;
        var found = GameDetector.Detect(d.FolderName);
        if (found.Count == 0) { Error("No spice2x game was found in that folder (looked for a modules folder with a game DLL)."); return; }
        AddGames(found);
        SetStatus($"Added {string.Join(", ", found.Select(f => f.Name))}.");
    }

    async Task ImportArchive(string path)
    {
        if (_importCts != null) { Error("An archive is already being extracted. Wait for it to finish or cancel it first."); return; }
        _importCts = new CancellationTokenSource();
        Progress.Visibility = CancelButton.Visibility = Visibility.Visible;
        Progress.Value = 0;
        try
        {
            var result = await Importer.ImportAsync(path, _state,
                new Progress<(double p, string t)>(x => { Progress.Value = x.p; SetStatus(x.t); }), _importCts.Token);

            var files = string.Join("\n", result.ArchiveFiles.Select(f => "  " + Path.GetFileName(f)));
            if (result.Games.Count == 0)
            {
                SetStatus("Extracted, but no game was recognised.");
                MessageBox.Show(this,
                    $"Extraction finished into:\n{result.Folder}\n\nNo spice2x game was recognised in it, so nothing was added. " +
                    "Keep the archive until you've checked the folder.",
                    "Bemani Spice Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AddGames(result.Games);
            SetStatus($"Added {string.Join(", ", result.Games.Select(g => g.Name))}. Extraction finished; the archive is no longer needed.");
            var answer = MessageBox.Show(this,
                $"Extraction finished and {string.Join(", ", result.Games.Select(g => g.Name))} was added.\n\n" +
                $"It is now safe to delete the archive:\n{files}\n\nMove it to the Recycle Bin now?",
                "Extraction finished", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes)
                foreach (var f in result.ArchiveFiles)
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Extraction cancelled. Keep the archive.");
        }
        catch (NotEnoughSpaceException ex)
        {
            SetStatus("Not enough disk space.");
            Error(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus("Extraction failed. Keep the archive.");
            Error("Extraction failed, so keep the archive.\n\n" + ex.Message);
        }
        finally
        {
            _importCts = null;
            Progress.Visibility = CancelButton.Visibility = Visibility.Collapsed;
        }
    }

    void OnCancel(object sender, RoutedEventArgs e) => _importCts?.Cancel();

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_importCts != null && !Confirm("An archive is still being extracted. Stop it and close?")) { e.Cancel = true; return; }
        _importCts?.Cancel();
        Save();
    }

    // ---- drag and drop -------------------------------------------------------------------

    static string[] DroppedPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) ? (string[])e.Data.GetData(DataFormats.FileDrop) : Array.Empty<string>();

    void OnDragEnter(object sender, DragEventArgs e) { OnDragOver(sender, e); }

    void OnDragOver(object sender, DragEventArgs e)
    {
        var ok = DroppedPaths(e).Any(p => ArchiveExtractor.LooksLikeArchive(p) || Directory.Exists(p));
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    void OnDragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;

    async void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        var paths = DroppedPaths(e);
        foreach (var dir in paths.Where(Directory.Exists))
        {
            var found = GameDetector.Detect(dir);
            if (found.Count > 0) { AddGames(found); SetStatus($"Added {string.Join(", ", found.Select(f => f.Name))}."); }
            else Error($"No spice2x game was found in:\n{dir}");
        }
        // one archive set at a time; parts of the same set collapse to the first volume
        var archives = paths.Where(ArchiveExtractor.LooksLikeArchive).Select(ArchiveExtractor.FirstVolume)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var a in archives) await ImportArchive(a);
    }
}
