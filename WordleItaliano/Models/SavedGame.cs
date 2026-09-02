namespace WordleItaliano.Models;

public sealed class SavedGame
{
    public string GameDate { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public int WordLength { get; set; } = 5;
    public List<string> Guesses { get; set; } = [];
    public GameStatus Status { get; set; } = GameStatus.Playing;
    public int DailyElapsedSeconds { get; set; }
    public bool DailyTimerStarted { get; set; }
    public BonusGame Bonus { get; set; } = new();
    public InfiniteGame Infinite { get; set; } = new();
}
