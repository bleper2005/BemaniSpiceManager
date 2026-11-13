namespace BemaniSpiceManager.Modules.Games;

public sealed class GitaDora : GameModule
{
    public override string Id => "gitadora";
    public override string Name => "GITADORA";
    public override string SpiceGameName => "GitaDora";
    public override string[] Models => new[] { "M32" };
    public override string[] Dlls => new[] { "gdxg.dll" };
}
