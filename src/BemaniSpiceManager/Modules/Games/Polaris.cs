namespace BemaniSpiceManager.Modules.Games;

public sealed class Polaris : GameModule
{
    public override string Id => "pc";
    public override string Name => "Polaris Chord";
    public override string SpiceGameName => "Polaris Chord";
    public override string[] Dlls => new[] { "kamunity.dll" };
    public override string? Match(GameFolder f) => f.HasDll("kamunity.dll") && f.HasFile(@"game\svm.exe") ? "kamunity.dll" : null;
}
