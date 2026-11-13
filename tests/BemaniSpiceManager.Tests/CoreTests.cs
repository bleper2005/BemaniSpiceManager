using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using BemaniSpiceManager.Core;
using BemaniSpiceManager.Modules;
using BemaniSpiceManager.Modules.Servers;
using BemaniSpiceManager.Modules.Tools;

namespace BemaniSpiceManager.Tests;

/// <summary>Builds a tiny fake pop'n folder (no game data) in a temp directory.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bsm-test-" + Guid.NewGuid().ToString("N")[..8]);
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }

    /// <summary>A fake game: modules\popn.dll is a copy of a 64-bit system DLL so the PE check works.</summary>
    public string MakeFakePopn(string name = "M39-2026041500")
    {
        var root = System.IO.Path.Combine(Path, "src", name);
        var modules = System.IO.Path.Combine(root, "modules");
        Directory.CreateDirectory(modules);
        File.Copy(System.IO.Path.Combine(Environment.SystemDirectory, "version.dll"), System.IO.Path.Combine(modules, "popn.dll"));
        File.WriteAllText(System.IO.Path.Combine(modules, "libaio-iob2_video.dll"), "x");
        Directory.CreateDirectory(System.IO.Path.Combine(root, "data"));
        File.WriteAllBytes(System.IO.Path.Combine(root, "data", "blob.bin"), System.Security.Cryptography.RandomNumberGenerator.GetBytes(200_000));
        return root;
    }
}

public class DetectionTests
{
    [Fact]
    public void FakePopnFolder_IsDetectedFromFolderName()
    {
        using var t = new TempDir();
        var root = t.MakeFakePopn();
        var g = GameDetector.TryDetect(root);
        Assert.NotNull(g);
        Assert.Equal("popn", g!.ModuleId);
        Assert.Equal("M39", g.Model);
        Assert.Equal("2026041500", g.DateCode);
        Assert.True(g.Is64Bit);
        Assert.Equal("pop'n music High☆Cheers!!", g.Name);
    }

    [Fact]
    public void Ea3Config_WinsOverFolderName()
    {
        using var t = new TempDir();
        var root = t.MakeFakePopn("whatever");
        Directory.CreateDirectory(Path.Combine(root, "prop"));
        File.WriteAllText(Path.Combine(root, "prop", "ea3-config.xml"),
            "<?xml version=\"1.0\"?><ea3><soft><model __type=\"str\">M39</model><dest>J</dest><spec>A</spec><rev>A</rev><ext>2024091000</ext></soft></ea3>");
        var g = GameDetector.TryDetect(root)!;
        Assert.Equal("2024091000", g.DateCode);
        Assert.Equal("J", g.Dest);
        Assert.Equal("pop'n music Jam&Fizz", g.Name);
    }

    [Fact]
    public void PopnWithoutVideoIo_IsHelloPopn()
    {
        using var t = new TempDir();
        var root = t.MakeFakePopn();
        File.Delete(Path.Combine(root, "modules", "libaio-iob2_video.dll"));
        Assert.Equal("hpm", GameDetector.TryDetect(root)!.ModuleId);
    }

    [Fact]
    public void RealPopnFolder_IfPresent()
    {
        const string real = @"C:\PopnArchive\M39-2026041500";
        if (!Directory.Exists(real)) return;
        var g = GameDetector.Detect(@"C:\PopnArchive").Single(x => x.RootPath == real);
        Assert.Equal("M39", g.Model);
        Assert.Equal("2026041500", g.DateCode);
        Assert.True(g.Is64Bit);
    }

    [Fact]
    public void AllModulesDiscovered()
    {
        Assert.Contains(ModuleRegistry.Games, g => g.Id == "popn");
        Assert.Contains(ModuleRegistry.Games, g => g.Id == "iidx");
        Assert.Contains(ModuleRegistry.Servers, s => s.Id == "asphyxia");
        Assert.Contains(ModuleRegistry.Servers, s => s.Id == "bemaniutils");
    }
}

