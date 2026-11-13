using System.IO;
using BemaniSpiceManager.Modules.Tools;

namespace BemaniSpiceManager.Core;

public sealed class NotEnoughSpaceException(string message) : IOException(message);

public sealed record ImportResult(string Folder, List<GameEntry> Games, List<string> ArchiveFiles);

/// <summary>Archive → library folder → detected games.</summary>
public static class Importer
{
    /// <summary>Extra room kept free on top of the archive's unpacked size.</summary>
    public const long Headroom = 2L * 1024 * 1024 * 1024;

    public static async Task<ImportResult> ImportAsync(string archive, AppState state,
        IProgress<(double percent, string text)> progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state.LibraryPath))
            throw new IOException("Choose a library folder in the Settings tab first.");
        Directory.CreateDirectory(state.LibraryPath);

        archive = ArchiveExtractor.FirstVolume(archive);
        var volumes = ArchiveExtractor.AllVolumes(archive);
        var ex = new ArchiveExtractor(state.SevenZipPath, ArchiveExtractor.ForceBuiltIn);

        progress.Report((0, $"Reading {Path.GetFileName(archive)}…"));
        var size = await ex.GetUncompressedSizeAsync(archive, ct);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(state.LibraryPath))!);
        var problem = CheckSpace(size, drive.AvailableFreeSpace, drive.Name);
        if (problem != null) throw new NotEnoughSpaceException(problem);

        var name = CleanName(archive);
        var dest = Unique(Path.Combine(state.LibraryPath, name));
        var temp = Path.Combine(state.LibraryPath, ".importing-" + Path.GetFileName(dest));
        if (Directory.Exists(temp)) Directory.Delete(temp, true);

        try
        {
            await ex.ExtractAsync(archive, temp, new Progress<(double p, string f)>(x =>
                progress.Report((x.p, $"Extracting with {ex.EngineName}: {x.p:0}%  {x.f}"))), ct);
        }
        catch
        {
            progress.Report((0, "Removing the partly extracted files…"));
            try { Directory.Delete(temp, true); } catch { }
            throw;
        }

        // archives often wrap everything in one folder: lift it up so the library stays flat
        var inner = Directory.GetFileSystemEntries(temp);
        if (inner.Length == 1 && Directory.Exists(inner[0]))
        {
            dest = Unique(Path.Combine(state.LibraryPath, Path.GetFileName(inner[0])));
            Directory.Move(inner[0], dest);
        }
        else
            Directory.Move(temp, dest);
        if (Directory.Exists(temp)) Directory.Delete(temp, true);

        progress.Report((100, "Looking for games…"));
        var games = GameDetector.Detect(dest);
        foreach (var g in games) g.SourceArchive = archive;
        return new ImportResult(dest, games, volumes);
    }

    /// <summary>Null if the archive fits, otherwise the message to show.</summary>
    public static string? CheckSpace(long unpackedSize, long freeSpace, string driveName) =>
        freeSpace >= unpackedSize + Headroom ? null :
            $"Not enough free space on {driveName}.\n\n" +
            $"Unpacked size: {Shell.FormatSize(unpackedSize)}\nFree space: {Shell.FormatSize(freeSpace)}\n" +
            $"Needed (with {Shell.FormatSize(Headroom)} spare): {Shell.FormatSize(unpackedSize + Headroom)}\n\n" +
            "Free up space or choose a library folder on another drive in Settings. Nothing was extracted.";

    static string CleanName(string archive)
    {
        var n = Path.GetFileName(archive);
        foreach (var suffix in new[] { ".001", ".rar", ".7z", ".zip" })
            if (n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) n = n[..^suffix.Length];
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\.part\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\.(7z|zip)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Trim().TrimEnd('.');
    }

    static string Unique(string path)
    {
        if (!Directory.Exists(path)) return path;
        for (int i = 2; ; i++) if (!Directory.Exists($"{path} ({i})")) return $"{path} ({i})";
    }
}
