using System.IO;
using WordleItaliano.Models;
using WordleItaliano.Services;

namespace WordleItaliano.ViewModels;

public sealed partial class MainViewModel
{
    private SavedGame? _preparedSavedGame;
    private bool _savedGameBlocked;
    private string _savedGameWarning = string.Empty;
    private int? _verifiedSavedStreak;

    private void PrepareSavedGame()
    {
        PcAuthorization.RequireOfficial();
        var saved = _storage.LoadGame();
        if (saved is null && !_storage.GameExists) return;
        if (saved?.RecoveryRequired == true)
        {
            BlockSavedGame("Questa partita è in attesa di recupero dalla copia conservata.");
            return;
        }
        string? backup = null;
        try
        {
            if (saved is null || saved.FormatVersion != 2)
            {
                backup = _storage.CreateRecoveryBackup();
            }
            var validation = saved is null
                ? new SavedGameValidation(false, _storage.GameReadError ?? "Il salvataggio non è leggibile.")
                : new SavedGameValidator(_repository, _dailyWordService).Validate(saved);
            if (validation.IsValid && saved!.GameDate == _todayKey)
            {
                var dailyRecorded = Statistics.History.Any(e => e.Date == _todayKey && !e.IsBonus && !e.IsInfinite);
                var bonusRecorded = Statistics.History.Any(e => e.Date == _todayKey && e.IsBonus && !e.IsInfinite);
                if (dailyRecorded && saved.Status == GameStatus.Playing || bonusRecorded && saved.Bonus.Status == GameStatus.Playing)
                    validation = new(false, "La partita in corso non corrisponde al risultato già presente nello storico.");
            }
            if (!validation.IsValid)
            {
                backup ??= _storage.CreateRecoveryBackup();
                _storage.SaveGame(new SavedGame { GameDate = _todayKey, Date = _todayKey, RecoveryRequired = true, RecoveryBackup = backup,
                    Bonus = new BonusGame { WordLength = _bonusWordLength } });
                BlockSavedGame(validation.Message);
                return;
            }
            _preparedSavedGame = saved;
            if (saved!.GameDate == _todayKey)
                SetSolutionsForDate(DateOnly.FromDateTime(DateTime.Today), saved.SequenceId ?? OfficialSequence.Legacy);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            BlockSavedGame("Non è stato possibile conservare una copia sicura dei dati. Il salvataggio originale non è stato sostituito.");
        }
    }

    private void BlockSavedGame(string reason)
    {
        _savedGameBlocked = true;
        _savedGameWarning = reason + " La partita è sospesa per evitare di perdere dati o registrare un risultato errato. Conserva i dati e chiedi assistenza per il recupero.";
    }

    private void ReconcileSavedCompetitiveResults()
    {
        var streak = 1;
        var cursor = DateOnly.Parse(_todayKey);
        while (IsConsecutiveStreakDate(cursor.AddDays(-1), cursor))
        {
            cursor = cursor.AddDays(-1);
            var previous = Statistics.History.LastOrDefault(e => e.Date == cursor.ToString("yyyy-MM-dd") && !e.IsBonus && !e.IsInfinite);
            if (previous?.Won != true) break;
            streak++;
        }
        _verifiedSavedStreak = streak;
        if (_dailyStatus != GameStatus.Playing)
        {
            var old = Statistics.History.LastOrDefault(e => e.Date == _todayKey && !e.IsBonus && !e.IsInfinite);
            var entry = CreateDailyHistoryEntry(_dailyStatus == GameStatus.Won, _dailyGuesses.Count);
            PreserveEarnedPrize(entry, old);
            Statistics.Played += old is null ? 1 : 0;
            Statistics.Won += (entry.Won ? 1 : 0) - (old?.Won == true ? 1 : 0);
            if (old?.Won == true && old.Attempts is >= 1 and <= 6) Statistics.WinDistribution[old.Attempts - 1] = Math.Max(0, Statistics.WinDistribution[old.Attempts - 1] - 1);
            if (entry.Won) Statistics.WinDistribution[entry.Attempts - 1]++;
            ReplaceSavedResult(entry);
            Statistics.LastPlayedDate = _todayKey;
            Statistics.CurrentStreak = entry.Won ? streak : 0;
            if (entry.Won) { Statistics.LastWinDate = _todayKey; Statistics.BestStreak = Math.Max(Statistics.BestStreak, streak); }
            _dailyStatisticsAlreadyRecorded = true;
        }
        if (_bonusStatus != GameStatus.Playing)
        {
            var old = Statistics.History.LastOrDefault(e => e.Date == _todayKey && e.IsBonus && !e.IsInfinite);
            var entry = CreateBonusHistoryEntry(_bonusStatus == GameStatus.Won, _bonusGuesses.Count);
            PreserveEarnedPrize(entry, old);
            Statistics.BonusPlayed += old is null ? 1 : 0;
            Statistics.BonusWon += (entry.Won ? 1 : 0) - (old?.Won == true ? 1 : 0);
            Statistics.TwoPointDays += (entry.Won && _dailyStatus == GameStatus.Won ? 1 : 0) - (old?.Won == true && _dailyStatus == GameStatus.Won ? 1 : 0);
            ReplaceSavedResult(entry);
        }
        NormalizeStatistics();
        RefreshStatisticsView();
        RefreshHistoryView();
        RefreshWrappedView();
    }

    private void ReplaceSavedResult(GameHistoryEntry entry)
    {
        var index = Statistics.History.FindIndex(e => e.Date == _todayKey && e.IsBonus == entry.IsBonus && !e.IsInfinite);
        if (index < 0) Statistics.History.Add(entry);
        else Statistics.History[index] = entry;
    }

    private static void PreserveEarnedPrize(GameHistoryEntry entry, GameHistoryEntry? previous)
    {
        if (!entry.IsPerfectShot || previous?.IsPerfectShot != true || !string.IsNullOrWhiteSpace(entry.PerfectShotPrizeText)) return;
        entry.PerfectShotPrizeText = previous.PerfectShotPrizeText;
        entry.IsPerfectShotPrizePending = previous.IsPerfectShotPrizePending;
    }
}
