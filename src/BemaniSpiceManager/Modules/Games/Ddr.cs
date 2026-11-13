namespace BemaniSpiceManager.Modules.Games;

public sealed class Ddr : GameModule
{
    public override string Id => "ddr";
    public override string Name => "DanceDanceRevolution";
    public override string SpiceGameName => "Dance Dance Revolution";
    public override string[] Models => new[] { "MDX" };
    public override string[] Dlls => new[] { "arkmdxp4.dll", "arkmdxp3.dll", "arkmdxbio2.dll", "mdxja_945.dll", "mdxja_hm65.dll", "ddr.dll" };
}
