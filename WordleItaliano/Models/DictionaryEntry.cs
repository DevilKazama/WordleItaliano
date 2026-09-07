namespace WordleItaliano.Models;

public sealed class DictionaryEntry
{
    public string Word { get; set; } = string.Empty;
    public string DisplayWord { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public List<string> Definitions { get; set; } = [];
    public string Example { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
