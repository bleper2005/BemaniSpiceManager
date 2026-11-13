using System.IO;
using System.Text.RegularExpressions;

namespace BemaniSpiceManager.Core;

/// <summary>
/// Line-based editor for simple YAML config files (like bemaniutils' server.yaml).
/// It only edits scalar "key: value" lines in place, so comments and layout survive.
/// </summary>
public sealed partial class YamlLines
{
    readonly List<string> _lines;
    public string Path { get; }

    public YamlLines(string path)
    {
        Path = path;
        _lines = File.ReadAllLines(path).ToList();
    }

    public sealed class Scalar
    {
        public required int Line { get; init; }
        /// <summary>Dotted path, e.g. server.port.</summary>
        public required string Key { get; init; }
        public required string Value { get; set; }
        public string Comment { get; init; } = "";
        public bool IsBool => Value is "True" or "False" or "true" or "false";
    }

    [GeneratedRegex(@"^(?<indent> *)(?<key>[A-Za-z0-9_\-]+):(?:\s+(?<val>.*?))?\s*$")]
    private static partial Regex KeyLine();

    public List<Scalar> Scalars()
    {
        var result = new List<Scalar>();
        var stack = new List<(int indent, string key)>();
        var comment = new List<string>();
        for (int i = 0; i < _lines.Count; i++)
        {
            var raw = _lines[i];
            var t = raw.Trim();
            if (t.StartsWith('#')) { comment.Add(t.TrimStart('#').Trim()); continue; }
            if (t.Length == 0) { comment.Clear(); continue; }
            var m = KeyLine().Match(raw);
            if (!m.Success) { comment.Clear(); continue; }
            int indent = m.Groups["indent"].Length;
            stack.RemoveAll(s => s.indent >= indent);
            var key = m.Groups["key"].Value;
            var val = m.Groups["val"].Success ? m.Groups["val"].Value : "";
            if (val.Length == 0) stack.Add((indent, key));
            else
            {
                var path = string.Join(".", stack.Select(s => s.key).Append(key));
                result.Add(new Scalar { Line = i, Key = path, Value = Unquote(val), Comment = string.Join(" ", comment) });
            }
            comment.Clear();
        }
        return result;
    }

    static string Unquote(string v) => v.Length >= 2 && v[0] == '"' && v[^1] == '"' ? v[1..^1] : v;

    public void Set(Scalar s, string value)
    {
        var raw = _lines[s.Line];
        var colon = raw.IndexOf(':');
        var old = raw[(colon + 1)..].Trim();
        bool quoted = old.StartsWith('"');
        bool needsQuote = quoted || (value.Length > 0 && !Regex.IsMatch(value, @"^[A-Za-z0-9_.\-/]+$"));
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        _lines[s.Line] = raw[..(colon + 1)] + " " + (needsQuote ? $"\"{escaped}\"" : value);
        s.Value = value;
    }

    public void Save() => File.WriteAllLines(Path, _lines);
}
