using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using WordleItaliano.Models;
using WordleItaliano.Services;
using WordleItaliano.ViewModels;

internal static class Program
{
    private static readonly WordRepository Repository = new();
    private static readonly DailyWordService Words = new(Repository, new AppSettings());
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "isolated", Guid.NewGuid().ToString("N"));
    private static int checks;

    [STAThread] static int Main()
    {
        try
        {
            var app = new Application(); Velopack.VelopackApp.Build().Run();
            foreach (var legacy in new[] { false, true }) Mixed(legacy, false);
            Mixed(false, true);
            RealCopy();
            app.Shutdown();
            Console.WriteLine($"PASS: {checks} isolated retention checks; no solutions printed."); return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL: " + error.GetType().Name); return 1; }
    }
    private static object? Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static void Set(MainViewModel vm, string field, object value) => typeof(MainViewModel)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, value);
    private static List<string> Guesses(MainViewModel vm, string field) => (List<string>)typeof(MainViewModel)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
    private static void Check(bool ok, string label)
    {
        Console.WriteLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) throw new InvalidOperationException(); checks++;
    }
    private static StorageService Storage(string folder)
    {
        Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", folder); return new StorageService();
    }
    private static SavedGame Game() => new() { GameDate = Words.TodayKey, Date = Words.TodayKey,
        Bonus = new BonusGame { WordLength = Words.GetTodayBonusWord().Length } };
    private static string Competitive(Statistics stats) => JsonSerializer.Serialize(stats.History.Where(e => !e.IsInfinite).OrderBy(e => e.Date).ThenBy(e => e.IsBonus));
    private static string InfiniteCounters(Statistics stats) => JsonSerializer.Serialize(new { stats.InfinitePlayed, stats.InfiniteWon, stats.InfiniteWinDistribution });
    private static string CompetitiveCounters(Statistics stats) => JsonSerializer.Serialize(new { stats.Points, stats.Played, stats.Won, stats.BonusPlayed, stats.BonusWon, stats.TwoPointDays, stats.CurrentStreak, stats.BestStreak, stats.WinDistribution });
    private static string Shares(MainViewModel vm, IEnumerable<GameHistoryEntry> entries) => JsonSerializer.Serialize(entries.Where(e => !e.IsInfinite).OrderBy(e => e.Date).ThenBy(e => e.IsBonus).Select(e => (string)Call(vm, "BuildShareText", e)!));
    private static string Summaries(MainViewModel vm)
    {
        var output = new List<string>();
        foreach (var month in vm.Statistics.History.Where(e => !e.IsInfinite).Select(e => DateOnly.Parse(e.Date)).Select(d => new DateOnly(d.Year, d.Month, 1)).Distinct().OrderBy(d => d))
        {
            Set(vm, "_wrappedPeriod", month); Call(vm, "RefreshWrappedView"); Call(vm, "BuildMonthlyRecap", month);
            output.Add(JsonSerializer.Serialize(new { Wrapped = vm.WrappedStatCards.Select(c => c.Value), Time = vm.WrappedTimeCards.Select(c => c.Value), Recap = vm.MonthlyRecapCards.Select(c => c.Value), Wins = vm.WrappedWinRows.Select(r => r.Wins) }));
            output.Add((string)Call(vm, "BuildMonthlyRecapShareText", month)!);
        }
        return JsonSerializer.Serialize(output);
    }
    private static void Mixed(bool legacy, bool tied)
    {
        var folder = Path.Combine(Root, (legacy ? "legacy" : "current") + (tied ? "-ties" : ""));
        var storage = Storage(folder);
        storage.SaveUserSettings(new UserSettings { PlayerName = "Checks", LastSeenChangelogVersion = "1.5.24" });
        var solution = Repository.ValidWords.First(); var wrong = Repository.ValidWords.First(w => w != solution);
        var stats = new Statistics { DataMigrationVersion = legacy ? 2 : 3, InfinitePlayed = 5000, InfiniteWon = 2500, BestStreak = 1 };
        stats.InfiniteWinDistribution[1] = 2500;
        for (var index = 0; index < 400; index++)
        {
            var date = new DateOnly(2023, 1, 1).AddDays(index * 2).ToString("yyyy-MM-dd");
            foreach (var bonus in new[] { false, true })
                stats.History.Add(new GameHistoryEntry { Date = date, Solution = solution, Guesses = [wrong, solution], Won = true, Attempts = 2,
                    IsBonus = bonus, Points = 25, ScoreEarned = 25, BaseScore = 25, DayBaseScore = bonus ? 50 : 25, DayFinalScore = bonus ? 50 : 25,
                    StreakAtDate = 1, StreakMultiplierPercent = 100, DurationSeconds = 60, PerfectShotPrizeText = index == 0 ? "Premio storico" : "" });
        }
        stats.Played = stats.Won = stats.BonusPlayed = stats.BonusWon = stats.TwoPointDays = 400;
        var prizeEntry = stats.History[0]; prizeEntry.Attempts = 1; prizeEntry.Guesses = [solution]; prizeEntry.IsPerfectShot = true;
        prizeEntry.Points = 30;
        prizeEntry.ScoreEarned = prizeEntry.BaseScore = prizeEntry.DayBaseScore = prizeEntry.DayFinalScore = 30;
        stats.History[1].DayBaseScore = stats.History[1].DayFinalScore = 55;
        stats.WinDistribution[0] = 1; stats.WinDistribution[1] = 399; stats.Points = 20005;
        for (var index = 0; index < 900; index++)
            stats.History.Add(new GameHistoryEntry { Date = DateTime.Today.AddDays(-1).AddSeconds(tied ? 0 : index).ToString("yyyy-MM-dd HH:mm:ss"),
                Solution = solution, Guesses = [wrong, solution], IsInfinite = true, Won = true, Attempts = 2, DurationSeconds = index + 1 });
        storage.SaveGameAndStatistics(Game(), stats); storage.CreateRecoveryBackup();
        var competitive = Competitive(stats); var counters = InfiniteCounters(stats);
        var expectedIds = stats.History.Where(e => e.IsInfinite).Skip(170).Select(e => e.DurationSeconds).ToArray();
        var vm = new MainViewModel(); var loaded = storage.LoadStatistics();
        Check(loaded.History.Count(e => !e.IsInfinite) == 800, "more than 730 competitive results retained on load, including old dates");
        Check(loaded.History.Where(e => e.IsInfinite).Select(e => e.DurationSeconds).SequenceEqual(expectedIds), "only newest 730 Infinite retained, including equal timestamps");
        Check(Competitive(loaded) == competitive, "competitive fields and prizes unchanged by retention/migration");
        Check(InfiniteCounters(loaded) == counters, "cumulative Infinite counters preserved, including legacy migration");
        var baselineCounters = CompetitiveCounters(vm.Statistics); var shares = Shares(vm, vm.Statistics.History); var summaries = Summaries(vm);
        var actual = vm.Statistics;
        actual.History.Insert(0, new GameHistoryEntry { Date = "2022-01-01 00:00:00", Solution = solution, IsInfinite = true, Won = true, Attempts = 2, Guesses = [wrong, solution] });
        Call(vm, "NormalizeStatistics");
        Check(CompetitiveCounters(actual) == baselineCounters && Shares(vm, actual.History) == shares && Summaries(vm) == summaries, "removing only old Infinite leaves points streak shares Wrapped and monthly summaries unchanged");
        vm.PersistActiveGameTime();
        for (var reopen = 0; reopen < 3; reopen++)
        {
            vm = new MainViewModel(); vm.PersistActiveGameTime();
            Check(Competitive(vm.Statistics) == competitive && vm.Statistics.History.Count(e => e.IsInfinite) == 730 && InfiniteCounters(vm.Statistics) == counters, "mixed history retained after save and reopen");
        }
        vm.SetHistoryFilterCommand.Execute("Giornaliere"); Check(vm.HistoryRows.Count == 400, "daily history filter still works");
        vm.SetHistoryFilterCommand.Execute("Bonus"); Check(vm.HistoryRows.Count == 400, "bonus history filter still works");
        vm.SetHistoryFilterCommand.Execute("Infinite"); Check(vm.HistoryRows.Count == 730, "Infinite history filter still works");
        Set(vm, "_infiniteSolution", solution); Set(vm, "_infiniteStatus", GameStatus.Won);
        Guesses(vm, "_infiniteGuesses").AddRange(new[] { wrong, solution });
        Call(vm, "RecordInfinite", true, 2);
        Check(vm.Statistics.InfinitePlayed == 5001 && vm.Statistics.InfiniteWon == 2501 && vm.Statistics.InfiniteWinDistribution[1] == 2501, "real Infinite registration increments cumulative counters once");
        Check(Competitive(vm.Statistics) == competitive && vm.Statistics.History.Count(e => e.IsInfinite) == 730 && Shares(vm, vm.Statistics.History) == shares && Summaries(vm) == summaries, "real Infinite registration cannot displace competitive results or change summaries");
        Guesses(vm, "_infiniteGuesses").Clear(); Guesses(vm, "_infiniteGuesses").AddRange(Enumerable.Repeat(wrong, 6));
        Set(vm, "_infiniteStatus", GameStatus.Lost); Call(vm, "RecordInfinite", false, 0);
        Check(vm.Statistics.InfinitePlayed == 5002 && vm.Statistics.InfiniteWon == 2501 && vm.Statistics.InfiniteWinDistribution[1] == 2501 && vm.Statistics.History.Count(e => e.IsInfinite) == 730, "lost Infinite increments only played count while preserving retention limit");
        Check(Competitive(vm.Statistics) == competitive && CompetitiveCounters(vm.Statistics) == baselineCounters, "lost Infinite cannot change competitive points streak or prizes");
        Guesses(vm, "_dailyGuesses").AddRange(new[] { Repository.ValidWords.First(w => w != Words.GetTodayWord()), Words.GetTodayWord() });
        Set(vm, "_dailyStatus", GameStatus.Won); Set(vm, "_isBonusUnlocked", true); Call(vm, "RecordDaily", true, 2);
        var bonusWord = Words.GetTodayBonusWord().Word;
        Guesses(vm, "_bonusGuesses").AddRange(new[] { Repository.GetBonusWords(bonusWord.Length).First(w => w != bonusWord), bonusWord });
        Set(vm, "_bonusStatus", GameStatus.Won); Call(vm, "RecordBonus", true, 2);
        var afterRegistration = CompetitiveCounters(vm.Statistics); var afterHistory = Competitive(vm.Statistics);
        Check(vm.Statistics.History.Select(e => e.Date).SequenceEqual(vm.Statistics.History.OrderBy(e => e.Date).Select(e => e.Date)), "registration preserves chronological archive ordering");
        for (var reopen = 0; reopen < 3; reopen++)
        {
            vm = new MainViewModel(); vm.PersistActiveGameTime();
            Check(CompetitiveCounters(vm.Statistics) == afterRegistration && Competitive(vm.Statistics) == afterHistory && vm.Statistics.History.Count(e => !e.IsInfinite) == 802, "daily and bonus registration plus reopen do not duplicate or truncate competitive history");
        }
    }
    private static void RealCopy()
    {
        var source = @"C:\Users\Magazzino3\AppData\Local\WordleItaliano"; var folder = Path.Combine(Root, "real-copy"); Directory.CreateDirectory(folder);
        var originals = new[] { "game.json", "statistics.json", "userSettings.json" }.ToDictionary(f => f, f => File.ReadAllBytes(Path.Combine(source, f)));
        foreach (var item in originals) File.WriteAllBytes(Path.Combine(folder, item.Key), item.Value);
        var storage = Storage(folder); storage.CreateRecoveryBackup(); var before = storage.LoadStatistics(); var history = Competitive(before);
        var vm = new MainViewModel(); var counters = CompetitiveCounters(vm.Statistics); var shares = Shares(vm, vm.Statistics.History); var summaries = Summaries(vm);
        var solution = Repository.ValidWords.First(); var wrong = Repository.ValidWords.First(w => w != solution);
        for (var index = 0; index < 800; index++) vm.Statistics.History.Add(new GameHistoryEntry { Date = DateTime.Today.AddDays(-1).AddSeconds(index).ToString("yyyy-MM-dd HH:mm:ss"), IsInfinite = true, Won = true, Attempts = 2, Solution = solution, Guesses = [wrong, solution] });
        vm.Statistics.InfinitePlayed += 800; vm.Statistics.InfiniteWon += 800; vm.Statistics.InfiniteWinDistribution[1] += 800;
        var infiniteCounters = InfiniteCounters(vm.Statistics);
        Call(vm, "NormalizeStatistics"); vm.PersistActiveGameTime();
        for (var reopen = 0; reopen < 3; reopen++)
        {
            vm = new MainViewModel(); vm.PersistActiveGameTime();
            Check(Competitive(vm.Statistics) == history && CompetitiveCounters(vm.Statistics) == counters && Shares(vm, vm.Statistics.History) == shares && Summaries(vm) == summaries && vm.Statistics.History.Count(e => e.IsInfinite) == 730 && InfiniteCounters(vm.Statistics) == infiniteCounters, "real copy remains unchanged competitively after 800 synthetic Infinite and reopen");
        }
        Check(originals.All(item => item.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(source, item.Key)))), "real originals remain identical byte for byte");
        Console.WriteLine("Real competitive results preserved: " + before.History.Count(e => !e.IsInfinite));
    }
}
