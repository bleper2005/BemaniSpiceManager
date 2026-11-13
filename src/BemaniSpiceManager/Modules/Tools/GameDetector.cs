using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BemaniSpiceManager.Core;

namespace BemaniSpiceManager.Modules.Tools;

/// <summary>Finds a spice2x-runnable game inside a folder and reads its model/datecode.</summary>
public static partial class GameDetector
{
    const int MaxDepth = 5;

    /// <summary>All games found under <paramref name="folder"/> (an extracted archive can hold several).</summary>
    public static List<GameEntry> Detect(string folder)
    {
        var found = new List<GameEntry>();
        Walk(folder, 0, found);
        return found;
    }

    static void Walk(string dir, int depth, List<GameEntry> found)
    {
        var g = TryDetect(dir);
        if (g != null) { found.Add(g); return; } // don't look inside a game folder
        if (depth >= MaxDepth) return;
        string[] subs;
        try { subs = Directory.GetDirectories(dir); } catch { return; }
        foreach (var s in subs.OrderBy(x => x))
        {
            var n = Path.GetFileName(s).ToLowerInvariant();
            if (n is "modules" or "data" or "plain_data" or "prop" or "dev" or "sd" or "tex") continue;
            Walk(s, depth + 1, found);
        }
    }

    public static GameEntry? TryDetect(string root)
    {
        var f = new GameFolder(root);
        foreach (var m in ModuleRegistry.Games)
        {
            var dll = m.Match(f);
            if (dll == null) continue;
            var e = new GameEntry { ModuleId = m.Id, RootPath = root, GameDll = dll };
            ReadMetadata(f, e);
            if (string.IsNullOrEmpty(e.Model) && m.Models.Length > 0) e.Model = m.Models[0];
            e.Is64Bit = Pe.Is64Bit(Path.Combine(f.ModulePath, dll)) ?? true;
            e.Name = m.DisplayName(e.Model, e.DateCode);
            return e;
        }
        return null;
    }

    [GeneratedRegex(@"(?<![A-Z0-9])([A-Z0-9]{3})[-_:]([A-Z])?[-_:]?(?:[A-Z][-_:]?[A-Z][-_:]?)?(\d{10})(?!\d)")]
    private static partial Regex ModelDate();

    [GeneratedRegex(@"(?<!\d)(20\d{8})(?!\d)")]
    private static partial Regex DateOnly();

    static void ReadMetadata(GameFolder f, GameEntry e)
    {
        // 1. prop\ea3-config.xml / ea3-ident.xml: <soft><model/><dest/><spec/><rev/><ext/></soft>
        foreach (var rel in new[] { @"prop\ea3-config.xml", @"prop\ea3-ident.xml", @"prop\ea3.xml" })
        {
            var p = Path.Combine(f.Root, rel);
            if (!File.Exists(p)) continue;
            try
            {
                var soft = XDocument.Load(p).Descendants("soft").FirstOrDefault();
                if (soft == null) continue;
                string V(string n) => soft.Element(n)?.Value.Trim() ?? "";
                if (V("model").Length == 3) e.Model = V("model");
                e.Dest = V("dest"); e.Spec = V("spec"); e.Rev = V("rev");
                if (Regex.IsMatch(V("ext"), @"^\d{10}$")) e.DateCode = V("ext");
                if (!string.IsNullOrEmpty(e.DateCode)) return;
            }
            catch { /* unreadable xml: try the next source */ }
        }

        // 2. folder names like "M39-2026041500" or "LDJ-012-2024100900"
        int up = 0;
        for (var d = new DirectoryInfo(f.Root); d != null && up++ < 3 && string.IsNullOrEmpty(e.DateCode); d = d.Parent)
        {
            var m = ModelDate().Match(d.Name.ToUpperInvariant());
            if (m.Success)
            {
                if (string.IsNullOrEmpty(e.Model)) e.Model = m.Groups[1].Value;
                e.DateCode = m.Groups[3].Value;
                break;
            }
            var dm = DateOnly().Match(d.Name);
            if (dm.Success) { e.DateCode = dm.Groups[1].Value; break; }
        }

        // 3. patches\*.json (spice2x patch export) carries gameCode and "version": "2026-04-15 (003)"
        var patches = Path.Combine(f.Root, "patches");
        if (Directory.Exists(patches))
        {
            foreach (var j in Directory.GetFiles(patches, "*.json"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(j));
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var head = doc.RootElement[0];
                        if (head.TryGetProperty("gameCode", out var gc) && string.IsNullOrEmpty(e.Model)) e.Model = gc.GetString() ?? "";
                        if (head.TryGetProperty("version", out var v) && string.IsNullOrEmpty(e.DateCode))
                        {
                            var digits = new string((v.GetString() ?? "").TakeWhile(c => c != '(').Where(char.IsDigit).ToArray());
                            if (digits.Length == 8) e.DateCode = digits + "00";
                        }
                    }
                }
                catch { }
            }
        }
    }
}
