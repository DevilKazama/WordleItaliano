using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using WordleItaliano.Models;
using WordleItaliano.Services;
using WordleItaliano.ViewModels;

internal static class Program
{
    private static string folder = "";
    private static readonly WordRepository Repository = new();
    private static readonly DailyWordService Words = new(Repository, new AppSettings());
    private static string Daily => Words.GetTodayWord();
    private static (string Word, int Length) Bonus => Words.GetTodayBonusWord();
    private static int checks;

    [STAThread] private static void Main()
    {
        try
        {
            var app = new Application();
            OfficialSequence.TestActivationDate = DateOnly.MaxValue;
            Velopack.VelopackApp.Build().Run();
            Run();
            Console.WriteLine($"PASS: {checks} isolated checks; no solution values printed; real user data untouched.");
            app.Shutdown();
        }
        catch (Exception error)
        {
            Console.WriteLine("FAIL: " + error.GetType().Name + ". Check labels contain no solutions.");
            Environment.ExitCode = 1;
        }
    }
    private static void Assert(bool value, string label)
    {
        if (!value) { Console.WriteLine("Failed check: " + label); throw new InvalidOperationException(); }
        checks++;
    }
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args)!;
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static StorageService Fresh()
    {
        folder = Path.Combine(AppContext.BaseDirectory, "isolated", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", folder);
        var storage = new StorageService();
        storage.SaveUserSettings(new UserSettings { PlayerName = "Checks", LastSeenChangelogVersion = "1.5.24" });
        storage.SaveStatistics(new Statistics { DataMigrationVersion = 3 });
        return storage;
    }
    private static MainViewModel Open()
    {
        var vm = new MainViewModel(); Pump(); Call(vm, "CloseOverlays"); vm.IsSplashVisible = false; vm.IsProfileDialogVisible = false;
        return vm;
    }
    private static SavedGame Legacy(List<string> guesses, GameStatus status) => new()
    {
        GameDate = DateTime.Today.ToString("yyyy-MM-dd"), Date = DateTime.Today.ToString("yyyy-MM-dd"), Solution = Daily,
        Guesses = guesses, Status = status, DailyElapsedSeconds = 73, DailyTimerStarted = true,
        Bonus = new BonusGame { Solution = Bonus.Word, WordLength = Bonus.Length }
    };
    private static byte[] WriteLegacy(StorageService storage, SavedGame game)
    {
        storage.CreateRecoveryBackup();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(game);
        File.WriteAllBytes(Path.Combine(folder, "game.json"), bytes);
        return bytes;
    }
    private static void NoSecrets()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "game.json")));
        Assert(!json.RootElement.TryGetProperty("Solution", out _) && !json.RootElement.GetProperty("Bonus").TryGetProperty("Solution", out _), "no daily/bonus solution properties");
        Assert(json.RootElement.GetProperty("FormatVersion").GetInt32() == 2, "versioned format");
    }
    private static void Reject(Action<SavedGame> alter, string label)
    {
        var storage = Fresh(); var game = Legacy([], GameStatus.Playing); alter(game);
        var original = WriteLegacy(storage, game);
        var vm = Open(); Assert(!vm.CanUseFavoriteWord && storage.LoadGame()!.RecoveryRequired, label);
        NoSecrets();
        var backups = Directory.GetFiles(Path.Combine(folder, "recovery-backups"), "game.json.dpapi", SearchOption.AllDirectories);
        Assert(backups.Any(path => LocalBackupProtection.Unprotect(File.ReadAllBytes(path)).SequenceEqual(original)), "exact original recoverable backup");
        var stats = File.ReadAllBytes(Path.Combine(folder, "statistics.json"));
        vm.PersistActiveGameTime(); var again = Open();
        Assert(!again.CanUseFavoriteWord && stats.SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "statistics.json"))), "reopening blocked game does not award results");
    }
    private static void Run()
    {
        var storage = Fresh(); var vm = Open(); NoSecrets();
        Assert(!storage.LoadGame()!.Bonus.IsUnlocked, "fresh bonus locked");
        var wrong = Repository.ValidWords.First(w => w != Daily);
        storage = Fresh(); var game = Legacy([wrong], GameStatus.Playing); var original = WriteLegacy(storage, game);
        vm = Open(); NoSecrets();
        Assert(storage.LoadGame()!.Guesses.SequenceEqual(new[] { wrong }) && storage.LoadGame()!.DailyElapsedSeconds == 73 && vm.CanUseFavoriteWord, "ongoing legacy attempts and timer preserved");
        Assert(Directory.GetFiles(Path.Combine(folder, "recovery-backups"), "game.json.dpapi", SearchOption.AllDirectories).Any(path => LocalBackupProtection.Unprotect(File.ReadAllBytes(path)).SequenceEqual(original)), "legacy backup decrypts byte-for-byte");
        vm.PersistActiveGameTime(); Open(); Assert(storage.LoadStatistics().History.Count == 0, "ongoing reopen records no result");
        foreach (var won in new[] { true, false })
        {
            storage = Fresh(); game = Legacy(won ? [wrong, Daily] : Enumerable.Repeat(wrong, 6).ToList(), won ? GameStatus.Won : GameStatus.Lost);
            WriteLegacy(storage, game); vm = Open(); NoSecrets();
            var stats = storage.LoadStatistics(); Assert(stats.Played == 1 && stats.Won == (won ? 1 : 0) && stats.History.Single().Won == won, "completed migration records derived outcome");
            var points = stats.Points; Open(); Open(); stats = storage.LoadStatistics();
            Assert(stats.Played == 1 && stats.Won == (won ? 1 : 0) && stats.Points == points && stats.History.Count == 1, "completed reopen idempotent");
        }
        storage = Fresh(); game = Legacy([Daily], GameStatus.Won);
        game.DailyPerfectShot = new PerfectShotState { IsPerfectShot = true, PrizePromptShown = true, PrizeText = "Preserved prize" };
        game.Bonus.IsUnlocked = true; game.Bonus.Guesses = [Bonus.Word]; game.Bonus.Status = GameStatus.Won;
        game.Bonus.ElapsedSeconds = 91; game.Bonus.TimerStarted = true;
        WriteLegacy(storage, game); vm = Open(); Open();
        var completed = storage.LoadStatistics();
        Assert(completed.Played == 1 && completed.BonusPlayed == 1 && completed.History.Count == 2 && completed.TwoPointDays == 1, "both completed outcomes idempotent");
        Assert(completed.History.First(e => !e.IsBonus).PerfectShotPrizeText == "Preserved prize" && completed.History.First(e => e.IsBonus).DurationSeconds == 91, "prize and bonus timer preserved");
        Assert(((string)Call(vm, "BuildShareText", completed.History.First(e => !e.IsBonus))).Contains("COLPO PERFETTO"), "valid sharing preserved");
        Reject(g => g.Status = GameStatus.Won, "false victory blocked");
        Reject(g => g.DailyPerfectShot.IsPerfectShot = true, "false perfect shot blocked");
        Reject(g => g.Solution = wrong, "substituted daily solution blocked");
        Reject(g => g.Bonus.Solution = Repository.GetBonusWords(Bonus.Length).First(w => w != Bonus.Word), "substituted bonus blocked");
        Reject(g => g.Bonus.WordLength = Bonus.Length == 7 ? 6 : 7, "altered bonus length blocked");
        Reject(g => g.Bonus.IsUnlocked = true, "premature bonus unlock blocked");
        Reject(g => g.Guesses = ["zzzzz"], "invalid dictionary guess blocked");
        Reject(g => { g.Guesses = [Daily, wrong]; g.Status = GameStatus.Won; }, "post-win attempts blocked");
        storage = Fresh(); game = Legacy([Daily], GameStatus.Playing); WriteLegacy(storage, game); vm = Open();
        Assert(storage.LoadGame()!.Status == GameStatus.Won && storage.LoadStatistics().History.Single().Attempts == 1, "interrupted completion recovered");
        storage = Fresh(); game = Legacy(Enumerable.Repeat(wrong, 6).ToList(), GameStatus.Playing); WriteLegacy(storage, game); vm = Open();
        Assert(storage.LoadGame()!.Status == GameStatus.Lost && storage.LoadStatistics().History.Single().Won == false, "interrupted sixth guess recovered as loss");
        storage = Fresh(); game = Legacy([Daily], GameStatus.Won); game.Bonus.IsUnlocked = true;
        var bonusWrong = Repository.GetBonusWords(Bonus.Length).First(w => w != Bonus.Word);
        game.Bonus.Guesses = [bonusWrong]; game.Bonus.ElapsedSeconds = 46; game.Bonus.TimerStarted = true;
        WriteLegacy(storage, game); vm = Open(); Open(); NoSecrets();
        Assert(storage.LoadGame()!.Bonus.Guesses.SequenceEqual(new[] { bonusWrong }) && storage.LoadGame()!.Bonus.ElapsedSeconds == 46 && storage.LoadStatistics().Played == 1 && storage.LoadStatistics().BonusPlayed == 0, "ongoing bonus migration preserves length attempts time and daily outcome");
        storage = Fresh(); game = Legacy([wrong, Daily], GameStatus.Won); game.DailyPerfectShot.IsPerfectShot = true;
        WriteLegacy(storage, game); vm = Open(); Assert(!vm.CanUseFavoriteWord && storage.LoadGame()!.RecoveryRequired, "false perfect shot after two guesses suspended");
        storage = Fresh(); game = Legacy([wrong], GameStatus.Playing); WriteLegacy(storage, game); vm = Open();
        var modern = storage.LoadGame()!; modern.Status = GameStatus.Won; storage.SaveGame(modern); vm = Open();
        Assert(!vm.CanUseFavoriteWord && storage.LoadStatistics().History.Count == 0, "new-format false victory also blocked");
        storage = Fresh(); game = Legacy([wrong], GameStatus.Playing); WriteLegacy(storage, game); vm = Open();
        var prior = new GameHistoryEntry { Date = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), Won = true, Attempts = 2, Solution = wrong, Guesses = [wrong], ScoreEarned = 25, StreakAtDate = 1, PerfectShotPrizeText = "Older prize" };
        var old = storage.LoadStatistics(); old.History.Add(prior); old.Played = 1; old.Won = 1; old.Points = 25; old.LastWinDate = prior.Date; old.CurrentStreak = 1; storage.SaveStatistics(old);
        var priorBytes = JsonSerializer.Serialize(prior); vm = Open();
        Assert(JsonSerializer.Serialize(storage.LoadStatistics().History.Single()) == priorBytes, "unrelated history not rewritten");
        game = Legacy([wrong, Daily], GameStatus.Won); WriteLegacy(storage, game); vm = Open();
        var streakStats = storage.LoadStatistics(); var streakPoints = streakStats.Points;
        Assert(streakStats.CurrentStreak == 2 && streakStats.History.Last().StreakAtDate == 2 && JsonSerializer.Serialize(streakStats.History.First()) == priorBytes, "existing streak and unrelated prize preserved on completion");
        Open(); Assert(storage.LoadStatistics().Points == streakPoints && storage.LoadStatistics().CurrentStreak == 2, "streak multiplier and points stable on reopen");
        storage = Fresh(); storage.CreateRecoveryBackup();
        var corrupt = System.Text.Encoding.UTF8.GetBytes("{broken-json"); File.WriteAllBytes(Path.Combine(folder, "game.json"), corrupt);
        vm = Open(); NoSecrets();
        Assert(storage.LoadGame()!.RecoveryRequired && !vm.CanUseFavoriteWord && storage.LoadStatistics().History.Count == 0, "unreadable save suspended without new result");
        Assert(Directory.GetFiles(Path.Combine(folder, "recovery-backups"), "game.json.dpapi", SearchOption.AllDirectories).Any(path => LocalBackupProtection.Unprotect(File.ReadAllBytes(path)).SequenceEqual(corrupt)), "unreadable original recoverable byte-for-byte");
        storage = Fresh(); game = Legacy([Daily], GameStatus.Won); WriteLegacy(storage, game); vm = Open();
        var affected = storage.LoadStatistics(); affected.History[0].ScoreEarned = 9999; affected.History[0].Points = 9999; storage.SaveStatistics(affected);
        vm = Open(); Assert(storage.LoadStatistics().Points == 30 && storage.LoadStatistics().History[0].ScoreEarned == 30, "affected result score recomputed without trust in stored score");
        storage = Fresh(); game = Legacy([wrong], GameStatus.Playing); WriteLegacy(storage, game); vm = Open();
        RenderThemes();
        var window = new WordleItaliano.MainWindow(); window.Measure(new Size(760, 780)); window.Arrange(new Rect(0, 0, 760, 780)); window.UpdateLayout(); window.Close();
        Assert(true, "WPF templates load without desktop interaction");
    }
    private static void RenderThemes()
    {
        var window = new WordleItaliano.MainWindow();
        var vm = (MainViewModel)window.DataContext;
        Call(vm, "CloseOverlays"); vm.IsSplashVisible = false; vm.IsProfileDialogVisible = false;
        foreach (var theme in new[] { "Original", "IbpLight", "IbpDark" })
        {
            vm.SetThemeCommand.Execute(theme);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(760, 750)); content.Arrange(new Rect(0, 0, 760, 750)); content.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(760, 750, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(folder, "view-" + theme + ".png")); encoder.Save(file);
        }
        Console.WriteLine("Visual checks folder: " + folder);
        window.Close();
    }
}
