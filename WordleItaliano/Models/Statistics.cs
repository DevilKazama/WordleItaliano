namespace WordleItaliano.Models;

public sealed class Statistics
{
    public int DataMigrationVersion { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Points { get; set; }
    public int BonusPlayed { get; set; }
    public int BonusWon { get; set; }
    public int TwoPointDays { get; set; }
    public int InfinitePlayed { get; set; }
    public int InfiniteWon { get; set; }
    public int[] InfiniteWinDistribution { get; set; } = new int[6];
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int[] WinDistribution { get; set; } = new int[6];
    public string LastPlayedDate { get; set; } = string.Empty;
    public string LastWinDate { get; set; } = string.Empty;
    public string LastMonthlyRecapShown { get; set; } = string.Empty;
    public List<GameHistoryEntry> History { get; set; } = [];

    public int WinPercentage => Played == 0 ? 0 : (int)Math.Round(Won * 100.0 / Played);
    public int InfiniteWinPercentage => InfinitePlayed == 0 ? 0 : (int)Math.Round(InfiniteWon * 100.0 / InfinitePlayed);
}
