using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using BemaniSpiceManager.Core;

namespace BemaniSpiceManager.Modules.Tools;

/// <summary>One spice2x command-line option (generated from spice2x launcher/options.cpp).</summary>
public sealed class SpiceOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("desc")] public string Desc { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "Text";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("setting_name")] public string Example { get; set; } = "";
    [JsonPropertyName("hidden")] public bool Hidden { get; set; }
    [JsonPropertyName("disabled")] public bool Disabled { get; set; }
    [JsonPropertyName("games")] public List<string> Games { get; set; } = new();
    [JsonPropertyName("elements")] public List<List<string>> Elements { get; set; } = new();
}

public static class Spice2x
{
    /// <summary>Options managed elsewhere in the app (server URL, PCBID, card, built-in EA).</summary>
    public static readonly HashSet<string> ManagedOptions = new() { "url", "p", "card0", "ea" };

    static List<SpiceOption>? _options;

    public static IReadOnlyList<SpiceOption> Options
    {
        get
        {
            if (_options != null) return _options;
            using var s = typeof(Spice2x).Assembly.GetManifestResourceStream("spice-options.json")!;
            _options = JsonSerializer.Deserialize<List<SpiceOption>>(s)!
                .Where(o => !o.Hidden && !o.Disabled && !ManagedOptions.Contains(o.Name))
                .ToList();
            return _options;
        }
    }

    /// <summary>Options that apply to a game: general ones plus that game's own.</summary>
    public static IEnumerable<SpiceOption> OptionsFor(string? spiceGameName) =>
        Options.Where(o => o.Games.Count == 0 || (spiceGameName != null && o.Games.Contains(spiceGameName)));

    public static string? Exe(string folder, bool is64) =>
        string.IsNullOrEmpty(folder) ? null
        : new[] { Path.Combine(folder, is64 ? "spice64.exe" : "spice.exe") }.FirstOrDefault(File.Exists);

    public static bool IsValidFolder(string folder) =>
        !string.IsNullOrEmpty(folder) && (File.Exists(Path.Combine(folder, "spice64.exe")) || File.Exists(Path.Combine(folder, "spice.exe")));

    /// <summary>Accepts the spice2x folder or any exe inside it.</summary>
    public static string? NormalizeFolder(string path)
    {
        if (File.Exists(path)) path = Path.GetDirectoryName(path)!;
        if (IsValidFolder(path)) return path;
        // release zips nest everything in a "spice2x" subfolder
        var sub = Directory.Exists(path) ? Directory.GetDirectories(path).FirstOrDefault(IsValidFolder) : null;
        return sub;
    }

    const string ReleasesApi = "https://api.github.com/repos/spice2x/spice2x.github.io/releases/latest";

    /// <summary>Downloads the latest spice2x release into the app's tools folder. Returns the folder.</summary>
    public static async Task<string> DownloadLatestAsync(IProgress<string> log, CancellationToken ct)
    {
        using var http = Net.Client();
        log.Report("Asking GitHub for the latest spice2x release…");
        using var doc = JsonDocument.Parse(await http.GetStringAsync(ReleasesApi, ct));
        var tag = doc.RootElement.GetProperty("tag_name").GetString()!;
        var asset = doc.RootElement.GetProperty("assets").EnumerateArray()
            .Select(a => (name: a.GetProperty("name").GetString()!, url: a.GetProperty("browser_download_url").GetString()!))
            .Where(a => a.name.EndsWith(".zip") && !a.name.Contains("-full"))
            .FirstOrDefault();
        if (asset.url == null) throw new IOException("The latest spice2x release has no zip file.");
        var dest = Path.Combine(AppState.ToolsFolder, "spice2x-" + tag);
        var zip = dest + ".zip";
        Directory.CreateDirectory(AppState.ToolsFolder);
        await Net.DownloadAsync(http, asset.url, zip, p => log.Report($"Downloading spice2x {tag}… {p:0}%"), ct);
        log.Report("Unpacking spice2x…");
        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        ZipFile.ExtractToDirectory(zip, dest);
        File.Delete(zip);
        return NormalizeFolder(dest) ?? throw new IOException("Downloaded spice2x, but spice64.exe wasn't in the zip.");
    }
}

public static class Net
{
    public static HttpClient Client()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("BemaniSpiceManager/0.1");
        return h;
    }

    public static async Task DownloadAsync(HttpClient http, string url, string file, Action<double> progress, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(file);
        var buf = new byte[1 << 16];
        long done = 0; int n; var last = DateTime.MinValue;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            done += n;
            if (total > 0 && (DateTime.Now - last).TotalMilliseconds > 200) { progress(done * 100.0 / total); last = DateTime.Now; }
        }
        progress(100);
    }
}
