using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BemaniSpiceManager.Core;

/// <summary>One installed game in the library.</summary>
public sealed class GameEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>Id of the game module that detected it (see Modules/Games).</summary>
    public string ModuleId { get; set; } = "";
    /// <summary>3-character model code, e.g. M39.</summary>
    public string Model { get; set; } = "";
    public string Dest { get; set; } = "";
    public string Spec { get; set; } = "";
    public string Rev { get; set; } = "";
    /// <summary>10-digit datecode (ext), e.g. 2026041500.</summary>
    public string DateCode { get; set; } = "";
    /// <summary>Folder spice2x is started from (holds modules\ or the game DLL).</summary>
    public string RootPath { get; set; } = "";
    public string GameDll { get; set; } = "";
    public bool Is64Bit { get; set; } = true;
    /// <summary>Server module id ("asphyxia", "bemaniutils"), "custom", or "none".</summary>
    public string ServerId { get; set; } = "asphyxia";
    public string CustomUrl { get; set; } = "";
    public string Pcbid { get; set; } = "";
    public string Card { get; set; } = "";
    /// <summary>spice2x option name to value ("" for a set flag).</summary>
    public Dictionary<string, string> SpiceOptions { get; set; } = new();
    public string SourceArchive { get; set; } = "";
    public DateTime Added { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string Subtitle => string.Join("  ", new[] { Model, DateCode }.Where(s => !string.IsNullOrEmpty(s)));
}

/// <summary>Everything the app remembers, saved as JSON in %LocalAppData%\BemaniSpiceManager.</summary>
public sealed class AppState
{
    public string LibraryPath { get; set; } = "";
    public string SpiceFolder { get; set; } = "";
    public string SevenZipPath { get; set; } = "";
    public string AsphyxiaFolder { get; set; } = "";
    public string BemaniutilsFolder { get; set; } = "";
    public string PythonPath { get; set; } = "python";
    public int BemaniutilsPort { get; set; } = 8084;
    public List<GameEntry> Games { get; set; } = new();

    /// <summary>
    /// %LocalAppData%\BemaniSpiceManager normally; a "data" folder next to the exe when a file
    /// named portable.txt sits beside it; or BSM_DATA_DIR if that environment variable is set.
    /// </summary>
    public static string DataFolder
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("BSM_DATA_DIR");
            var dir = !string.IsNullOrEmpty(env) ? env
                : File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt")) ? Path.Combine(AppContext.BaseDirectory, "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BemaniSpiceManager");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Tools the app downloads itself (spice2x, Asphyxia) live here.</summary>
    public static string ToolsFolder => Path.Combine(DataFolder, "tools");

    static string FilePath => Path.Combine(DataFolder, "state.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Why the last Load fell back to an empty state, if it did.</summary>
    public static string? LastLoadError { get; private set; }

    public static AppState Load()
    {
        LastLoadError = null;
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Json) ?? new AppState();
        }
        catch (Exception ex)
        {
            // unreadable or corrupt file: start fresh, but keep a copy so nothing is lost
            LastLoadError = ex.GetType().Name + ": " + ex.Message;
            try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
        }
        return new AppState();
    }

    public void Save()
    {
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, FilePath, true);
    }
}
