namespace WordleItaliano.Models;

public sealed class GameHistoryEntry
{
    public string Date { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public bool IsBonus { get; set; }
    public bool IsInfinite { get; set; }
    public int WordLength { get; set; } = 5;
    public bool Won { get; set; }
    public int Attempts { get; set; }
    public int Points { get; set; }
    public int? ScoreEarned { get; set; }
    public int? DurationSeconds { get; set; }
    public List<string> Guesses { get; set; } = [];
}
