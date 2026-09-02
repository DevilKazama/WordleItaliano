namespace WordleItaliano.Models;

public sealed class AppSettings
{
    public DateOnly BaseDate { get; set; } = new(2026, 1, 1);
    public string UpdateRepositoryUrl { get; set; } = "https://github.com/DevilKazama/WordleItaliano";
    public bool EnableAutomaticUpdateChecks { get; set; } = true;
}
