namespace WordleItaliano.Models;

public sealed class BonusGame
{
    public bool IsUnlocked { get; set; }
    public string Solution { get; set; } = string.Empty;
    public int WordLength { get; set; } = 5;
    public List<string> Guesses { get; set; } = [];
    public GameStatus Status { get; set; } = GameStatus.Playing;
}
