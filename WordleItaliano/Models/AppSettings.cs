namespace WordleItaliano.Models;

public sealed class AppSettings
{
    public string ColleagueName { get; set; } = "[NOME COLLEGA]";
    public DateOnly BaseDate { get; set; } = new(2026, 1, 1);
}
