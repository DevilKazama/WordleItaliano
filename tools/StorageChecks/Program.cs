using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using WordleItaliano.Models;
using WordleItaliano.Services;
using WordleItaliano.ViewModels;

internal static class Program
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "isolated", Guid.NewGuid().ToString("N"));
    private static readonly WordRepository Repository = new();
    private static readonly DailyWordService Words = new(Repository, new AppSettings());
    private static int checks;
    private static string Executable => Path.Combine(AppContext.BaseDirectory, "StorageChecks.exe");

    [STAThread] static int Main(string[] args)
    {
        try
        {
            OfficialSequence.TestActivationDate = DateOnly.MaxValue;
            if (args.Length > 0) return Worker(args);
            var app = new Application();
            Velopack.VelopackApp.Build().Run();
            Run();
            app.Shutdown();
            Console.WriteLine($"PASS: {checks} storage checks; no solution values printed.");
            return 0;
        }
        catch (Exception error)
        {
            for (var current = error; current is not null; current = current.InnerException)
                Console.WriteLine("FAIL: " + current.GetType().Name + " HResult=" + current.HResult + "\n" + current.StackTrace);
            return 1;
        }
    }

    private static int Worker(string[] args)
    {
        Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", args[1]);
        if (args[0] == "recover")
            Hook(phase =>
            {
                if (phase != args[2]) return;
                Console.WriteLine("AT-CHECKPOINT"); Console.Out.Flush(); Thread.Sleep(Timeout.Infinite);
            });
        try
        {
            var storage = new StorageService();
            if (args[0] == "probe") { Console.WriteLine("OPEN"); return 0; }
            if (args[0] == "hold") { Console.WriteLine("READY"); Console.Out.Flush(); Thread.Sleep(Timeout.Infinite); }
            if (args[0] == "submit")
            {
                var app = new Application(); Velopack.VelopackApp.Build().Run();
                var vm = new MainViewModel();
                var guess = args[3] == "lost" ? Repository.ValidWords.First(w => w != Words.GetTodayWord())
                    : args[3] == "bonus" ? Words.GetTodayBonusWord().Word : Words.GetTodayWord();
                foreach (var letter in guess) Invoke(vm, "AddLetter", letter.ToString());
                Hook(phase =>
                {
                    if (phase != args[2]) return;
                    Console.WriteLine("AT-CHECKPOINT"); Console.Out.Flush(); Thread.Sleep(Timeout.Infinite);
                });
                Invoke(vm, "SubmitGuess");
                throw new InvalidOperationException("Checkpoint not reached.");
            }
            if (args[0] == "commit")
            {
                Hook(phase =>
                {
                    if (phase != args[2]) return;
                    Console.WriteLine("AT-CHECKPOINT"); Console.Out.Flush(); Thread.Sleep(Timeout.Infinite);
                });
                var pair = Pair(args[3]);
                storage.SaveGameAndStatistics(pair.Game, pair.Stats);
                throw new InvalidOperationException("Checkpoint not reached.");
            }
            return 0;
        }
        catch (DataDirectoryInUseException) { Console.WriteLine("LOCKED"); return 3; }
    }

    private static void Hook(Action<string>? action) => typeof(StorageService)
        .GetField("CommitCheckpoint", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, action);

    private static Process Start(string executable, params string[] args)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    private static string Line(Process process)
    {
        var pending = process.StandardOutput.ReadLineAsync();
        if (!pending.Wait(TimeSpan.FromSeconds(30))) { process.Kill(true); throw new TimeoutException(); }
        return pending.Result ?? "";
    }

    private static void Kill(Process process) { process.Kill(true); Check(process.WaitForExit(10000), "actual child termination finished"); }
    private static void Check(bool value, string label)
    {
        Console.WriteLine((value ? "PASS: " : "FAIL: ") + label);
        if (!value) throw new InvalidOperationException();
        checks++;
    }

    private static StorageService Storage(string folder)
    {
        Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", folder);
        return new StorageService();
    }

    private static (SavedGame Game, Statistics Stats) Pair(string outcome)
    {
        var wrong = Repository.ValidWords.First(w => w != Words.GetTodayWord());
        var guesses = outcome == "won" ? new List<string> { wrong, Words.GetTodayWord() }
            : Enumerable.Repeat(wrong, outcome == "lost" ? 6 : 2).ToList();
        var status = outcome == "won" ? GameStatus.Won : outcome == "lost" ? GameStatus.Lost : GameStatus.Playing;
        var game = new SavedGame
        {
            GameDate = Words.TodayKey, Date = Words.TodayKey, FormatVersion = 2, Guesses = guesses, Status = status,
            DailyElapsedSeconds = 73, DailyTimerStarted = true,
            Bonus = new BonusGame { WordLength = Words.GetTodayBonusWord().Length, IsUnlocked = status == GameStatus.Won }
        };
        var stats = new Statistics { DataMigrationVersion = 3 };
        if (status != GameStatus.Playing)
        {
            var won = status == GameStatus.Won;
            stats.Played = 1; stats.Won = won ? 1 : 0; stats.Points = won ? 25 : 0;
            stats.CurrentStreak = stats.BestStreak = won ? 1 : 0; stats.LastPlayedDate = Words.TodayKey;
            stats.LastWinDate = won ? Words.TodayKey : "";
            if (won) stats.WinDistribution[1] = 1;
            stats.History.Add(new GameHistoryEntry { Date = Words.TodayKey, Solution = Words.GetTodayWord(), Guesses = guesses,
                Won = won, Attempts = guesses.Count, WordLength = 5, DurationSeconds = 73, Points = stats.Points, ScoreEarned = stats.Points,
                BaseScore = stats.Points, DayBaseScore = stats.Points, DayFinalScore = stats.Points, StreakAtDate = stats.CurrentStreak, StreakMultiplierPercent = 100 });
        }
        return (game, stats);
    }

    private static void Seed(string folder)
    {
        var storage = Storage(folder);
        storage.SaveUserSettings(new UserSettings { PlayerName = "Checks", LastSeenChangelogVersion = "1.5.24" });
        var pair = Pair("ongoing"); storage.SaveGameAndStatistics(pair.Game, pair.Stats);
        DataDirectoryLease.Acquire(folder).Dispose();
    }

    private static void Run()
    {
        Directory.CreateDirectory(Root);
        var alternate = Path.Combine(Root, "other-program"); Directory.CreateDirectory(alternate);
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory)) File.Copy(file, Path.Combine(alternate, Path.GetFileName(file)));
        Directory.CreateDirectory(Path.Combine(alternate, "Data"));
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Data"))) File.Copy(file, Path.Combine(alternate, "Data", Path.GetFileName(file)));
        var folder = Path.Combine(Root, "lock"); Seed(folder);
        var originalFiles = new[] { "game.json", "statistics.json", "userSettings.json" }
            .ToDictionary(file => file, file => File.ReadAllBytes(Path.Combine(folder, file)));
        using (var first = Start(Executable, "hold", folder))
        {
            Check(Line(first) == "READY", "first real process owns folder");
            foreach (var executable in new[] { Executable, Path.Combine(alternate, "StorageChecks.exe") })
            {
                using var second = Start(executable, "probe", folder);
                Check(Line(second) == "LOCKED" && second.WaitForExit(10000) && second.ExitCode == 3, "second real process rejected, including copied executable");
            }
            using var independent = Start(Executable, "probe", Path.Combine(Root, "independent"));
            Check(Line(independent) == "OPEN" && independent.WaitForExit(10000) && independent.ExitCode == 0, "distinct folder runs concurrently");
            Check(originalFiles.All(item => item.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, item.Key)))), "second starts did not modify game statistics or preferences");
            Kill(first);
        }
        using (var reopened = Start(Executable, "probe", folder))
            Check(Line(reopened) == "OPEN" && reopened.WaitForExit(10000) && reopened.ExitCode == 0, "lease released after real abrupt termination");

        foreach (var outcome in new[] { "won", "lost", "ongoing" })
        foreach (var phase in new[] { "journal-flushed", "prepared", "game-flushed", "game-applied", "statistics-flushed", "statistics-applied", "completed" })
        {
            folder = Path.Combine(Root, outcome + "-" + phase); Seed(folder);
            using (var child = Start(Executable, "commit", folder, phase, outcome))
            {
                Check(Line(child) == "AT-CHECKPOINT", "real process reached " + outcome + "/" + phase);
                Kill(child);
            }
            var storage = Storage(folder);
            var expected = Pair(phase == "journal-flushed" ? "ongoing" : outcome);
            var saved = storage.LoadGame()!; var stats = storage.LoadStatistics();
            Check(saved.Status == expected.Game.Status && saved.Guesses.SequenceEqual(expected.Game.Guesses) && saved.DailyElapsedSeconds == 73 && stats.Points == expected.Stats.Points && stats.Played == expected.Stats.Played, "coherent durable pair recovered " + outcome + "/" + phase);
            for (var i = 0; i < 2; i++)
            {
                var vm = new MainViewModel(); vm.PersistActiveGameTime();
                var again = storage.LoadStatistics();
                Check(again.Points == expected.Stats.Points && again.Played == expected.Stats.Played && again.History.Count == expected.Stats.History.Count, "recovered state does not reset or duplicate results");
            }
            NoSecrets(folder);
        }
        WriteFailures();
        RecoveryInterrupted();
        SubmitInterruptions();
        RealCopy();
    }

    private static void RecoveryInterrupted()
    {
        var folder = Path.Combine(Root, "interrupted-recovery"); Seed(folder);
        using (var child = Start(Executable, "commit", folder, "prepared", "lost"))
        {
            Check(Line(child) == "AT-CHECKPOINT", "transaction prepared before recovery test"); Kill(child);
        }
        using (var child = Start(Executable, "recover", folder, "game-applied"))
        {
            Check(Line(child) == "AT-CHECKPOINT", "recovery itself reached controlled checkpoint"); Kill(child);
        }
        var storage = Storage(folder); var vm = new MainViewModel(); vm.PersistActiveGameTime();
        Check(storage.LoadGame()!.Status == GameStatus.Lost && storage.LoadStatistics().Played == 1 && storage.LoadStatistics().History.Count == 1, "repeated real interruption still recovers one loss");
        NoSecrets(folder);
    }

    private static void Invoke(MainViewModel vm, string method, params object[] args) => typeof(MainViewModel)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, args);

    private static void SubmitInterruptions()
    {
        foreach (var outcome in new[] { "won", "lost", "bonus" })
        foreach (var phase in new[] { "prepared", "game-applied", "statistics-applied", "completed" })
        {
            var folder = Path.Combine(Root, "submit-" + outcome + "-" + phase); Seed(folder);
            var storage = Storage(folder); var pair = Pair(outcome == "bonus" ? "won" : "ongoing");
            if (outcome == "lost") pair.Game.Guesses = Enumerable.Repeat(pair.Game.Guesses[0], 5).ToList();
            if (outcome == "bonus")
            {
                pair.Game.Bonus.Guesses = new List<string> { Repository.GetBonusWords(pair.Game.Bonus.WordLength).First(w => w != Words.GetTodayBonusWord().Word) };
                pair.Game.Bonus.TimerStarted = true; pair.Game.Bonus.ElapsedSeconds = 41;
            }
            storage.SaveGameAndStatistics(pair.Game, pair.Stats); DataDirectoryLease.Acquire(folder).Dispose();
            using (var child = Start(Executable, "submit", folder, phase, outcome))
            {
                Check(Line(child) == "AT-CHECKPOINT", "actual submission reached " + outcome + "/" + phase);
                Kill(child);
            }
            storage = Storage(folder); var vm = new MainViewModel(); vm.PersistActiveGameTime();
            var game = storage.LoadGame()!; var stats = storage.LoadStatistics();
            Check(game.Status == (outcome == "lost" ? GameStatus.Lost : GameStatus.Won) && stats.Played == 1 && stats.Won == (outcome == "lost" ? 0 : 1), "actual submission terminal outcome reconstructed after kill");
            Check(outcome == "bonus" ? game.Bonus.Status == GameStatus.Won && game.Bonus.Guesses.Count == 2 && stats.BonusPlayed == 1 && stats.BonusWon == 1 && stats.History.Count == 2
                : game.Guesses.Count == (outcome == "lost" ? 6 : 3) && stats.History.Count == 1 && stats.Points == (outcome == "lost" ? 0 : 20), "actual submission attempts history and score consistent");
            var baseline = JsonSerializer.Serialize(stats);
            vm = new MainViewModel(); vm.PersistActiveGameTime();
            Check(baseline == JsonSerializer.Serialize(storage.LoadStatistics()), "actual submission reopen cannot duplicate result");
            NoSecrets(folder);
        }
    }

    private static void NoSecrets(string folder)
    {
        foreach (var file in Directory.GetFiles(folder).Where(f => Path.GetFileName(f).StartsWith("game.json")))
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(file));
            Check(!json.RootElement.TryGetProperty("Solution", out _) && !json.RootElement.GetProperty("Bonus").TryGetProperty("Solution", out _), "game and orphaned game temporary files contain no solution properties");
        }
        Check(!File.Exists(Path.Combine(folder, ".transaction.dpapi")), "completed recovery removed journal");
    }

    private static void WriteFailures()
    {
        var folder = Path.Combine(Root, "write-error"); Seed(folder); var storage = Storage(folder);
        var prior = File.ReadAllBytes(Path.Combine(folder, "statistics.json"));
        using (var held = new FileStream(Path.Combine(folder, "statistics.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var pair = Pair("lost");
            try { storage.SaveGameAndStatistics(pair.Game, pair.Stats); Check(false, "write must fail"); }
            catch (StorageFailureException) { Check(storage.IsFaulted && prior.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "statistics.json"))), "real sharing error reported; last valid statistics preserved"); }
            Check(File.Exists(Path.Combine(folder, ".transaction.dpapi")), "failed write retains durable recovery journal");
            try { storage.SaveStatistics(new Statistics()); Check(false, "faulted store must refuse new writes"); }
            catch (StorageFailureException) { Check(true, "faulted store refuses further writes"); }
        }
        storage = Storage(folder); var vm = new MainViewModel();
        Check(storage.LoadGame()!.Status == GameStatus.Lost && storage.LoadStatistics().Played == 1 && storage.LoadStatistics().History.Single().Won == false, "actual failed write recovers loss exactly once");
        NoSecrets(folder);
        var settings = File.ReadAllBytes(Path.Combine(folder, "userSettings.json"));
        using (var held = new FileStream(Path.Combine(folder, "userSettings.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { storage.SaveUserSettings(new UserSettings()); Check(false, "atomic preference write must fail"); }
            catch (StorageFailureException) { Check(settings.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "userSettings.json"))), "single-file write error preserves previous preferences"); }
        }
        folder = Path.Combine(Root, "corrupt-statistics"); Seed(folder);
        File.WriteAllText(Path.Combine(folder, "statistics.json"), "{broken"); storage = Storage(folder);
        try { storage.LoadStatistics(); Check(false, "corrupt statistics must not become empty"); }
        catch (StorageFailureException) { Check(File.ReadAllText(Path.Combine(folder, "statistics.json")) == "{broken", "corrupt statistics preserved and reported, not first launch"); }
        folder = Path.Combine(Root, "missing-statistics"); Seed(folder); File.Delete(Path.Combine(folder, "statistics.json")); storage = Storage(folder);
        try { storage.LoadStatistics(); Check(false, "missing statistics must not reset existing game"); }
        catch (StorageFailureException) { Check(storage.IsFaulted, "missing statistics with game reported rather than new profile"); }
        folder = Path.Combine(Root, "incomplete-statistics"); Seed(folder); File.WriteAllText(Path.Combine(folder, "statistics.json"), "{}"); storage = Storage(folder);
        try { storage.LoadStatistics(); Check(false, "incomplete statistics must not become empty"); }
        catch (StorageFailureException) { Check(storage.IsFaulted, "incomplete statistics reported rather than silently replaced"); }
        folder = Path.Combine(Root, "corrupt-journal"); Seed(folder);
        File.WriteAllBytes(Path.Combine(folder, ".transaction.dpapi"), new byte[] { 1, 2, 3 });
        var gameBytes = File.ReadAllBytes(Path.Combine(folder, "game.json"));
        try { Storage(folder); Check(false, "corrupt journal must suspend load"); }
        catch (StorageFailureException) { Check(gameBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "game.json"))) && File.Exists(Path.Combine(folder, ".transaction.dpapi")), "unrecoverable journal preserved with game unchanged"); }
    }

    private static void RealCopy()
    {
        var source = @"C:\Users\Magazzino3\AppData\Local\WordleItaliano";
        var folder = Path.Combine(Root, "real-copy"); Directory.CreateDirectory(folder);
        var originals = new[] { "game.json", "statistics.json", "userSettings.json" }.ToDictionary(f => f, f => File.ReadAllBytes(Path.Combine(source, f)));
        foreach (var item in originals) File.WriteAllBytes(Path.Combine(folder, item.Key), item.Value);
        var storage = Storage(folder); storage.CreateRecoveryBackup();
        var before = storage.LoadStatistics(); var history = JsonSerializer.Serialize(before.History);
        var vm = new MainViewModel(); var after = storage.LoadStatistics();
        Check(history == JsonSerializer.Serialize(after.History) && before.Points == after.Points && before.CurrentStreak == after.CurrentStreak && before.BestStreak == after.BestStreak, "real copy preserves complete history points streak and prizes");
        var baseline = JsonSerializer.Serialize(after);
        for (var i = 0; i < 3; i++) { vm = new MainViewModel(); vm.PersistActiveGameTime(); Check(baseline == JsonSerializer.Serialize(storage.LoadStatistics()), "real copy reopen is idempotent"); }
        Check(originals.All(item => item.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(source, item.Key)))), "real originals unchanged byte for byte");
        Console.WriteLine("Real history entries verified: " + after.History.Count);
        NoSecrets(folder);
    }
}
