namespace WordleItaliano.Theme;

public static class ThemeModes
{
    public const string IbpDark = "IbpDark";
    public const string IbpLight = "IbpLight";
    public const string Original = "Original";

    public static string Normalize(string? theme) => theme switch
    {
        IbpLight => IbpLight,
        Original => Original,
        _ => IbpDark
    };
}
