namespace BemaniSpiceManager.Modules.Games;

public sealed class SoundVoltex : GameModule
{
    public override string Id => "sdvx";
    public override string Name => "SOUND VOLTEX";
    public override string SpiceGameName => "Sound Voltex";
    public override string[] Models => new[] { "KFC" };
    public override string[] Dlls => new[] { "soundvoltex.dll" };
}
