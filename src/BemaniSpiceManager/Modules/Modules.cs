using System.IO;
using System.Reflection;
using System.Windows;
using BemaniSpiceManager.Core;

namespace BemaniSpiceManager.Modules;

/// <summary>A game folder being examined during detection.</summary>
public sealed class GameFolder
{
    public string Root { get; }
    /// <summary>Where the game DLLs are: Root\modules if it exists, else Root (same rule as spice2x).</summary>
    public string ModulePath { get; }

    public GameFolder(string root)
    {
        Root = root;
        var m = System.IO.Path.Combine(root, "modules");
        ModulePath = Directory.Exists(m) ? m : root;
    }

    public bool HasDll(string name) => File.Exists(System.IO.Path.Combine(ModulePath, name));
    public bool HasFile(string relative) => File.Exists(System.IO.Path.Combine(Root, relative));
    public bool HasDir(string relative) => Directory.Exists(System.IO.Path.Combine(Root, relative));
}

/// <summary>
/// One supported game. To add a game, add a class deriving from GameModule anywhere in
/// Modules/Games; it is picked up automatically.
/// </summary>
public abstract class GameModule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    /// <summary>The game DLLs spice2x checks for (any one is enough).</summary>
    public abstract string[] Dlls { get; }
    /// <summary>Name spice2x uses for game-specific options (options.cpp game_name).</summary>
    public virtual string? SpiceGameName => null;
    /// <summary>Model codes this game uses, for display and server plugin matching.</summary>
    public virtual string[] Models => Array.Empty<string>();
    /// <summary>Higher wins when several modules match (e.g. HELLO! Pop'n vs pop'n).</summary>
    public virtual int Priority => 0;

    /// <summary>Returns the matching game DLL, or null.</summary>
    public virtual string? Match(GameFolder f) => Dlls.FirstOrDefault(f.HasDll);

    /// <summary>Optional per-version name, e.g. "pop'n music Jam&amp;Fizz" from the datecode.</summary>
    public virtual string DisplayName(string model, string dateCode) => Name;
}

/// <summary>
/// A local e-amusement server. To add one, derive from ServerModule; it shows up in the
/// Servers tab and in each game's server list automatically.
/// </summary>
public abstract class ServerModule
{
    protected AppState State { get; private set; } = null!;
    public void Attach(AppState state) => State = state;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract bool IsInstalled { get; }
    public abstract bool IsRunning { get; }
    /// <summary>URL passed to spice2x with -url.</summary>
    public abstract string ServiceUrl { get; }
    /// <summary>Starts the server if needed. Returns false (and logs why) if it can't.</summary>
    public abstract Task<bool> EnsureRunningAsync(Action<string> log);
    public abstract void Stop();
    /// <summary>Settings page shown in the Servers tab.</summary>
    public abstract FrameworkElement CreateView(Action save);
    /// <summary>Optional note shown next to a game that uses this server.</summary>
    public virtual string? NoteFor(GameEntry game) => null;
}

public static class ModuleRegistry
{
    public static IReadOnlyList<GameModule> Games { get; } = Discover<GameModule>().OrderByDescending(g => g.Priority).ToList();
    public static IReadOnlyList<ServerModule> Servers { get; } = Discover<ServerModule>().ToList();

    public static GameModule? Game(string id) => Games.FirstOrDefault(g => g.Id == id);
    public static ServerModule? Server(string id) => Servers.FirstOrDefault(s => s.Id == id);

    static IEnumerable<T> Discover<T>() =>
        Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name)
            .Select(t => (T)Activator.CreateInstance(t)!);
}
