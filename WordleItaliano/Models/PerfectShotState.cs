namespace WordleItaliano.Models;

public sealed class PerfectShotState
{
    public bool IsPerfectShot { get; set; }
    public bool PrizePromptShown { get; set; }
    public string PrizeText { get; set; } = string.Empty;
    public bool IsPrizePending { get; set; }

    public PerfectShotState Clone()
    {
        return new PerfectShotState
        {
            IsPerfectShot = IsPerfectShot,
            PrizePromptShown = PrizePromptShown,
            PrizeText = PrizeText,
            IsPrizePending = IsPrizePending
        };
    }
}
