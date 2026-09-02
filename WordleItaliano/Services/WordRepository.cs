using System.IO;
using System.Text.Json;

namespace WordleItaliano.Services;

public sealed class WordRepository
{
    private readonly Dictionary<int, HashSet<string>> _validWordsByLength = [];

    public WordRepository()
    {
        ValidWords = LoadWords(5, "Data", "validWords.json");
        DailyWords = LoadWords(5, "Data", "dailyWords.json");
        BonusWordsByLength = new Dictionary<int, IReadOnlyList<string>>
        {
            [5] = LoadWords(5, "Data", "bonusWords5.json"),
            [6] = LoadWords(6, "Data", "bonusWords6.json"),
            [7] = LoadWords(7, "Data", "bonusWords7.json")
        };

        _validWordsByLength[5] = ValidWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _validWordsByLength[6] = LoadWords(6, "Data", "validWords6.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
        _validWordsByLength[7] = LoadWords(7, "Data", "validWords7.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> ValidWords { get; }
    public IReadOnlyList<string> DailyWords { get; }
    public IReadOnlyDictionary<int, IReadOnlyList<string>> BonusWordsByLength { get; }

    public bool IsValid(string word) => _validWordsByLength.TryGetValue(word.Length, out var words) && words.Contains(word);

    public IReadOnlyList<string> GetBonusWords(int length) => BonusWordsByLength[length];

    private static IReadOnlyList<string> LoadWords(int length, params string[] parts)
    {
        var path = Path.Combine(AppContext.BaseDirectory, Path.Combine(parts));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File parole non trovato: {path}");
        }

        var words = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
        return words
            .Select(Normalize)
            .Where(word => word.Length == length && word.All(char.IsLetter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string Normalize(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetter)
            .ToArray();
        return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
    }
}
