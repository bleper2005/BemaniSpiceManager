namespace BemaniSpiceManager.Modules.Games;

public sealed class DanceRush : GameModule
{
    public override string Id => "drs";
    public override string Name => "DANCERUSH STARDOM";
    public override string SpiceGameName => "DANCERUSH";
    public override string[] Models => new[] { "REC" };
    public override string[] Dlls => new[] { "superstep.dll" };
}
