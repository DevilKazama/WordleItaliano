using System.IO;
using System.Text.Json;
using WordleItaliano.Models;

namespace WordleItaliano.Services;

public sealed class StorageService
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly string _folder;
    private readonly string _gamePath;
    private readonly string _statsPath;
    private readonly string _userSettingsPath;
    private readonly string _journalPath;
    private readonly object _sync;
    private readonly bool _official = PcAuthorization.Current.IsAuthorized;
    public bool IsFaulted { get; private set; }
    internal static Action<string>? CommitCheckpoint = null;

    public StorageService()
    {
        _folder = DataDirectoryLease.DataFolder;
        _sync = DataDirectoryLease.Acquire(_folder).SyncRoot;
        _gamePath = Path.Combine(_folder, "game.json");
        _statsPath = Path.Combine(_folder, "statistics.json");
        _userSettingsPath = Path.Combine(_folder, "userSettings.json");
        _journalPath = Path.Combine(_folder, ".transaction.dpapi");
        if (_official) Guard(RecoverTransaction);
    }

    public bool UserSettingsExists => IsPresent(_userSettingsPath);
    public string? GameReadError { get; private set; }
    public bool GameExists => IsPresent(_gamePath);

    public SavedGame? LoadGame()
    {
        if (!_official) return new SavedGame { Infinite = LoadTraining() };
        GameReadError = null;
        try
        {
            if (!Exists(_gamePath)) return null;
            var game = JsonSerializer.Deserialize<SavedGame>(File.ReadAllText(_gamePath), _options);
            if (game is null) GameReadError = "Il salvataggio della partita è vuoto o non leggibile.";
            return game;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            GameReadError = "Non è stato possibile leggere il salvataggio della partita.";
            return null;
        }
    }

    public Statistics LoadStatistics()
    {
        if (!_official) return new Statistics();
        Statistics? result = null;
        Guard(() =>
        {
            if (!Exists(_statsPath))
            {
                if (Exists(_gamePath)) throw new InvalidDataException("Statistiche mancanti con partita esistente.");
                result = new Statistics();
            }
            else result = ReadStatistics(File.ReadAllText(_statsPath));
        });
        return result!;
    }

    public UserSettings LoadUserSettings()
    {
        var settings = Load<UserSettings>(_userSettingsPath);
        if (settings is not null || _official) return settings ?? new UserSettings();
        // Copied preferences may be consulted, never rewritten from training.
        try
        {
            var original = Path.Combine(DataDirectoryLease.OfficialDataFolder, "userSettings.json");
            return File.Exists(original) ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(original), _options) ?? new() : new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void SaveGame(SavedGame game)
    {
        if (!_official) { SaveTraining(game.Infinite); return; }
        game.FormatVersion = 2;
        game.Solution = null;
        game.Bonus.Solution = null;
        Save(_gamePath, game);
    }

    public string CreateRecoveryBackup()
    {
        PcAuthorization.RequireOfficial();
        var destination = Path.Combine(_folder, "recovery-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N"));
        Guard(() =>
        {
            Directory.CreateDirectory(destination);
            foreach (var path in new[] { _gamePath, _statsPath, _userSettingsPath })
                if (Exists(path)) AtomicWrite(Path.Combine(destination, Path.GetFileName(path) + ".dpapi"), LocalBackupProtection.Protect(File.ReadAllBytes(path)), "backup");
        });
        return destination;
    }

    public void SaveGameAndStatistics(SavedGame game, Statistics statistics)
    {
        if (!_official) { SaveTraining(game.Infinite); return; }
        game.FormatVersion = 2;
        game.Solution = null;
        game.Bonus.Solution = null;
        Guard(() =>
        {
            var transaction = new Transaction(JsonSerializer.Serialize(game, _options), JsonSerializer.Serialize(statistics, _options));
            AtomicWrite(_journalPath, LocalBackupProtection.Protect(JsonSerializer.SerializeToUtf8Bytes(transaction)), "journal");
            CommitCheckpoint?.Invoke("prepared");
            ApplyTransaction(transaction);
        });
    }

    public void SaveStatistics(Statistics statistics)
    {
        if (!_official) return;
        Save(_statsPath, statistics);
    }

    public void SaveUserSettings(UserSettings settings)
    {
        Save(_userSettingsPath, settings);
    }

    private void SaveTraining(InfiniteGame game) => Save(Path.Combine(_folder, "training.json"), game);

    private InfiniteGame LoadTraining()
    {
        var game = Load<InfiniteGame>(Path.Combine(_folder, "training.json")) ?? new();
        Guard(() =>
        {
            if (game.Guesses is null || game.Solution is null) throw new InvalidDataException("Allenamento incompleto.");
            if (string.IsNullOrEmpty(game.Solution) && game.Guesses.Count == 0) return;
            var repository = new WordRepository();
            if (game.Solution.Length != 5 || !repository.IsValid(game.Solution) || game.ElapsedSeconds < 0 || game.Guesses.Count > 6)
                throw new InvalidDataException("Allenamento non leggibile. I dati ufficiali non sono stati modificati.");
            var status = GameStatus.Playing;
            foreach (var guess in game.Guesses)
            {
                if (status != GameStatus.Playing || guess is null || guess.Length != 5 || !repository.IsValid(guess)) throw new InvalidDataException();
                if (guess == game.Solution) status = GameStatus.Won;
            }
            if (status == GameStatus.Playing && game.Guesses.Count == 6) status = GameStatus.Lost;
            if (game.Status != status && game.Status != GameStatus.Playing) throw new InvalidDataException();
            game.Status = status;
        });
        return game;
    }

    private T? Load<T>(string path)
    {
        T? value = default;
        Guard(() =>
        {
            if (Exists(path)) value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options)
                ?? throw new InvalidDataException("File dati vuoto.");
        });
        return value;
    }

    private void Save<T>(string path, T value)
    {
        Guard(() => AtomicWrite(path, JsonSerializer.SerializeToUtf8Bytes(value, _options), Path.GetFileName(path)));
    }

    private void RecoverTransaction()
    {
        if (!Exists(_journalPath)) return;
        var transaction = JsonSerializer.Deserialize<Transaction>(LocalBackupProtection.Unprotect(File.ReadAllBytes(_journalPath)))
            ?? throw new InvalidDataException("Diario di recupero non leggibile.");
        if (string.IsNullOrWhiteSpace(transaction.Game) || string.IsNullOrWhiteSpace(transaction.Statistics))
            throw new InvalidDataException("Diario di recupero incompleto.");
        // Check both payloads before replacing either file; destinations are a fixed pair.
        var game = JsonSerializer.Deserialize<SavedGame>(transaction.Game) ?? throw new InvalidDataException();
        _ = ReadStatistics(transaction.Statistics);
        if (game.FormatVersion != 2 || game.Solution is not null || game.Bonus is null || game.Bonus.Solution is not null)
            throw new InvalidDataException("Formato del diario non riconosciuto.");
        ApplyTransaction(transaction);
    }

    private void ApplyTransaction(Transaction transaction)
    {
        AtomicWrite(_gamePath, System.Text.Encoding.UTF8.GetBytes(transaction.Game), "game");
        CommitCheckpoint?.Invoke("game-applied");
        AtomicWrite(_statsPath, System.Text.Encoding.UTF8.GetBytes(transaction.Statistics), "statistics");
        CommitCheckpoint?.Invoke("statistics-applied");
        RetryFileOperation(() => File.Delete(_journalPath));
        CommitCheckpoint?.Invoke("completed");
    }

    private static void AtomicWrite(string path, byte[] bytes, string phase)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            CommitCheckpoint?.Invoke(phase + "-flushed");
            // Both names are in the same directory: Windows replaces the name without copying file contents.
            RetryFileOperation(() => File.Move(temporary, path, overwrite: true));
        }
        finally { if (File.Exists(temporary)) RetryFileOperation(() => File.Delete(temporary)); }
    }

    private static void RetryFileOperation(Action operation)
    {
        // Short-lived Windows file handles can delay a rename. Persistent failures still reach the recovery warning.
        for (var attempt = 0; ; attempt++)
        {
            try { operation(); return; }
            catch (Exception error) when (attempt < 5 && (error is IOException or UnauthorizedAccessException) &&
                (error.HResult & 0xffff) is 5 or 32 or 33 or 1175)
            {
                Thread.Sleep(25 << attempt);
            }
        }
    }

    private void Guard(Action operation)
    {
        lock (_sync)
        {
            if (IsFaulted) throw new StorageFailureException(new InvalidOperationException("Archivio sospeso."));
            try { operation(); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or System.ComponentModel.Win32Exception)
            {
                IsFaulted = true;
                throw new StorageFailureException(error);
            }
        }
    }

    private static Statistics ReadStatistics(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("History", out var history) || history.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Statistiche incomplete: storico non leggibile.");
        var statistics = JsonSerializer.Deserialize<Statistics>(text) ?? throw new InvalidDataException();
        if (statistics.History is null || statistics.History.Any(entry => entry is null) || statistics.WinDistribution is null || statistics.InfiniteWinDistribution is null)
            throw new InvalidDataException("Statistiche incomplete.");
        return statistics;
    }

    private bool IsPresent(string path)
    {
        var present = false;
        Guard(() => present = Exists(path));
        return present;
    }

    private static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private sealed record Transaction(string Game, string Statistics);
}
