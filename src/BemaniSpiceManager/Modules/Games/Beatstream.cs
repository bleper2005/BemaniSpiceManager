namespace BemaniSpiceManager.Modules.Games;

public sealed class Beatstream : GameModule
{
    public override string Id => "bs";
    public override string Name => "BeatStream";
    public override string SpiceGameName => "Beatstream";
    public override string[] Models => new[] { "NBT" };
    public override string[] Dlls => new[] { "beatstream.dll" };
}
