using System.Text.Json.Serialization;

namespace WordleItaliano.Models;

public sealed class BonusGame
{
    public bool IsUnlocked { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Solution { get; set; }
    public int WordLength { get; set; } = 5;
    public List<string> Guesses { get; set; } = [];
    public GameStatus Status { get; set; } = GameStatus.Playing;
    public int ElapsedSeconds { get; set; }
    public bool TimerStarted { get; set; }
    public PerfectShotState PerfectShot { get; set; } = new();
}
