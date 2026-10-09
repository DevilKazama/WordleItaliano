using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WordleItaliano.Services;

public static class OfficialSequence
{
    public const string Legacy = "legacy-v1";
    public const string Next = "frozen-v2";
    public static DateOnly? ActivationDate => new DateOnly(2026, 10, 9);
#if WORDLE_TEST_BUILD
    public static DateOnly? TestActivationDate { get; set; }
#endif
    public static string ForDate(DateOnly date)
    {
        var activation = ActivationDate;
#if WORDLE_TEST_BUILD
        activation = TestActivationDate ?? activation;
#endif
        return activation is { } start && date >= start ? Next : Legacy;
    }

    public static bool IsKnown(string? id) => id is null or Legacy or Next;

    private static readonly Lazy<Dictionary<string, string[]>> Frozen = new(() =>
    {
        var result = new Dictionary<string, string[]>();
        foreach (var name in new[] { "dailyWords", "bonusWords5", "bonusWords6", "bonusWords7" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"FrozenSequence.{name}.json")
                ?? throw new InvalidOperationException("Dizionario della sequenza incorporata mancante.");
            using var buffer = new System.IO.MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            var words = (JsonSerializer.Deserialize<string[]>(bytes) ?? throw new InvalidOperationException())
                .Select(WordRepository.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var expectedHash = name switch
            {
                "dailyWords" or "bonusWords5" => "EB35C1574CD0C7B0743F50261CED05A52E51832CE1532A0E41290FF68DCB055E",
                "bonusWords6" => "C7E729111B87433DF84339EAC5EFAADA5D7626C7ABD42A686BA1C11DDB882104",
                _ => "CF85DEED44E07E1D799D8C7AFD4522FE998CAD9083B14BB25CFC0A76DBEB523B"
            };
            // Hash normalized ordered words, independent of JSON whitespace and checkout line endings.
            if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", words)))) != expectedHash)
                throw new InvalidOperationException("Il dizionario incorporato non corrisponde alla sequenza frozen-v2.");
            result[name] = words;
        }
        return result;
    });

    public static (string Daily, string Bonus, int Length) GetNext(DateOnly date)
    {
        PcAuthorization.RequireOfficial();
        var dictionaries = Frozen.Value;
        var oldDaily = dictionaries["dailyWords"][LegacySeed(date, 0x5a17cafe) % dictionaries["dailyWords"].Length];
        var oldLength = 5 + LegacySeed(date, 0x51f15e) % 3;
        var oldBonusList = dictionaries[$"bonusWords{oldLength}"];
        var oldBonus = oldBonusList[LegacySeed(date, 0xb07115) % oldBonusList.Length];
        string Pick(string name, string kind)
        {
            var candidates = dictionaries[name].Where(w => w != oldDaily && w != oldBonus)
                .OrderBy(w => w, StringComparer.Ordinal).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("Dizionario della nuova sequenza insufficiente.");
            return candidates[Seed(date, kind) % (uint)candidates.Length];
        }
        var length = 5 + (int)(Seed(date, "length") % 3);
        return (Pick("dailyWords", "daily"), Pick($"bonusWords{length}", "bonus"), length);
    }

    private static uint Seed(DateOnly date, string kind) => BinaryPrimitives.ReadUInt32LittleEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes("WordleItaliano|frozen-v2|" +
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + kind)));

    internal static int LegacySeed(DateOnly date, int salt)
    {
        unchecked
        {
            var value = 2166136261u;
            foreach (var c in date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) { value ^= c; value *= 16777619u; }
            value ^= (uint)salt; value *= 16777619u;
            return (int)(value & 0x7fffffff);
        }
    }
}
