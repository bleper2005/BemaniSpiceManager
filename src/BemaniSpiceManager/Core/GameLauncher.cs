using System.Diagnostics;
using System.IO;
using BemaniSpiceManager.Modules;
using BemaniSpiceManager.Modules.Tools;

namespace BemaniSpiceManager.Core;

public static class GameLauncher
{
    /// <summary>How spice2x gets started; tests swap this out so no game actually runs.</summary>
    public static Func<ProcessStartInfo, Process?> StartProcess { get; set; } = Process.Start;

    /// <summary>spice2x arguments for a game. Server URL, PCBID and card come from the game page.</summary>
    public static List<string> BuildArgs(GameEntry g, AppState state)
    {
        var args = new List<string>();
        switch (g.ServerId)
        {
            case "none":
                // spice2x's built-in EA: boots the game but saves nothing
                args.Add("-ea");
                break;
            case "custom":
                if (!string.IsNullOrWhiteSpace(g.CustomUrl)) { args.Add("-url"); args.Add(g.CustomUrl.Trim()); }
                break;
            default:
                var server = ModuleRegistry.Server(g.ServerId);
                if (server != null) { args.Add("-url"); args.Add(server.ServiceUrl); }
                break;
        }
        if (!string.IsNullOrWhiteSpace(g.Pcbid)) { args.Add("-p"); args.Add(g.Pcbid.Trim()); }
        if (!string.IsNullOrWhiteSpace(g.Card)) { args.Add("-card0"); args.Add(g.Card.Trim()); }

        var known = Spice2x.Options.ToDictionary(o => o.Name);
        foreach (var (name, value) in g.SpiceOptions.OrderBy(kv => kv.Key))
        {
            if (Spice2x.ManagedOptions.Contains(name) || !known.TryGetValue(name, out var opt)) continue;
            if (opt.Type == "Bool")
            {
                if (value is "1" or "true" or "") args.Add("-" + name);
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                args.Add("-" + name);
                args.Add(value);
            }
        }
        return args;
    }

    public static string CommandLine(GameEntry g, AppState state)
    {
        var exe = Spice2x.Exe(state.SpiceFolder, g.Is64Bit) ?? (g.Is64Bit ? "spice64.exe" : "spice.exe");
        return string.Join(" ", new[] { Shell.Quote(exe) }.Concat(BuildArgs(g, state).Select(Shell.Quote)));
    }

    /// <summary>Starts the game's server (if any) and then spice2x. Returns an error message or null.</summary>
    public static async Task<string?> LaunchAsync(GameEntry g, AppState state, Action<string> log)
    {
        var exe = Spice2x.Exe(state.SpiceFolder, g.Is64Bit);
        if (exe == null)
            return $"{(g.Is64Bit ? "spice64.exe" : "spice.exe")} wasn't found. Set up spice2x in the spice2x tab.";
        if (!Directory.Exists(g.RootPath))
            return $"The game folder is missing:\n{g.RootPath}";

        var server = ModuleRegistry.Server(g.ServerId);
        if (server != null)
        {
            string? err = null;
            if (!await server.EnsureRunningAsync(m => err = m)) return err ?? $"{server.Name} could not be started.";
        }

        var psi = new ProcessStartInfo(exe) { WorkingDirectory = g.RootPath, UseShellExecute = false };
        foreach (var a in BuildArgs(g, state)) psi.ArgumentList.Add(a);
        log($"Starting {g.Name}…");
        StartProcess(psi);
        return null;
    }
}
