namespace WordleItaliano.Models;

public sealed class InfiniteGame
{
    public string Solution { get; set; } = string.Empty;
    public List<string> Guesses { get; set; } = [];
    public GameStatus Status { get; set; } = GameStatus.Playing;
    public int ElapsedSeconds { get; set; }
    public bool TimerStarted { get; set; }
}
