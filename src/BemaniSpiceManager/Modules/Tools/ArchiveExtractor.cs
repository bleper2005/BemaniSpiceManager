using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace BemaniSpiceManager.Modules.Tools;

/// <summary>
/// Extracts .zip/.7z/.rar (including multi-part sets). Uses 7-Zip when it is installed because
/// it is much faster on 30-50 GB archives, and falls back to the built-in SharpCompress.
/// </summary>
public sealed partial class ArchiveExtractor
{
    public string? SevenZip { get; }

    /// <param name="forceBuiltIn">Skip 7-Zip even if installed (used by tests).</param>
    public ArchiveExtractor(string? configured7z, bool forceBuiltIn = false)
    {
        SevenZip = forceBuiltIn ? null : Find7z(configured7z);
    }

    /// <summary>Set by tests to exercise the SharpCompress path.</summary>
    public static bool ForceBuiltIn { get; set; }

    public string EngineName => SevenZip != null ? "7-Zip" : "built-in extractor";

    public static readonly string[] Extensions = { ".zip", ".7z", ".rar", ".001" };

    public static bool LooksLikeArchive(string path)
    {
        var n = path.ToLowerInvariant();
        return Extensions.Any(n.EndsWith) || PartRar().IsMatch(n) || OldRar().IsMatch(n);
    }

    [GeneratedRegex(@"\.part(\d+)\.rar$")] private static partial Regex PartRar();
    [GeneratedRegex(@"\.r(\d\d)$")] private static partial Regex OldRar();
    [GeneratedRegex(@"\.(7z|zip)\.(\d{3})$")] private static partial Regex NumberedPart();

    /// <summary>For a multi-part set, returns the first volume (the one 7-Zip must be given).</summary>
    public static string FirstVolume(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        var m = PartRar().Match(name.ToLowerInvariant());
        if (m.Success)
        {
            var width = m.Groups[1].Value.Length;
            var first = name[..m.Index] + ".part" + "1".PadLeft(width, '0') + ".rar";
            if (File.Exists(Path.Combine(dir, first))) return Path.Combine(dir, first);
        }
        m = NumberedPart().Match(name.ToLowerInvariant());
        if (m.Success)
        {
            var first = name[..^3] + "001";
            if (File.Exists(Path.Combine(dir, first))) return Path.Combine(dir, first);
        }
        if (OldRar().IsMatch(name.ToLowerInvariant()))
        {
            var rar = Path.ChangeExtension(path, ".rar");
            if (File.Exists(rar)) return rar;
        }
        return path;
    }

