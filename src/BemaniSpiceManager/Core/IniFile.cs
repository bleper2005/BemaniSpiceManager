using System.IO;

namespace BemaniSpiceManager.Core;

/// <summary>
/// Minimal INI reader/writer that keeps comments, blank lines and key order when saving.
/// </summary>
public sealed class IniFile
{
    readonly List<string> _lines;
    public string Path { get; }

    public IniFile(string path)
    {
        Path = path;
        _lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
    }

    public sealed record Entry(string Section, string Key, string Value);

    public IEnumerable<Entry> Entries()
    {
        var section = "";
        foreach (var raw in _lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            if (line[0] == '[' && line.EndsWith(']')) { section = line[1..^1].Trim(); continue; }
            var eq = line.IndexOf('=');
            if (eq > 0) yield return new Entry(section, line[..eq].Trim(), line[(eq + 1)..].Trim());
        }
    }

    public string? Get(string section, string key) =>
        Entries().LastOrDefault(e => e.Section.Equals(section, StringComparison.OrdinalIgnoreCase)
                                  && e.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

    public void Set(string section, string key, string value)
    {
        var current = "";
        int sectionEnd = -1; bool sectionFound = section == "";
        if (sectionFound) sectionEnd = FirstSectionIndex();
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                if (current.Equals(section, StringComparison.OrdinalIgnoreCase)) { sectionFound = true; sectionEnd = i + 1; }
                continue;
            }
            if (!current.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Length > 0 && line[0] != ';' && line[0] != '#') sectionEnd = i + 1;
            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = $"{line[..eq].Trim()}={value}";
                return;
            }
        }
        if (!sectionFound)
        {
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0) _lines.Add("");
            _lines.Add($"[{section}]");
            sectionEnd = _lines.Count;
        }
        _lines.Insert(sectionEnd, $"{key}={value}");
    }

    int FirstSectionIndex()
    {
        int last = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            var l = _lines[i].Trim();
            if (l.StartsWith('[')) return last;
            if (l.Length > 0) last = i + 1;
        }
        return last;
    }

    public string Text => string.Join(Environment.NewLine, _lines);

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllLines(Path, _lines);
    }
}
