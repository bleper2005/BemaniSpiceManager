using System.IO;
using System.Windows;
using System.Windows.Controls;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules.Tools;
using static BemaniSpiceManager.Ui.Ui;

namespace BemaniSpiceManager.Pages;

public static class SpicePage
{
    public static FrameworkElement Create(AppState state, Action save)
    {
        var root = new StackPanel { Margin = new Thickness(16) };
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        void Rebuild()
        {
            root.Children.Clear();
            root.Children.Add(Header("spice2x"));
            root.Children.Add(Note("spice2x loads the game and emulates the cabinet I/O. One copy is shared by all games; " +
                                   "spice64.exe is used for 64-bit games and spice.exe for 32-bit ones."));
            var ok = Spice2x.IsValidFolder(state.SpiceFolder);
            root.Children.Add(Row(Status(ok ? "Found" : "Not set up", ok),
                new TextBlock { Text = ok ? VersionText(state.SpiceFolder) : "", VerticalAlignment = VerticalAlignment.Center }));
            root.Children.Add(Setting("spice2x folder", FolderPicker(state.SpiceFolder, v =>
            {
                state.SpiceFolder = Spice2x.NormalizeFolder(v) ?? v; save();
            }, "Folder with spice64.exe"), "Pick an existing spice2x folder, or download the latest release below."));
            root.Children.Add(Row(
                Button("Download latest spice2x", async () =>
                {
                    try
                    {
                        state.SpiceFolder = await Spice2x.DownloadLatestAsync(new Progress<string>(s => status.Text = s), CancellationToken.None);
                        save(); status.Text = "spice2x is ready."; Rebuild();
                    }
                    catch (Exception ex) { Error("Download failed: " + ex.Message); }
                }, "From github.com/spice2x/spice2x.github.io releases"),
                Button("Check folder", Rebuild),
                Button("Open folder", () => Shell.Open(state.SpiceFolder)),
                Button("spice2x website", () => Shell.Open("https://spice2x.github.io/")),
                status));
            root.Children.Add(Header("Options list"));
            root.Children.Add(Note($"{Spice2x.Options.Count} options are known (taken from spice2x's launcher/options.cpp). " +
                                   "Set them per game with \"Edit spice2x options…\" on a game's page. " +
                                   "Settings made in spicecfg (controls, etc.) still apply; options set here override them."));
        }
        Rebuild();
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    static string VersionText(string folder)
    {
        var exe = Path.Combine(folder, "spice64.exe");
        if (!File.Exists(exe)) exe = Path.Combine(folder, "spice.exe");
        var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
        return $"{exe}   (modified {File.GetLastWriteTime(exe):yyyy-MM-dd}{(string.IsNullOrEmpty(v.ProductVersion) ? "" : ", " + v.ProductVersion)})";
    }
}

public static class SettingsPage
{
    public static FrameworkElement Create(AppState state, Action save)
    {
        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(Header("Library"));
        root.Children.Add(Setting("Library folder", FolderPicker(state.LibraryPath, v => { state.LibraryPath = v; save(); }, "Where extracted games go"),
            "Dropped archives are extracted here, one folder per game. Pick a drive with plenty of space (games are often 30-80 GB unpacked)."));
        var free = new TextBlock { Foreground = System.Windows.Media.Brushes.DimGray };
        void UpdateFree()
        {
            try
            {
                var d = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(state.LibraryPath))!);
                free.Text = $"Free space on {d.Name}: {Shell.FormatSize(d.AvailableFreeSpace)}";
            }
            catch { free.Text = ""; }
        }
        UpdateFree();
        root.Children.Add(Row(free, Button("Refresh", UpdateFree)));

        root.Children.Add(Header("Extraction"));
        var ex = new ArchiveExtractor(state.SevenZipPath);
        root.Children.Add(Note(ex.SevenZip != null
            ? $"Using 7-Zip: {ex.SevenZip}"
            : "7-Zip wasn't found, so the slower built-in extractor is used. Installing 7-Zip (7-zip.org) makes big archives much faster."));
        root.Children.Add(Setting("7-Zip folder (optional)", FolderPicker(state.SevenZipPath, v => { state.SevenZipPath = v; save(); }, "Folder with 7z.exe"),
            "Only needed if 7-Zip is installed somewhere unusual."));

        root.Children.Add(Header("Shortcuts"));
        var made = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = System.Windows.Media.Brushes.DimGray };
        void Make(string folder)
        {
            try { made.Text = "Created " + Shortcuts.CreateForApp(folder); }
            catch (Exception e) { Error("Couldn't create the shortcut: " + e.Message); }
        }
        root.Children.Add(Row(Button("Add app to desktop", () => Make(Shortcuts.Desktop)),
            Button("Add app to Start menu", () => Make(Shortcuts.StartMenu)), made));
        root.Children.Add(Note("Each game's page also has buttons for a one-click launch shortcut."));

        root.Children.Add(Header("App data"));
        root.Children.Add(Note($"Settings and the game list are stored in {AppState.DataFolder}. Downloaded tools go in {AppState.ToolsFolder}."));
        root.Children.Add(Row(Button("Open app data folder", () => Shell.Open(AppState.DataFolder))));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
