using WordleItaliano.Theme;

namespace WordleItaliano.Models;

public sealed class UserSettings
{
    public List<string> FavoriteWords { get; set; } = [];
    public bool FavoritesPanelOpen { get; set; }
    public string FavoritesPanelSide { get; set; } = "Destra";
    public bool HideFavoritesWithAbsentLetters { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string LastSeenChangelogVersion { get; set; } = string.Empty;
    public string ThemeMode { get; set; } = ThemeModes.IbpDark;
}
