namespace BemaniSpiceManager.Modules.Games;

public sealed class DanceEvolution : GameModule
{
    public override string Id => "dea";
    public override string Name => "DanceEvolution";
    public override string SpiceGameName => "Dance Evolution";
    public override string[] Models => new[] { "KDM" };
    public override string[] Dlls => new[] { "arkkdm.dll" };
}
