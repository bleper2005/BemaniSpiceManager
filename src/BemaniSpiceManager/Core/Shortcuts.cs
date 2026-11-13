using System.IO;

namespace BemaniSpiceManager.Core;

/// <summary>Creates Windows .lnk shortcuts for the app and for launching single games.</summary>
public static partial class Shortcuts
{
    [System.Runtime.InteropServices.LibraryImport("shell32.dll", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    private static partial void SHChangeNotify(int eventId, uint flags, string item1, IntPtr item2);

    public static string AppExe => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "BemaniSpiceManager.exe");

    public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string StartMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Bemani Spice Manager");

    /// <summary>Shortcut that opens the app.</summary>
    public static string CreateForApp(string folder) =>
        Create(Path.Combine(folder, "Bemani Spice Manager.lnk"), AppExe, "", Path.GetDirectoryName(AppExe)!,
            "Bemani Spice Manager", AppExe);

    /// <summary>Shortcut that starts the game's server and the game directly.</summary>
    public static string CreateForGame(GameEntry g, string folder, string? spiceExe)
    {
        var icon = spiceExe != null && File.Exists(spiceExe) ? spiceExe : AppExe;
        return Create(Path.Combine(folder, SafeName(g.Name) + ".lnk"), AppExe, $"--launch {g.Id}",
            Path.GetDirectoryName(AppExe)!, $"Launch {g.Name} with Bemani Spice Manager", icon);
    }

    static string SafeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }

    static string Create(string lnk, string target, string args, string workDir, string desc, string icon)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnk)!);
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Windows Script Host isn't available.");
        dynamic shell = Activator.CreateInstance(type)!;
        // WScript.Shell can't save names with characters like ☆, so save under a plain name and rename
        var temp = Path.Combine(Path.GetDirectoryName(lnk)!, "bsm-" + Guid.NewGuid().ToString("N")[..8] + ".lnk");
        try
        {
            dynamic s = shell.CreateShortcut(temp);
            s.TargetPath = target;
            s.Arguments = args;
            s.WorkingDirectory = workDir;
            s.Description = desc;
            s.IconLocation = icon + ",0";
            s.Save();
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(s);
            File.Move(temp, lnk, true);
            // Explorer may miss the rename, so announce the new file or it won't appear until a refresh
            SHChangeNotify(0x2 /* SHCNE_CREATE */, 0x5 /* SHCNF_PATHW */, lnk, IntPtr.Zero);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            if (File.Exists(temp)) File.Delete(temp);
        }
        return lnk;
    }
}
