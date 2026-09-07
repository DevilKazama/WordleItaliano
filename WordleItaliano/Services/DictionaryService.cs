using System.IO;
using System.Text.Json;
using WordleItaliano.Models;

namespace WordleItaliano.Services;

public sealed class DictionaryService
{
    private readonly string _path;
    private IReadOnlyDictionary<string, DictionaryEntry>? _entries;

    public DictionaryService()
    {
        _path = Path.Combine(AppContext.BaseDirectory, "Data", "definitions.json");
    }

    public DictionaryEntry? Find(string word)
    {
        var normalized = WordRepository.Normalize(word);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return LoadEntries().TryGetValue(normalized, out var entry)
            ? entry
            : null;
    }

    private IReadOnlyDictionary<string, DictionaryEntry> LoadEntries()
    {
        if (_entries is not null)
        {
            return _entries;
        }

        if (!File.Exists(_path))
        {
            _entries = new Dictionary<string, DictionaryEntry>(StringComparer.OrdinalIgnoreCase);
            return _entries;
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _entries = JsonSerializer.Deserialize<Dictionary<string, DictionaryEntry>>(
                    File.ReadAllText(_path),
                    options)
                ?? new Dictionary<string, DictionaryEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _entries = new Dictionary<string, DictionaryEntry>(StringComparer.OrdinalIgnoreCase);
        }

        return _entries;
    }
}