    /// <summary>All files belonging to the archive set (for the "safe to delete" message).</summary>
    public static List<string> AllVolumes(string first)
    {
        var dir = Path.GetDirectoryName(first)!;
        var name = Path.GetFileName(first);
        var lower = name.ToLowerInvariant();
        var m = PartRar().Match(lower);
        if (m.Success)
        {
            var prefix = name[..m.Index];
            return Directory.GetFiles(dir, prefix + ".part*.rar").OrderBy(x => x).ToList();
        }
        if (NumberedPart().IsMatch(lower))
            return Directory.GetFiles(dir, name[..^3] + "???").OrderBy(x => x).ToList();
        if (lower.EndsWith(".rar"))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var list = new List<string> { first };
            list.AddRange(Directory.GetFiles(dir, stem + ".r??").Where(f => OldRar().IsMatch(f.ToLowerInvariant())).OrderBy(x => x));
            return list;
        }
        return new List<string> { first };
    }

    static string? Find7z(string? configured)
    {
        var candidates = new List<string?> { configured };
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\7-Zip");
                candidates.Add(k?.GetValue("Path64") as string);
                candidates.Add(k?.GetValue("Path") as string);
            }
            catch { }
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip"));
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            var exe = c.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? c : Path.Combine(c, "7z.exe");
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    /// <summary>Total uncompressed size in bytes.</summary>
    public async Task<long> GetUncompressedSizeAsync(string archive, CancellationToken ct)
    {
        if (SevenZip != null)
        {
            var (code, output) = await Run7zAsync($"l -slt -sccUTF-8 {Core.Shell.Quote(archive)}", null, ct);
            if (code != 0) throw new IOException("7-Zip could not read the archive:\n" + LastLines(output));
            long total = 0; bool entries = false;
            foreach (var line in output.Split('\n'))
            {
                var l = line.TrimEnd('\r');
                if (l.StartsWith("----------")) { entries = true; continue; }
                if (entries && l.StartsWith("Size = ") && long.TryParse(l[7..], out var s)) total += s;
            }
            return total;
        }
        return await Task.Run(() =>
        {
            using var a = OpenArchive(archive);
            return a.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        }, ct);
    }

    static IArchive OpenArchive(string archive)
    {
        var vols = AllVolumes(archive);
        return vols.Count > 1
            ? ArchiveFactory.OpenArchive(vols.Select(v => new FileInfo(v)).ToList())
            : ArchiveFactory.OpenArchive(new FileInfo(archive));
    }

    /// <summary>Extracts everything into <paramref name="dest"/>. Progress is 0-100.</summary>
    public async Task ExtractAsync(string archive, string dest, IProgress<(double percent, string file)> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(dest);
        if (SevenZip != null)
        {
            var (code, output) = await Run7zAsync(
                $"x {Core.Shell.Quote(archive)} -o{Core.Shell.Quote(dest)} -y -aoa -bsp1 -bso1 -bse1 -sccUTF-8",
                progress, ct);
            // 7-Zip exit codes: 0 ok, 1 warning (some files not extracted), 2 fatal, 7 bad command line, 8 out of memory, 255 cancelled
            if (code != 0) throw new IOException($"7-Zip stopped with exit code {code}:\n" + LastLines(output));
            return;
        }
        await Task.Run(() =>
        {
            using var a = OpenArchive(archive);
            long total = Math.Max(1, a.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size)), done = 0;
            var opts = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
            if (a.IsSolid || a.Type == ArchiveType.SevenZip)
            {
                // solid archives must be read front to back in one pass
                using var reader = a.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    ct.ThrowIfCancellationRequested();
                    if (reader.Entry.IsDirectory) continue;
                    progress.Report((done * 100.0 / total, reader.Entry.Key ?? ""));
                    reader.WriteEntryToDirectory(dest, opts);
                    done += reader.Entry.Size;
                }
            }
            else
            {
                foreach (var entry in a.Entries.Where(e => !e.IsDirectory))
                {
                    ct.ThrowIfCancellationRequested();
                    progress.Report((done * 100.0 / total, entry.Key ?? ""));
                    entry.WriteToDirectory(dest, opts);
                    done += entry.Size;
                }
            }
            progress.Report((100, ""));
        }, ct);
    }

    static string LastLines(string s) => string.Join("\n", s.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(8));

    async Task<(int code, string output)> Run7zAsync(string args, IProgress<(double, string)>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(SevenZip!, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true, // closed right away, so a password prompt fails instead of hanging
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi)!;
        p.StandardInput.Close();
        var all = new StringBuilder();
        var token = new StringBuilder();
        var buf = new char[4096];
        using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
        int n;
        while ((n = await p.StandardOutput.ReadAsync(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                char c = buf[i];
                if (c is '\b' or '\r' or '\n')
                {
                    if (token.Length > 0)
                    {
                        var t = token.ToString();
                        token.Clear();
                        if (progress != null)
                        {
                            var m = Regex.Match(t, @"^\s*(\d+)%(?:\s+\d+)?(?:\s+-\s+(.*))?");
                            if (m.Success) { progress.Report((int.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim())); continue; }
                        }
                        all.AppendLine(t);
                    }
                }
                else token.Append(c);
            }
        }
        if (token.Length > 0) all.AppendLine(token.ToString());
        await p.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();
        return (p.ExitCode, all.ToString());
    }
}
