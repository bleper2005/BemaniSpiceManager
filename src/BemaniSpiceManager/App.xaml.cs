using System.Windows;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules;

namespace BemaniSpiceManager;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // BemaniSpiceManager.exe --launch <game id>: used by per-game shortcuts.
        // Starts the game's server and spice2x without opening the main window.
        var i = Array.FindIndex(e.Args, a => a.Equals("--launch", StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var id = i + 1 < e.Args.Length ? e.Args[i + 1] : "";
            var state = AppState.Load();
            foreach (var s in ModuleRegistry.Servers) s.Attach(state);
            var game = state.Games.FirstOrDefault(g => g.Id == id);
            string? err;
            try
            {
                err = game == null
                    ? "This shortcut's game isn't in Bemani Spice Manager's list any more. Open the app and create the shortcut again."
                    : await GameLauncher.LaunchAsync(game, state, _ => { });
            }
            catch (Exception ex) { err = ex.Message; }
            if (err != null)
                MessageBox.Show(err, "Bemani Spice Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
