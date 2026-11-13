namespace BemaniSpiceManager.Modules.Games;

public sealed class Iidx : GameModule
{
    public override string Id => "iidx";
    public override string Name => "beatmania IIDX";
    public override string SpiceGameName => "Beatmania IIDX";
    public override string[] Models => new[] { "LDJ", "KDZ", "JDZ" };
    public override string[] Dlls => new[] { "bm2dx.dll" };
}
