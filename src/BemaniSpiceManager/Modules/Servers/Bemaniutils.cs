using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BemaniSpiceManager.Core;
using static BemaniSpiceManager.Ui.Ui;

namespace BemaniSpiceManager.Modules.Servers;

/// <summary>
/// bemaniutils (github.com/DragonMinded/bemaniutils): a full e-amusement network in Python
/// that stores data in MySQL. This module starts its services script and edits server.yaml.
/// </summary>
public sealed class Bemaniutils : ServerModule
{
    public override string Id => "bemaniutils";
    public override string Name => "bemaniutils";

    string Folder => State.BemaniutilsFolder;
    string ConfigPath => Path.Combine(Folder, "config", "server.yaml");
    string ServicesScript => Path.Combine(Folder, "services");

    public override bool IsInstalled => !string.IsNullOrEmpty(Folder) && File.Exists(ServicesScript) && File.Exists(ConfigPath);

    Process? _proc;

    public override bool IsRunning => (_proc != null && !_proc.HasExited);

    public override string ServiceUrl => $"http://localhost:{State.BemaniutilsPort}";

    Process StartPython(string script, string args, bool minimized)
    {
        return Process.Start(new ProcessStartInfo("cmd.exe",
            $"/k \"{Shell.Quote(State.PythonPath)} {Shell.Quote(script)} {args}\"")
        {
            WorkingDirectory = Folder,
            UseShellExecute = true,
            WindowStyle = minimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
        })!;
    }

    public override async Task<bool> EnsureRunningAsync(Action<string> log)
    {
        if (!IsInstalled) { log("bemaniutils isn't set up. Pick its folder in the Servers tab."); return false; }
        if (IsRunning || await Asphyxia.PortOpenAsync(State.BemaniutilsPort)) return true;
        log("Starting bemaniutils services…");
        _proc = StartPython(ServicesScript, $"-c config\\server.yaml -p {State.BemaniutilsPort}", true);
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(500);
            if (await Asphyxia.PortOpenAsync(State.BemaniutilsPort)) return true;
        }
        log("bemaniutils didn't start listening. Open its window to see the error (MySQL running? requirements installed?).");
        return false;
    }

    public override void Stop()
    {
        try { _proc?.Kill(true); } catch { }
        _proc = null;
    }

    public override string? NoteFor(GameEntry game)
    {
        if (!IsInstalled) return null;
        var addr = new YamlLines(ConfigPath).Scalars().FirstOrDefault(s => s.Key == "server.address")?.Value;
        return addr is "127.0.0.1" or "localhost" ? null
            : $"server.yaml advertises address {addr}; games on this PC need 127.0.0.1 (button in the Servers tab).";
    }

    public override FrameworkElement CreateView(Action save)
    {
        var root = new StackPanel { Margin = new Thickness(12) };
        void Rebuild()
        {
            root.Children.Clear();
            root.Children.Add(Header("bemaniutils"));
            root.Children.Add(Note("A full Python e-amusement network. It needs Python 3 with its requirements installed " +
                                   "(pip install -r requirements.txt) and a MySQL database matching the database section below."));
            root.Children.Add(Setting("Folder", FolderPicker(Folder, v => { State.BemaniutilsFolder = v; save(); }, "bemaniutils checkout folder")));
            root.Children.Add(Setting("Python", Text(State.PythonPath, v => { State.PythonPath = v; save(); }, 380), "python, py, or a full path to python.exe"));
            var port = new ComboBox { Width = 100, IsEditable = true, HorizontalAlignment = HorizontalAlignment.Left, Text = State.BemaniutilsPort.ToString() };
            foreach (var p in new[] { 80, 8080, 8084, 5730 }) port.Items.Add(p.ToString());
            port.SelectionChanged += (_, _) => { if (port.SelectedItem is string s && int.TryParse(s, out var v)) { State.BemaniutilsPort = v; save(); } };
            port.LostFocus += (_, _) => { if (int.TryParse(port.Text, out var v)) { State.BemaniutilsPort = v; save(); } };
            root.Children.Add(Setting("Port", port, "Port the services listen on (overrides server.port)."));
            root.Children.Add(Row(
                IsInstalled ? Status(IsRunning ? "Running" : "Stopped", IsRunning) : Status("Not found", false),
                Button("Start", async () => { await EnsureRunningAsync(m => Error(m)); Rebuild(); }),
                Button("Stop", () => { Stop(); Rebuild(); }),
                Button("Create database tables", () => StartPython(Path.Combine(Folder, "dbutils"), "-c config\\server.yaml create", false),
                    "Runs: dbutils -c config\\server.yaml create (first-time setup, MySQL must be running)"),
                Button("Open folder", () => Shell.Open(Folder))));

            if (!IsInstalled) { root.Children.Add(Note("Pick the folder that contains the services script and config\\server.yaml.")); return; }

            var yaml = new YamlLines(ConfigPath);
            var scalars = yaml.Scalars();
            root.Children.Add(Header("Server config (config\\server.yaml)"));
            root.Children.Add(Row(Button("Set up for this PC", () =>
            {
                foreach (var s in scalars.Where(s => s.Key is "server.address" or "server.keepalive")) yaml.Set(s, "127.0.0.1");
                yaml.Save(); Rebuild();
            }, "Sets server.address and server.keepalive to 127.0.0.1"),
                Button("Open server.yaml in Notepad", () => Shell.Open(ConfigPath))));
            root.Children.Add(Note("Only single values are listed; comments in the file are kept. Lists (like webhooks) can be edited in the file."));
            string? group = null;
            foreach (var s in scalars)
            {
                var g = s.Key.Contains('.') ? s.Key[..s.Key.IndexOf('.')] : "";
                if (g != group) { group = g; root.Children.Add(new TextBlock { Text = g, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) }); }
                var label = s.Key.Contains('.') ? s.Key[(s.Key.IndexOf('.') + 1)..] : s.Key;
                FrameworkElement ed = s.IsBool
                    ? Check(s.Value.Equals("true", StringComparison.OrdinalIgnoreCase), v => { yaml.Set(s, v ? "True" : "False"); yaml.Save(); })
                    : Text(s.Value, v => { yaml.Set(s, v); yaml.Save(); }, 300);
                root.Children.Add(Setting(label, ed, s.Comment));
            }
        }
        Rebuild();
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
