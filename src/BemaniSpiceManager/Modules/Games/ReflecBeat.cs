namespace BemaniSpiceManager.Modules.Games;

public sealed class ReflecBeat : GameModule
{
    public override string Id => "rb";
    public override string Name => "REFLEC BEAT";
    public override string SpiceGameName => "Reflec Beat";
    public override string[] Models => new[] { "MBR" };
    public override string[] Dlls => new[] { "reflecbeat.dll" };
}
