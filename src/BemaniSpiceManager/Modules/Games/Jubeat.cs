namespace BemaniSpiceManager.Modules.Games;

public sealed class Jubeat : GameModule
{
    public override string Id => "jubeat";
    public override string Name => "jubeat";
    public override string SpiceGameName => "Jubeat";
    public override string[] Models => new[] { "L44" };
    public override string[] Dlls => new[] { "jubeat.dll", "jubeat2019.dll" };
}
