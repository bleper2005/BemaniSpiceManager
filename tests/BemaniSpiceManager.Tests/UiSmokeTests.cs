using System.IO;
using System.Windows;
using System.Windows.Controls;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules;
using BemaniSpiceManager.Modules.Tools;
using BemaniSpiceManager.Pages;

namespace BemaniSpiceManager.Tests;

/// <summary>
/// Builds every page and window without showing anything on screen, to catch XAML and
/// layout-code crashes. Uses a throwaway data folder, never the real one.
/// </summary>
public class UiSmokeTests
{
    static void Sta(Action a)
    {
        Exception? err = null;
        var t = new Thread(() => { try { a(); } catch (Exception e) { err = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start(); t.Join();
        if (err != null) throw new Exception("UI code failed", err);
    }

    [Fact]
    public void AllPagesBuild()
    {
        using var tmp = new TempDir();
        Environment.SetEnvironmentVariable("BSM_DATA_DIR", Path.Combine(tmp.Path, "data"));
        try
        {
            var root = tmp.MakeFakePopn();
            var game = GameDetector.TryDetect(root)!;
            game.Card = Ids.NewCard(); game.Pcbid = Ids.NewPcbid();
            var state = new AppState
            {
                LibraryPath = Path.Combine(tmp.Path, "lib"),
                AsphyxiaFolder = Directory.Exists(@"C:\PopnArchive\asphyxia") ? @"C:\PopnArchive\asphyxia" : "",
                BemaniutilsFolder = Directory.Exists(@"C:\PopnArchive\bemaniutils") ? @"C:\PopnArchive\bemaniutils" : "",
                Games = { game },
            };
            state.Save();
            var iniBefore = state.AsphyxiaFolder != "" ? File.ReadAllText(Path.Combine(state.AsphyxiaFolder, "config.ini")) : "";

            Sta(() =>
            {
                if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var w = new MainWindow();                     // constructed, never shown
                var page = new GamePage(game, state, new GamePage.Callbacks(() => { }, _ => { }, () => { }));
                Assert.NotNull(page.Content);
                var gone = new GameEntry { Name = "gone", ModuleId = "popn", RootPath = Path.Combine(tmp.Path, "deleted") };
                Assert.NotNull(new GamePage(gone, state, new GamePage.Callbacks(() => { }, _ => { }, () => { })).Content);

                var opts = new SpiceOptionsWindow(game, "Pop'n Music");
                var cats = (ListBox)((DockPanel)opts.Content).Children[1];
                foreach (var c in cats.Items.Cast<object>().ToList()) cats.SelectedItem = c; // build every category
                opts.Close();

                foreach (var s in ModuleRegistry.Servers)
                {
                    s.Attach(state);
                    Assert.NotNull(s.CreateView(() => { }));
                }
                Assert.NotNull(SpicePage.Create(state, () => { }));
                Assert.NotNull(SettingsPage.Create(state, () => { }));
                w.Close();
            });

            // building pages must never write to the user's config files
            if (iniBefore != "") Assert.Equal(iniBefore, File.ReadAllText(Path.Combine(state.AsphyxiaFolder, "config.ini")));
            Assert.Single(AppState.Load().Games);
        }
        finally { Environment.SetEnvironmentVariable("BSM_DATA_DIR", null); }
    }
}
