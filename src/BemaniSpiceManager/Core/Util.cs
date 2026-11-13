using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace BemaniSpiceManager.Core;

public static class Ids
{
    /// <summary>Random e-amusement style card id: E004 + 12 hex digits.</summary>
    public static string NewCard() => "E004" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6));

    /// <summary>Random 20-digit PCBID in the usual 01201000... form.</summary>
    public static string NewPcbid() => "0120100000" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5));

    public static bool IsHex(string s, int len) => s.Length == len && s.All(Uri.IsHexDigit);
}

public static class Pe
{
    /// <summary>True for a 64-bit PE file, false for 32-bit, null if it can't be read.</summary>
    public static bool? Is64Bit(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            using var r = new BinaryReader(f);
            if (r.ReadUInt16() != 0x5A4D) return null;
            f.Position = 0x3C;
            f.Position = r.ReadInt32();
            if (r.ReadUInt32() != 0x4550) return null;
            return r.ReadUInt16() switch { 0x8664 => true, 0xAA64 => true, 0x14C => false, _ => null };
        }
        catch { return null; }
    }
}

public static partial class Shell
{
    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", EntryPoint = "GetLongPathNameW", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    private static partial uint GetLongPathName(string shortPath, [System.Runtime.InteropServices.Out] char[] longPath, uint size);

    /// <summary>Full path with 8.3 short names (C:\Users\ABCDEF~1) expanded, for comparing paths.</summary>
    public static string LongPath(string path)
    {
        var full = Path.GetFullPath(path);
        var buf = new char[1024];
        var n = GetLongPathName(full, buf, (uint)buf.Length);
        return n > 0 && n < buf.Length ? new string(buf, 0, (int)n) : full;
    }

    public static bool SamePath(string? a, string? b) =>
        a != null && b != null && string.Equals(LongPath(a), LongPath(b), StringComparison.OrdinalIgnoreCase);

    public static void Open(string pathOrUrl)
    {
        try { Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true }); } catch { }
    }

    public static string FormatSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }

    /// <summary>Quote one command-line argument the way the MSVC runtime parses it.</summary>
    public static string Quote(string a)
    {
        if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
        var sb = new System.Text.StringBuilder("\"");
        int bs = 0;
        foreach (var c in a)
        {
            if (c == '\\') { bs++; continue; }
            if (c == '"') { sb.Append('\\', bs * 2 + 1); bs = 0; sb.Append('"'); continue; }
            sb.Append('\\', bs); bs = 0; sb.Append(c);
        }
        sb.Append('\\', bs * 2).Append('"');
        return sb.ToString();
    }
}
