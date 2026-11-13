namespace BemaniSpiceManager.Modules.Games;

public sealed class PopnMusic : GameModule
{
    public override string Id => "popn";
    public override string Name => "pop'n music";
    public override string SpiceGameName => "Pop'n Music";
    public override string[] Models => new[] { "M39", "K39", "L39", "J39" };
    public override string[] Dlls => new[] { "popn.dll", "popn22.dll", "popn21.dll", "popn20.dll", "popn19.dll" };

    public override string? Match(GameFolder f)
    {
        if (f.HasDll("popn.dll"))
            // spice2x: popn.dll + libaio-iob2_video.dll is the pop'n cabinet, otherwise HELLO! Pop'n Music
            return f.HasDll("libaio-iob2_video.dll") ? "popn.dll" : null;
        return base.Match(f);
    }

    public override string DisplayName(string model, string dateCode)
    {
        if (model != "M39" || !long.TryParse(dateCode, out var d)) return Name;
        // Same boundaries the Asphyxia popn plugin uses.
        var title = d >= 2025100000 ? "High☆Cheers!!"
            : d >= 2024091000 ? "Jam&Fizz"
            : d >= 2022091300 ? "UniLab"
            : d >= 2021042600 ? "Kaimei riddles"
            : d >= 2018101700 ? "peace"
            : "usaneko";
        return $"pop'n music {title}";
    }
}
