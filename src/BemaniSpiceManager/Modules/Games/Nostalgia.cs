namespace BemaniSpiceManager.Modules.Games;

public sealed class Nostalgia : GameModule
{
    public override string Id => "nostalgia";
    public override string Name => "NOSTALGIA";
    public override string SpiceGameName => "Nostalgia";
    public override string[] Models => new[] { "PAN" };
    public override string[] Dlls => new[] { "nostalgia.dll" };
}
