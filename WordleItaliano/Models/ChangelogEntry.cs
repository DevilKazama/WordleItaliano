namespace WordleItaliano.Models;

public sealed class ChangelogEntry
{
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Items { get; set; } = [];
}
