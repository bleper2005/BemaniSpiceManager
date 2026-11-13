namespace BemaniSpiceManager.Modules.Games;

/// <summary>Same popn.dll as pop'n music, but without the cabinet's video I/O DLL.</summary>
public sealed class HelloPopn : GameModule
{
    public override string Id => "hpm";
    public override string Name => "HELLO! Pop'n Music";
    public override string SpiceGameName => "HELLO! Pop'n Music";
    public override string[] Dlls => new[] { "popn.dll" };
    public override int Priority => -1; // only when the pop'n cabinet check fails
}
