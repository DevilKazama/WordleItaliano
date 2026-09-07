using System.IO;
using System.Text.Json;
using WordleItaliano.Models;

namespace WordleItaliano.Services;

public sealed class ChangelogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public ChangelogService()
    {
        _path = Path.Combine(AppContext.BaseDirectory, "Data", "changelog.json");
    }

    public ChangelogEntry? GetEntry(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || !File.Exists(_path))
        {
            return null;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<ChangelogEntry>>(File.ReadAllText(_path), JsonOptions);
            return entries?.FirstOrDefault(entry => entry.Version == version);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<ChangelogEntry> GetLatestEntries(int maxEntries = 8)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<ChangelogEntry>>(File.ReadAllText(_path), JsonOptions) ?? [];
            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Version))
                .Take(maxEntries)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