public class ImportTests
{
    static async Task<ImportResult> Import(string archive, string library)
    {
        var state = new AppState { LibraryPath = library };
        return await Importer.ImportAsync(archive, state, new Progress<(double, string)>(_ => { }), CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zip_ExtractsAndDetects(bool builtIn)
    {
        using var t = new TempDir();
        var root = t.MakeFakePopn();
        var zip = Path.Combine(t.Path, "popn game.zip");
        ZipFile.CreateFromDirectory(Path.GetDirectoryName(root)!, zip);
        ArchiveExtractor.ForceBuiltIn = builtIn;
        try
        {
            var r = await Import(zip, Path.Combine(t.Path, "lib"));
            var g = Assert.Single(r.Games);
            Assert.Equal("2026041500", g.DateCode);
            // the single wrapping folder is lifted: lib\M39-2026041500\modules
            Assert.Equal(Path.Combine(t.Path, "lib", "M39-2026041500"), g.RootPath);
            Assert.True(File.Exists(Path.Combine(g.RootPath, "data", "blob.bin")));
            Assert.Empty(Directory.GetDirectories(Path.Combine(t.Path, "lib"), ".importing-*"));
            Assert.Equal(new[] { zip }, r.ArchiveFiles);
        }
        finally { ArchiveExtractor.ForceBuiltIn = false; }
    }

    [Fact]
    public async Task SevenZip_MultiVolume()
    {
        var sevenZip = new ArchiveExtractor(null).SevenZip;
        if (sevenZip == null) return; // 7-Zip not installed on this machine
        using var t = new TempDir();
        var root = t.MakeFakePopn();
        var arc = Path.Combine(t.Path, "set.7z");
        var p = Process.Start(new ProcessStartInfo(sevenZip, $"a \"{arc}\" \"{root}\" -v60k -mx1") { CreateNoWindow = true, UseShellExecute = false })!;
        p.WaitForExit();
        Assert.True(File.Exists(arc + ".002"));
        // dropping the second part still imports the whole set
        var r = await Import(arc + ".002", Path.Combine(t.Path, "lib"));
        Assert.Single(r.Games);
        Assert.True(r.ArchiveFiles.Count >= 2);
    }

    [Fact]
    public void SpaceCheck()
    {
        const long GB = 1L << 30;
        Assert.Null(Importer.CheckSpace(40 * GB, 100 * GB, "D:\\"));
        Assert.NotNull(Importer.CheckSpace(40 * GB, 41 * GB, "D:\\")); // within the spare headroom
        Assert.NotNull(Importer.CheckSpace(40 * GB, 10 * GB, "D:\\"));
    }

    [Fact]
    public async Task ArchiveWithoutGame_IsKeptButAddsNothing()
    {
        using var t = new TempDir();
        var zip = Path.Combine(t.Path, "notes.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            using (var w = new StreamWriter(z.CreateEntry("readme.txt").Open())) w.Write("hi");
        var r = await Import(zip, Path.Combine(t.Path, "lib"));
        Assert.Empty(r.Games);
        Assert.True(File.Exists(Path.Combine(r.Folder, "readme.txt")));
    }

    [Fact]
    public void Volumes_AreGrouped()
    {
        using var t = new TempDir();
        foreach (var n in new[] { "g.part1.rar", "g.part2.rar", "g.part3.rar" }) File.WriteAllText(Path.Combine(t.Path, n), "");
        Assert.Equal(Path.Combine(t.Path, "g.part1.rar"), ArchiveExtractor.FirstVolume(Path.Combine(t.Path, "g.part3.rar")));
        Assert.Equal(3, ArchiveExtractor.AllVolumes(Path.Combine(t.Path, "g.part1.rar")).Count);
    }
}

public class LaunchTests
{
    [Fact]
    public void Args_UseServerPcbidCardAndOptions()
    {
        var state = new AppState { AsphyxiaFolder = @"C:\nowhere" };
        foreach (var s in ModuleRegistry.Servers) s.Attach(state);
        var g = new GameEntry
        {
            ServerId = "asphyxia", Pcbid = "012010000004EF09FC10", Card = "E00467D170D97EAF",
            SpiceOptions = new() { ["w"] = "1", ["ea"] = "1", ["notarealoption"] = "x" },
        };
        var args = GameLauncher.BuildArgs(g, state);
        Assert.Equal(new[] { "-url", "http://localhost:8083", "-p", "012010000004EF09FC10", "-card0", "E00467D170D97EAF", "-w" }, args);
        Assert.DoesNotContain("-ea", args); // never mixed with a real server
    }

    [Fact]
    public void Options_ParsedFromSpiceSource()
    {
        Assert.True(Spice2x.Options.Count > 250);
        Assert.Contains(Spice2x.Options, o => o.Name == "w" && o.Type == "Bool");
        Assert.DoesNotContain(Spice2x.Options, o => o.Name == "url");
        Assert.Contains(Spice2x.OptionsFor("Pop'n Music"), o => o.Games.Contains("Pop'n Music"));
        Assert.DoesNotContain(Spice2x.OptionsFor("Pop'n Music"), o => o.Games.Count > 0 && !o.Games.Contains("Pop'n Music"));
    }

    [Fact]
    public void Ids_HaveRightShape()
    {
        Assert.Matches("^E004[0-9A-F]{12}$", Ids.NewCard());
        Assert.Matches("^[0-9A-F]{20}$", Ids.NewPcbid());
    }
}

public class ShortcutTests
{
    [Fact]
    public void GameShortcut_LaunchesThroughApp()
    {
        using var t = new TempDir();
        var g = new GameEntry { Name = "pop'n music High☆Cheers!!" };
        var lnk = Shortcuts.CreateForGame(g, t.Path, null);
        Assert.True(File.Exists(lnk));
        var type = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(type)!;
        var plain = Path.Combine(t.Path, "read.lnk"); // WScript can't open the ☆ name either
        File.Copy(lnk, plain);
        dynamic s = shell.CreateShortcut(plain);
        Assert.Equal($"--launch {g.Id}", (string)s.Arguments);
        Assert.EndsWith(".exe", (string)s.TargetPath, StringComparison.OrdinalIgnoreCase);
    }
}

public class ConfigTests
{
    [Fact]
    public void Ini_KeepsCommentsAndAddsSections()
    {
        using var t = new TempDir();
        var p = Path.Combine(t.Path, "config.ini");
        File.WriteAllText(p, "; comment\nport=8083\nbind=localhost\n\n[popn@asphyxia]\nenable_force_unlock=true\n");
        var ini = new IniFile(p);
        ini.Set("", "port", "9000");
        ini.Set("popn@asphyxia", "enable_force_unlock", "false");
        ini.Set("popn@asphyxia", "new_key", "1");
        ini.Set("sdvx@asphyxia", "x", "y");
        ini.Set("", "added", "z");
        ini.Save();
        var again = new IniFile(p);
        Assert.Equal("9000", again.Get("", "port"));
        Assert.Equal("false", again.Get("popn@asphyxia", "enable_force_unlock"));
        Assert.Equal("1", again.Get("popn@asphyxia", "new_key"));
        Assert.Equal("y", again.Get("sdvx@asphyxia", "x"));
        Assert.Equal("z", again.Get("", "added"));
        Assert.StartsWith("; comment", File.ReadAllText(p));
    }

    [Fact]
    public void Yaml_EditsScalarsInPlace()
    {
        using var t = new TempDir();
        var p = Path.Combine(t.Path, "server.yaml");
        File.WriteAllText(p, "# top\ndatabase:\n    # host\n    address: \"localhost\"\n    read_only: False\nserver:\n    port: 80\n    address: \"192.168.0.1\"\nwebhooks:\n    discord:\n        iidx:\n        - \"https://x\"\n");
        var y = new YamlLines(p);
        var sc = y.Scalars();
        Assert.Contains(sc, s => s.Key == "database.address" && s.Value == "localhost" && s.Comment == "host");
        Assert.Contains(sc, s => s.Key == "database.read_only" && s.IsBool);
        y.Set(sc.First(s => s.Key == "server.address"), "127.0.0.1");
        y.Set(sc.First(s => s.Key == "server.port"), "8084");
        y.Save();
        var text = File.ReadAllText(p);
        Assert.Contains("    address: \"127.0.0.1\"", text);
        Assert.Contains("    port: 8084", text);
        Assert.Contains("# top", text);
        Assert.Contains("- \"https://x\"", text);
    }
}

public class AsphyxiaTests
{
    [Fact]
    public void Plugins_SettingsAndGameCodesAreRead()
    {
        const string real = @"C:\PopnArchive\asphyxia";
        if (!Directory.Exists(real)) return;
        var a = new Asphyxia();
        a.Attach(new AppState { AsphyxiaFolder = real });
        var popn = a.Plugins().Single(p => p.Id == "popn@asphyxia");
        Assert.Contains("M39", popn.GameCodes);
        var unlock = popn.Settings.Single(s => s.Key == "enable_force_unlock");
        Assert.Equal("boolean", unlock.Type);
        Assert.Equal("Unlock all songs", unlock.Name);
        Assert.Null(a.NoteFor(new GameEntry { Model = "M39" }));
        Assert.NotNull(a.NoteFor(new GameEntry { Model = "ZZZ" }));
    }

    /// <summary>Starts and stops a private copy on another port (never the user's own instance).</summary>
    [Fact]
    public async Task StartStop_OnCopy()
    {
        const string real = @"C:\PopnArchive\asphyxia";
        if (!File.Exists(Path.Combine(real, "asphyxia-core-x64.exe"))) return;
        using var t = new TempDir();
        var copy = Path.Combine(t.Path, "asphyxia");
        Directory.CreateDirectory(copy);
        File.Copy(Path.Combine(real, "asphyxia-core-x64.exe"), Path.Combine(copy, "asphyxia-core-x64.exe"));
        File.WriteAllText(Path.Combine(copy, "config.ini"), "port=8093\nbind=localhost\nwebui_on_startup=false\n");
        var a = new Asphyxia();
        a.Attach(new AppState { AsphyxiaFolder = copy });
        string? err = null;
        try
        {
            Assert.True(await a.EnsureRunningAsync(m => err = m), err);
            Assert.True(a.IsRunning);
            Assert.Equal("http://localhost:8093", a.ServiceUrl);
            using (var http = new HttpClient())
                Assert.NotNull(await http.GetAsync("http://localhost:8093/")); // it answers HTTP
        }
        finally { a.Stop(); }
        Assert.False(a.IsRunning);
    }
}
