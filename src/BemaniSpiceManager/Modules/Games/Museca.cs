namespace BemaniSpiceManager.Modules.Games;

public sealed class Museca : GameModule
{
    public override string Id => "museca";
    public override string Name => "MÚSECA";
    public override string SpiceGameName => "Museca";
    public override string[] Models => new[] { "PIX" };
    public override string[] Dlls => new[] { "museca.dll" };
}
