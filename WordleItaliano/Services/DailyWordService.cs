using WordleItaliano.Models;

namespace WordleItaliano.Services;

public sealed class DailyWordService
{
    private readonly WordRepository _repository;
    private readonly AppSettings _settings;

    public DailyWordService(WordRepository repository, AppSettings settings)
    {
        _repository = repository;
        _settings = settings;
    }

    public string TodayKey => DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

    public static string FormatDateKey(DateOnly date) => date.ToString("yyyy-MM-dd");

    public string GetTodayWord()
    {
        return GetWordForDate(DateOnly.FromDateTime(DateTime.Today));
    }

    public string GetWordForDate(DateOnly date)
    {
        var count = _repository.DailyWords.Count;
        var index = StableDateSeed(date, 0x5a17cafe) % count;
        return _repository.DailyWords[index];
    }

    public (string Word, int Length) GetTodayBonusWord()
    {
        return GetBonusWordForDate(DateOnly.FromDateTime(DateTime.Today));
    }

    public (string Word, int Length) GetBonusWordForDate(DateOnly date)
    {
        var seed = StableDateSeed(date, 0x51f15e);
        var length = 5 + seed % 3;
        var words = _repository.GetBonusWords(length);
        var index = StableDateSeed(date, 0xb07115) % words.Count;
        return (words[index], length);
    }

    private static int StableDateSeed(DateOnly date, int salt)
    {
        unchecked
        {
            var value = 2166136261u;
            foreach (var c in date.ToString("yyyy-MM-dd"))
            {
                value ^= c;
                value *= 16777619u;
            }

            value ^= (uint)salt;
            value *= 16777619u;
            return (int)(value & 0x7fffffff);
        }
    }
}
