using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules.Tools;
using static BemaniSpiceManager.Ui.Ui;

namespace BemaniSpiceManager.Modules.Servers;

/// <summary>Asphyxia CORE: a small local e-amusement server with per-game plugins.</summary>
public sealed partial class Asphyxia : ServerModule
{
    public override string Id => "asphyxia";
    public override string Name => "Asphyxia CORE";

    string Folder => State.AsphyxiaFolder;
    string ConfigPath => Path.Combine(Folder, "config.ini");

    string? Exe => string.IsNullOrEmpty(Folder) ? null
        : new[] { "asphyxia-core-x64.exe", "asphyxia-core.exe", "asphyxia-core-x86.exe" }
            .Select(n => Path.Combine(Folder, n)).FirstOrDefault(File.Exists);

    public override bool IsInstalled => Exe != null;

    Process? FindProcess()
    {
        var exe = Exe;
        if (exe == null) return null;
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            try { if (Shell.SamePath(p.MainModule?.FileName, exe)) return p; }
            catch { return p; } // can't read the path (other user/elevated): assume it's ours
        }
        return null;
    }

    public override bool IsRunning => FindProcess() != null;

    int Port => int.TryParse(new IniFile(ConfigPath).Get("", "port"), out var p) ? p : 8083;

    public override string ServiceUrl => $"http://localhost:{Port}";

    public override async Task<bool> EnsureRunningAsync(Action<string> log)
    {
        if (!IsInstalled) { log("Asphyxia CORE isn't set up. Open the Servers tab to download or locate it."); return false; }
        if (IsRunning) return true;
        log("Starting Asphyxia CORE…");
        Process.Start(new ProcessStartInfo(Exe!)
        {
            WorkingDirectory = Folder,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        });
        // wait until it accepts connections (plugins compile on first start, so allow some time)
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(500);
            if (await PortOpenAsync(Port)) return true;
            if (!IsRunning) { log("Asphyxia CORE closed right after starting. Check its window or config.ini."); return false; }
        }
        log("Asphyxia CORE is running but isn't answering yet; launching anyway.");
        return true;
    }

    internal static async Task<bool> PortOpenAsync(int port)
    {
        try
        {
            using var c = new System.Net.Sockets.TcpClient();
            var t = c.ConnectAsync("127.0.0.1", port);
            return await Task.WhenAny(t, Task.Delay(400)) == t && c.Connected;
        }
        catch { return false; }
    }

    public override void Stop()
    {
        var p = FindProcess();
        if (p == null) return;
        try { p.Kill(true); p.WaitForExit(5000); } catch { }
    }

    public override string? NoteFor(GameEntry game)
    {
        if (!IsInstalled || string.IsNullOrEmpty(game.Model)) return null;
        var plugins = Plugins();
        if (plugins.Count > 0 && !plugins.Any(p => p.GameCodes.Contains(game.Model)))
            return $"No installed Asphyxia plugin lists game code {game.Model}. Add a plugin for it in the Servers tab.";
        return null;
    }

    // ---- plugins -------------------------------------------------------------------------

    public sealed record PluginSetting(string Key, string Name, string Desc, string Type, string Default);
    public sealed record Plugin(string Id, string Folder, List<string> GameCodes, List<PluginSetting> Settings);

    [GeneratedRegex(@"R\.GameCode\(\s*['""`]([A-Za-z0-9]{3})['""`]\s*\)")] private static partial Regex GameCodeRx();
    [GeneratedRegex(@"R\.Config\(\s*['""`]([\w\-]+)['""`]\s*,\s*\{(.*?)\}\s*\)", RegexOptions.Singleline)] private static partial Regex ConfigRx();

    static string Prop(string body, string name)
    {
        var m = Regex.Match(body, name + @"\s*:\s*(?:(['""`])((?:\\.|(?!\1).)*)\1|([^,\n}]+))", RegexOptions.Singleline);
        return !m.Success ? "" : m.Groups[2].Success && m.Groups[1].Success ? m.Groups[2].Value : m.Groups[3].Value.Trim();
    }

    public List<Plugin> Plugins()
    {
        var list = new List<Plugin>();
        var dir = Path.Combine(Folder, "plugins");
        if (!Directory.Exists(dir)) return list;
        foreach (var p in Directory.GetDirectories(dir))
        {
            var id = Path.GetFileName(p);
            if (id.StartsWith('_') || id.StartsWith('.')) continue;
            var index = new[] { "index.ts", "index.js" }.Select(n => Path.Combine(p, n)).FirstOrDefault(File.Exists);
            if (index == null) continue;
            var src = File.ReadAllText(index);
            var codes = GameCodeRx().Matches(src).Select(m => m.Groups[1].Value.ToUpperInvariant()).Distinct().ToList();
            var settings = ConfigRx().Matches(src).Select(m => new PluginSetting(
                m.Groups[1].Value, Prop(m.Groups[2].Value, "name"), Prop(m.Groups[2].Value, "desc"),
                Prop(m.Groups[2].Value, "type"), Prop(m.Groups[2].Value, "default"))).ToList();
            list.Add(new Plugin(id, p, codes, settings));
        }
        return list;
    }

    // ---- downloads -----------------------------------------------------------------------

    async Task<string> DownloadCoreAsync(IProgress<string> log)
    {
        using var http = Net.Client();
        log.Report("Asking GitHub for the latest Asphyxia CORE…");
        using var doc = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/asphyxia-core/core/releases/latest"));
        var tag = doc.RootElement.GetProperty("tag_name").GetString()!;
        var url = doc.RootElement.GetProperty("assets").EnumerateArray()
            .Select(a => a.GetProperty("browser_download_url").GetString()!)
            .First(u => u.EndsWith("win-x64.zip"));
        var dest = Path.Combine(AppState.ToolsFolder, "asphyxia");
        Directory.CreateDirectory(dest);
        var zip = Path.Combine(AppState.ToolsFolder, $"asphyxia-{tag}.zip");
        await Net.DownloadAsync(http, url, zip, p => log.Report($"Downloading Asphyxia CORE {tag}… {p:0}%"), CancellationToken.None);
        // keep config.ini, plugins and savedata if they already exist
        using (var z = ZipFile.OpenRead(zip))
            foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
            {
                var target = Path.Combine(dest, e.FullName);
                if (File.Exists(target) && !e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                e.ExtractToFile(target, true);
            }
        File.Delete(zip);
        var exe = Directory.GetFiles(dest, "asphyxia-core*.exe", SearchOption.AllDirectories).FirstOrDefault()
                  ?? throw new IOException("asphyxia-core-x64.exe wasn't in the download.");
        return Path.GetDirectoryName(exe)!;
    }

    /// <summary>Adds the official plugins that aren't installed yet (never overwrites a plugin you have).</summary>
    async Task<int> DownloadPluginsAsync(IProgress<string> log)
    {
        using var http = Net.Client();
        var zip = Path.Combine(AppState.ToolsFolder, "asphyxia-plugins.zip");
        Directory.CreateDirectory(AppState.ToolsFolder);
        await Net.DownloadAsync(http, "https://github.com/asphyxia-core/plugins/archive/refs/heads/main.zip", zip,
            p => log.Report($"Downloading official plugins… {p:0}%"), CancellationToken.None);
        int added = 0;
        var pluginsDir = Path.Combine(Folder, "plugins");
        using (var z = ZipFile.OpenRead(zip))
        {
            // layout: plugins-main/<plugin@asphyxia>/...
            foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
            {
                var parts = e.FullName.Split('/');
                if (parts.Length < 3 || !parts[1].Contains('@')) continue;
                var pluginDir = Path.Combine(pluginsDir, parts[1]);
                if (Directory.Exists(pluginDir) && !File.Exists(Path.Combine(pluginDir, ".bsm-new"))) continue;
                if (!Directory.Exists(pluginDir)) { Directory.CreateDirectory(pluginDir); File.WriteAllText(Path.Combine(pluginDir, ".bsm-new"), ""); added++; }
                var target = Path.Combine(pluginsDir, Path.Combine(parts.Skip(1).ToArray()));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                e.ExtractToFile(target, true);
            }
        }
        foreach (var marker in Directory.GetFiles(pluginsDir, ".bsm-new", SearchOption.AllDirectories)) File.Delete(marker);
        File.Delete(zip);
        return added;
    }

    // ---- settings page -------------------------------------------------------------------

    public override FrameworkElement CreateView(Action save)
    {
        var root = new StackPanel { Margin = new Thickness(12) };
        void Rebuild()
        {
            root.Children.Clear();
            root.Children.Add(Header("Asphyxia CORE"));
            root.Children.Add(Note("Local e-amusement server. Profiles and scores are saved in its savedata folder. " +
                                   "Games using it are started with -url " + (IsInstalled ? ServiceUrl : "http://localhost:<port>") + "."));
            root.Children.Add(Setting("Folder", FolderPicker(Folder, v => { State.AsphyxiaFolder = v; save(); }, "Folder with asphyxia-core-x64.exe")));
            var status = new TextBlock();
            root.Children.Add(Row(
                IsInstalled ? Status(IsRunning ? "Running" : "Stopped", IsRunning) : Status("Not found", false),
                Button("Start", async () => { if (await EnsureRunningAsync(m => Error(m))) Rebuild(); }),
                Button("Stop", () => { Stop(); Rebuild(); }),
                Button("Refresh", Rebuild),
                Button("Open folder", () => Shell.Open(Folder)),
                Button("Open web UI", () => Shell.Open(ServiceUrl), "Profiles, scores and plugin pages (opens your browser)")));
            root.Children.Add(Row(
                Button(IsInstalled ? "Update Asphyxia CORE" : "Download Asphyxia CORE", async () =>
                {
                    try
                    {
                        if (IsRunning) Stop();
                        State.AsphyxiaFolder = await DownloadCoreAsync(new Progress<string>(s => status.Text = s));
                        save(); Rebuild();
                    }
                    catch (Exception ex) { Error("Download failed: " + ex.Message); }
                }, "From github.com/asphyxia-core/core (your config.ini, plugins and savedata are kept)"),
                Button("Add official plugins", async () =>
                {
                    if (!IsInstalled) { Error("Set up Asphyxia CORE first."); return; }
                    try
                    {
                        var n = await DownloadPluginsAsync(new Progress<string>(s => status.Text = s));
                        status.Text = n == 0 ? "All official plugins were already installed." : $"Added {n} plugin(s). Restart Asphyxia to load them.";
                        Rebuild();
                    }
                    catch (Exception ex) { Error("Download failed: " + ex.Message); }
                }, "From github.com/asphyxia-core/plugins. Plugins you already have are never replaced."),
                status));

            if (!IsInstalled) return;

            var ini = new IniFile(ConfigPath);
            void SetIni(string section, string key, string value) { ini.Set(section, key, value); ini.Save(); }

            root.Children.Add(Header("Server settings (config.ini)"));
            root.Children.Add(Note("Changes are saved right away; restart Asphyxia for them to apply."));
            var known = new (string key, string label, string desc, bool isBool)[]
            {
                ("port", "Port", "HTTP port games connect to.", false),
                ("bind", "Bind address", "localhost = only this PC; 0.0.0.0 = whole network.", false),
                ("ping_ip", "Ping IP", "Address games ping to check the network.", false),
                ("matching_port", "Matching port", "Port used for online matching.", false),
                ("allow_register", "Allow new cards", "Let unknown cards create a profile.", true),
                ("maintenance_mode", "Maintenance mode", "Games show maintenance and refuse network play.", true),
                ("enable_paseli", "Enable PASELI", "Fake unlimited PASELI balance.", true),
                ("webui_on_startup", "Open web UI on start", "Opens the browser every time Asphyxia starts.", true),
            };
            foreach (var k in known)
            {
                var cur = ini.Get("", k.key) ?? "";
                FrameworkElement ed = k.isBool
                    ? Check(cur.Equals("true", StringComparison.OrdinalIgnoreCase), v => SetIni("", k.key, v ? "true" : "false"))
                    : Text(cur, v => SetIni("", k.key, v), 160);
                root.Children.Add(Setting(k.label, ed, k.desc));
            }

            foreach (var p in Plugins())
            {
                var section = p.Id;
                root.Children.Add(Header($"Plugin {p.Id}"));
                root.Children.Add(Note(p.GameCodes.Count > 0 ? "Games: " + string.Join(", ", p.GameCodes) : "No game codes found."));
                if (p.Settings.Count == 0) root.Children.Add(Note("This plugin has no settings."));
                foreach (var s in p.Settings)
                {
                    var cur = ini.Get(section, s.Key) ?? s.Default;
                    FrameworkElement ed = s.Type == "boolean"
                        ? Check(cur.Equals("true", StringComparison.OrdinalIgnoreCase), v => SetIni(section, s.Key, v ? "true" : "false"))
                        : Text(cur, v => SetIni(section, s.Key, v), 200);
                    root.Children.Add(Setting(string.IsNullOrEmpty(s.Name) ? s.Key : s.Name, ed, s.Desc));
                }
            }

            root.Children.Add(Header("Raw config.ini"));
            root.Children.Add(Row(Button("Open config.ini in Notepad", () => Shell.Open(ConfigPath))));
        }
        Rebuild();
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
