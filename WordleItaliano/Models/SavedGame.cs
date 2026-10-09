using System.Text.Json.Serialization;

namespace WordleItaliano.Models;

public sealed class SavedGame
{
    public string GameDate { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public int FormatVersion { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SequenceId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RecoveryRequired { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecoveryBackup { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Solution { get; set; }
    public int WordLength { get; set; } = 5;
    public List<string> Guesses { get; set; } = [];
    public GameStatus Status { get; set; } = GameStatus.Playing;
    public int DailyElapsedSeconds { get; set; }
    public bool DailyTimerStarted { get; set; }
    public PerfectShotState DailyPerfectShot { get; set; } = new();
    public BonusGame Bonus { get; set; } = new();
    public InfiniteGame Infinite { get; set; } = new();
}
