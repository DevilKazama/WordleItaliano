using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WordleItaliano.Models;
using WordleItaliano.Services;
using WordleItaliano.ViewModels;
using WordleItaliano.PcIdentityCollector;

internal static class Program
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "isolated", Guid.NewGuid().ToString("N"));
    private static readonly string[] Codes =
    [
        "WIPC1-0E6555960E939D745659175D8E1F1C32C9966E1CEA6B77870DE1425031D2D058",
        "WIPC1-7617CB7E2B9309A3BEB376E7314C221E6FF1C8DD1D83E416EB6F405EAD9488AF",
        "WIPC1-9DD172AA9457F1266FD759674093A327350FE5D699691A21DC6E0AAA0E067C2D"
    ];
    private const string Outside = "WIPC1-0000000000000000000000000000000000000000000000000000000000000000";
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++; Console.WriteLine("PASS: " + label);
    }
    private static object? Field(MainViewModel vm, string name) => typeof(MainViewModel).GetField(name, Private)!.GetValue(vm);
    private static void Set(MainViewModel vm, string name, object value) => typeof(MainViewModel).GetField(name, Private)!.SetValue(vm, value);
    private static object? Call(MainViewModel vm, string name, params object[] args) => typeof(MainViewModel).GetMethod(name, Private)!.Invoke(vm, args);
    private static string Folder(string name, string code)
    {
        var folder = Path.Combine(Root, name); Directory.CreateDirectory(folder);
        Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", folder);
        Environment.SetEnvironmentVariable("WORDLE_TEST_PC_CODE", code);
        return folder;
    }
    private static string Serialized(object value) => JsonSerializer.Serialize(value);

    [STAThread] private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "startup-worker") return StartupWorker();
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Velopack.VelopackApp.Build().Run();
            foreach (var code in Codes) Check(PcAuthorization.CheckCode(code).IsAuthorized, "approved code accepted (simulated identity)");
            Check(!PcAuthorization.CheckCode(Outside).IsAuthorized, "external code rejected");
            var code1 = PcIdentity.ReadCode();
            Check(code1 == PcIdentity.ReadCode(), "actual installation identity is repeatable without printing it");
            Console.WriteLine("Actual local identity approved: " + PcAuthorization.CheckCode(code1).IsAuthorized);
            Training(Outside); Training("unreadable");
            Sequences(); RealCopy(); ReleaseBoundary(); TrainingLayout(); StartupProcesses();
            StartupUpdateChecks.Run(Root, Codes[0]);
            OfficialSequence.TestActivationDate = null;
            Check(OfficialSequence.ActivationDate == new DateOnly(2026, 10, 9), "production activation date is 9 October 2026");
            Check(OfficialSequence.ForDate(new DateOnly(2026, 10, 8)) == OfficialSequence.Legacy &&
                OfficialSequence.ForDate(new DateOnly(2026, 10, 9)) == OfficialSequence.Next &&
                OfficialSequence.ForDate(new DateOnly(2026, 10, 10)) == OfficialSequence.Next,
                "actual activation boundary selects legacy before 9 October and new sequence thereafter");
            Console.WriteLine($"PASS: {checks} PC and sequence checks; no solutions printed; real originals untouched.");
            app.Shutdown(); return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("FAIL: " + (error.InnerException?.Message ?? error.Message));
            Console.WriteLine(error.StackTrace); return 1;
        }
    }

    private static int StartupWorker()
    {
        try
        {
            Velopack.VelopackApp.Build().Run();
            using var lease = DataDirectoryLease.Acquire(DataDirectoryLease.DataFolder);
            var app = new WordleItaliano.App();
            app.InitializeComponent();
            app.DispatcherUnhandledException += (_, e) =>
            {
                Console.Error.WriteLine(e.Exception.GetType().FullName + "\n" + e.Exception.StackTrace);
                e.Handled = true;
                app.Shutdown(1);
            };
            var window = new WordleItaliano.MainWindow { ShowActivated = false, ShowInTaskbar = false };
            window.Loaded += (_, _) =>
            {
                window.Hide();
                var vm = (MainViewModel)window.DataContext;
                Console.WriteLine(vm.IsTraining ? "STARTED: training" : "STARTED: official");
                window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    new Action(() => window.Close()));
            };
            return app.Run(window);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetType().FullName + "\n" + error.StackTrace);
            return 1;
        }
    }

    private static void StartupProcesses()
    {
        var original = @"C:\Users\Magazzino3\AppData\Local\WordleItaliano";
        var files = new[] { "game.json", "statistics.json", "userSettings.json" }
            .ToDictionary(n => n, n => File.ReadAllBytes(Path.Combine(original, n)));
        foreach (var code in Codes.Concat(new[] { Outside, "unreadable" }))
        {
            var folder = Path.Combine(Root, "startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            foreach (var file in files) File.WriteAllBytes(Path.Combine(folder, file.Key), file.Value);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var start = new ProcessStartInfo(Environment.ProcessPath!)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = AppContext.BaseDirectory
                };
                if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(typeof(Program).Assembly.Location);
                start.ArgumentList.Add("startup-worker");
                start.Environment["WORDLE_STORAGE_FOLDER"] = folder;
                start.Environment["WORDLE_TEST_PC_CODE"] = code;
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                    throw new InvalidOperationException("Isolated startup worker timed out.");
                }
                var expected = Codes.Contains(code) ? "STARTED: official" : "STARTED: training";
                Check(process.ExitCode == 0 && output.GetAwaiter().GetResult().Contains(expected) &&
                    string.IsNullOrWhiteSpace(error.GetAwaiter().GetResult()),
                    "complete window startup and graceful close succeeds in separate process (simulated identity)");
                Check(File.ReadAllBytes(Path.Combine(folder, "statistics.json")).SequenceEqual(files["statistics.json"]),
                    "complete startup preserves copied official statistics byte for byte");
                if (!Codes.Contains(code))
                    Check(files.All(file => File.ReadAllBytes(Path.Combine(folder, file.Key)).SequenceEqual(file.Value)),
                        "external startup leaves copied official saves and preferences untouched");
            }
        }
        Check(files.All(file => File.ReadAllBytes(Path.Combine(original, file.Key)).SequenceEqual(file.Value)),
            "real originals untouched after ten full-window startup processes");
    }

    private static void Training(string code)
    {
        var folder = Folder(code == Outside ? "external" : "unreadable", Codes[0]);
        var storage = new StorageService();
        var seed = new MainViewModel();
        seed.SaveProfileCommand.Execute(null);
        DataDirectoryLease.Acquire(DataDirectoryLease.DataFolder).Dispose();
        File.WriteAllBytes(Path.Combine(folder, ".transaction.dpapi"), [1, 2, 3, 4]);
        Directory.CreateDirectory(Path.Combine(folder, "recovery-backups"));
        File.WriteAllBytes(Path.Combine(folder, "recovery-backups", "copied.dpapi"), [3, 2, 1]);
        var snapshots = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".wordle.lock")).ToDictionary(p => p, File.ReadAllBytes);
        Environment.SetEnvironmentVariable("WORDLE_TEST_PC_CODE", code);
        var selectionsBefore = DailyWordService.InvocationCount;
        var replaysBefore = SavedGameValidator.InvocationCount;
        var vm = new MainViewModel();
        Check(DailyWordService.InvocationCount == selectionsBefore && SavedGameValidator.InvocationCount == replaysBefore,
            "external startup invokes neither official selection nor competitive validation");
        Check(vm.IsTraining && !vm.IsOfficialAvailable, "training only for external/unreadable identity");
        Check(vm.TrainingNotice.Length > 0 && PcAuthorization.Current.IdentityUnavailable == (code == "unreadable"), "identity error distinct from unapproved PC");
        Check(Field(vm, "_dailyWordService") is null && (string)Field(vm, "_dailySolution")! == "" &&
            (string)Field(vm, "_bonusSolution")! == "" && Field(vm, "_preparedSavedGame") is null,
            "training never creates official selector or prepares competitive save");
        try { new DailyWordService(new WordRepository(), new AppSettings()).GetTodayWord(); throw new Exception("Selector was not blocked"); }
        catch (InvalidOperationException) { Check(true, "official selection blocked underneath UI"); }
        try { new SavedGameValidator(new WordRepository(), new DailyWordService(new WordRepository(), new AppSettings())).Validate(new SavedGame()); throw new Exception("Validator was not blocked"); }
        catch (InvalidOperationException) { Check(true, "competitive replay blocked underneath UI"); }
        vm.ViewDailyCommand.Execute(null); vm.ViewBonusCommand.Execute(null); vm.StartBonusCommand.Execute(null);
        Check((bool)Field(vm, "_isInfiniteActive")! && !(bool)Field(vm, "_isBonusActive")!, "official commands cannot leave training");
        var solution = (string)Field(vm, "_infiniteSolution")!;
        var wrong = new WordRepository().ValidWords.First(w => w != solution);
        foreach (var letter in wrong) Call(vm, "AddLetter", letter.ToString());
        Call(vm, "SubmitGuess");
        Set(vm, "_infiniteElapsedSeconds", 42); Set(vm, "_infiniteTimerStartedAt", null!);
        Call(vm, "SaveGame");
        var reopened = new MainViewModel();
        Check((string)Field(reopened, "_infiniteSolution")! == solution &&
            ((List<string>)Field(reopened, "_infiniteGuesses")!).SequenceEqual(new[] { wrong }) &&
            (int)Field(reopened, "_infiniteElapsedSeconds")! == 42, "training solution guesses and elapsed time persist");
        foreach (var letter in solution) Call(reopened, "AddLetter", letter.ToString());
        Call(reopened, "SubmitGuess");
        var finished = new MainViewModel();
        Check((GameStatus)Field(finished, "_infiniteStatus")! == GameStatus.Won &&
            !finished.IsStreakLineVisible && !finished.IsScoreLineVisible && !finished.IsPerfectShotCelebrationVisible,
            "finished training remains finished and has no competitive awards");
        Check(finished.Statistics.History.Count == 0 && finished.Statistics.Points == 0 &&
            finished.Statistics.Played == 0 && finished.Statistics.InfinitePlayed == 0, "training does not register in competitive or Infinite statistics");
        var entry = new GameHistoryEntry { IsInfinite = true, Won = true, Attempts = 2, Solution = solution, Guesses = [wrong, solution] };
        Check(((string)Call(finished, "BuildShareText", entry)!).Contains("ALLENAMENTO"), "shared training explicitly labeled");
        Check(snapshots.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "copied official data including undecipherable transaction remain byte-identical");
        Check(!File.Exists(Path.Combine(folder, "training", "statistics.json")), "training writes no statistics file");
        DataDirectoryLease.Acquire(DataDirectoryLease.DataFolder).Dispose();
        File.Delete(Path.Combine(folder, ".transaction.dpapi"));
        Environment.SetEnvironmentVariable("WORDLE_TEST_PC_CODE", Codes[0]);
        var returned = new MainViewModel();
        Check(returned.IsOfficialAvailable && snapshots.Where(p => p.Key.EndsWith("statistics.json"))
            .All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "returning to authorized context preserves competitive history");
    }

    private static void Sequences()
    {
        Folder("sequences", Codes[0]);
        var repository = new WordRepository(); var words = new DailyWordService(repository, new AppSettings());
        var today = DateOnly.FromDateTime(DateTime.Today);
        OfficialSequence.TestActivationDate = today;
        var allDifferent = true;
        for (var index = 0; index < 730; index++)
        {
            var date = today.AddDays(index);
            var oldDaily = words.GetWordForDate(date, OfficialSequence.Legacy);
            var oldBonus = words.GetBonusWordForDate(date, OfficialSequence.Legacy).Word;
            var next = OfficialSequence.GetNext(date);
            allDifferent &= next.Daily != oldDaily && next.Daily != oldBonus && next.Bonus != oldDaily && next.Bonus != oldBonus;
            allDifferent &= repository.IsValid(next.Daily) && repository.IsValid(next.Bonus);
            foreach (var code in Codes)
            {
                Environment.SetEnvironmentVariable("WORDLE_TEST_PC_CODE", code);
                if (OfficialSequence.GetNext(date) != next) allDifferent = false;
            }
        }
        Check(allDifferent, "730 test days identical across three simulated PCs and exclude all old daily/bonus cross matches");
        Check(OfficialSequence.ForDate(today.AddDays(-1)) == OfficialSequence.Legacy && OfficialSequence.ForDate(today) == OfficialSequence.Next,
            "test activation date switches sequence at day boundary");
        var old = new SavedGame { GameDate = today.ToString("yyyy-MM-dd"), FormatVersion = 2,
            Bonus = new BonusGame { WordLength = words.GetBonusWordForDate(today, OfficialSequence.Legacy).Length } };
        var validator = new SavedGameValidator(repository, words);
        Check(validator.Validate(old).IsValid, "old save without sequence id validates as legacy despite test activation");
        var storage = new StorageService(); storage.SaveGameAndStatistics(old, new Statistics { DataMigrationVersion = 3 });
        var vm = new MainViewModel();
        Check((string)Field(vm, "_dailySolution")! == words.GetWordForDate(today, OfficialSequence.Legacy), "existing official game keeps original word after activation");
        Call(vm, "SaveGame");
        Check(new StorageService().LoadGame()!.SequenceId == OfficialSequence.Legacy, "sequence id persisted without solution");
        Set(vm, "_todayKey", today.AddDays(-1).ToString("yyyy-MM-dd")); vm.EnsureCurrentGame();
        Check(new StorageService().LoadGame()!.SequenceId == OfficialSequence.Next &&
            (string)Field(vm, "_dailySolution")! == words.GetWordForDate(today), "day rollover chooses newly configured test sequence");
        var saved = storage.LoadGame()!;
        Check(saved.Solution is null && saved.Bonus.Solution is null && validator.Validate(saved).IsValid, "new sequence save remains solution-free and replayable");
        saved.SequenceId = "unknown";
        Check(!validator.Validate(saved).IsValid, "unknown sequence rejected before replay");
        saved.SequenceId = OfficialSequence.Legacy; saved.GameDate = today.AddDays(1).ToString("yyyy-MM-dd");
        Check(!validator.Validate(saved).IsValid, "future saved date suspended without silently changing its word");
        OfficialSequence.TestActivationDate = null;
    }

    private static void RealCopy()
    {
        var original = @"C:\Users\Magazzino3\AppData\Local\WordleItaliano";
        var originals = new[] { "game.json", "statistics.json", "userSettings.json" }.ToDictionary(n => n, n => File.ReadAllBytes(Path.Combine(original, n)));
        var folder = Folder("real-copy", Codes[0]);
        foreach (var file in originals) File.WriteAllBytes(Path.Combine(folder, file.Key), file.Value);
        var storage = new StorageService();
        var stats = Serialized(storage.LoadStatistics());
        var game = storage.LoadGame()!;
        // Legacy solutions are intentionally removed by the already implemented first-step migration.
        game.Solution = null; game.Bonus.Solution = null;
        var guesses = Serialized(new { game.GameDate, game.Status, game.Guesses, game.Bonus, game.DailyElapsedSeconds, game.DailyPerfectShot });
        for (var i = 0; i < 3; i++)
        {
            var vm = new MainViewModel();
            Check(Serialized(vm.Statistics) == stats, "authorized real-data copy preserves complete statistics points streak prizes and history");
            var after = storage.LoadGame()!;
            Check(Serialized(new { after.GameDate, after.Status, after.Guesses, after.Bonus, after.DailyElapsedSeconds, after.DailyPerfectShot }) == guesses,
                "authorized real-data copy preserves current game and timer");
        }
        Check(originals.All(p => File.ReadAllBytes(Path.Combine(original, p.Key)).SequenceEqual(p.Value)), "real original files unchanged byte for byte");
    }

    private static void ReleaseBoundary()
    {
        var releasePath = Environment.GetEnvironmentVariable("WORDLE_CHECK_RELEASE_ASSEMBLY") ??
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../WordleItaliano/bin/Release/net8.0-windows/WordleItaliano.dll"));
        var release = Assembly.LoadFile(releasePath);
        var lease = release.GetType("WordleItaliano.Services.DataDirectoryLease")!;
        Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", Path.Combine(Root, "forbidden-alternative"));
        var canonical = (string)lease.GetProperty("OfficialDataFolder")!.GetValue(null)!;
        Check(canonical == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WordleItaliano"),
            "distributed build ignores alternative data-folder variable");
        var authorization = release.GetType("WordleItaliano.Services.PcAuthorization")!;
        foreach (var simulated in new[] { Codes[0], Outside, "unreadable" })
        {
            Environment.SetEnvironmentVariable("WORDLE_TEST_PC_CODE", simulated);
            var access = authorization.GetProperty("Current")!.GetValue(null)!;
            Check((bool)access.GetType().GetProperty("IsAuthorized")!.GetValue(access)! == PcAuthorization.CheckCode(PcIdentity.ReadCode()).IsAuthorized,
                "distributed build ignores approved external and unreadable test overrides");
        }
        var sequence = release.GetType("WordleItaliano.Services.OfficialSequence")!;
        Check(sequence.GetProperty("TestActivationDate") is null &&
            Equals(sequence.GetProperty("ActivationDate")!.GetValue(null), new DateOnly(2026, 10, 9)),
            "distributed build has no test activation control and agreed activation date");
    }

    private static void TrainingLayout()
    {
        Folder("training-layout", Outside);
        var window = new WordleItaliano.MainWindow();
        var vm = (MainViewModel)window.DataContext;
        vm.ProfileNameDraft = "Test"; vm.SaveProfileCommand.Execute(null);
        vm.IsChangelogVisible = false; vm.IsSplashVisible = false; vm.IsToastVisible = false;
        foreach (var theme in new[] { "Original", "IbpLight", "IbpDark" })
        {
            vm.SetThemeCommand.Execute(theme);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(760, 750)); content.Arrange(new Rect(0, 0, 760, 750)); content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(760, 750, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var pixels = new byte[760 * 750 * 4]; bitmap.CopyPixels(pixels, 760 * 4, 0);
            Check(pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha != 0) && pixels.Distinct().Count() > 4,
                "offscreen training render contains visible content in " + theme);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(Root, "training-" + theme + ".png")); png.Save(output);
            Check(vm.ModeBadgeText == "Allenamento" && !vm.IsDailyViewButtonVisible && !vm.IsBonusViewButtonVisible,
                "training window renders without official navigation in " + theme);
        }
        window.Close();
        Console.WriteLine("Offscreen training renders: " + Root);
    }
}
