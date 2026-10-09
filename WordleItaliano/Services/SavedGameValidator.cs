using WordleItaliano.Models;

namespace WordleItaliano.Services;

public sealed class SavedGameValidator(WordRepository repository, DailyWordService words)
{
#if WORDLE_TEST_BUILD
    public static int InvocationCount { get; private set; }
#endif
    public SavedGameValidation Validate(SavedGame game)
    {
#if WORDLE_TEST_BUILD
        InvocationCount++;
#endif
        PcAuthorization.RequireOfficial();
        if (game.RecoveryRequired) return new(false, "Questa partita è in attesa di recupero dalla copia conservata.");
        var dateText = string.IsNullOrWhiteSpace(game.GameDate) ? game.Date : game.GameDate;
        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", out var date) || game.FormatVersion is not (0 or 2))
            return new(false, "Il formato o la data del salvataggio non sono riconosciuti.");
        if (date > DateOnly.FromDateTime(DateTime.Today) || !OfficialSequence.IsKnown(game.SequenceId))
            return new(false, "Data futura o sequenza sconosciuta: il salvataggio deve essere verificato senza sostituire la sfida.");
        var sequence = game.SequenceId ?? OfficialSequence.Legacy;
        if (sequence == OfficialSequence.Next && OfficialSequence.ForDate(date) != OfficialSequence.Next)
            return new(false, "La sequenza salvata non e ancora attiva per questa data. Conserva il salvataggio per la verifica.");
        var daily = words.GetWordForDate(date, sequence);
        var bonus = words.GetBonusWordForDate(date, sequence);
        if (game.Bonus is null || game.Infinite is null || game.WordLength != 5 || game.Bonus.WordLength != bonus.Length ||
            !MatchesLegacySolution(game.Solution, daily) || !MatchesLegacySolution(game.Bonus.Solution, bonus.Word))
            return new(false, "La sfida salvata non corrisponde alla parola e alla lunghezza previste per quel giorno.");
        if (!TryReplay(game.Guesses, daily, out var dailyStatus) || !TryReplay(game.Bonus.Guesses, bonus.Word, out var bonusStatus))
            return new(false, "I tentativi salvati non formano una partita valida.");
        if (!MatchesStatus(game.Status, dailyStatus) || !MatchesStatus(game.Bonus.Status, bonusStatus) ||
            game.DailyElapsedSeconds < 0 || game.Bonus.ElapsedSeconds < 0 ||
            (game.Bonus.IsUnlocked || game.Bonus.Guesses.Count > 0) && dailyStatus != GameStatus.Won)
            return new(false, "Lo stato della partita o lo sblocco del Bonus non corrispondono ai tentativi salvati.");
        var dailyPerfect = dailyStatus == GameStatus.Won && game.Guesses.Count == 1;
        var bonusPerfect = bonusStatus == GameStatus.Won && game.Bonus.Guesses.Count == 1;
        if ((game.DailyPerfectShot?.IsPerfectShot == true && !dailyPerfect) ||
            (game.Bonus.PerfectShot?.IsPerfectShot == true && !bonusPerfect))
            return new(false, "Il Colpo Perfetto dichiarato non corrisponde ai tentativi salvati.");
        game.GameDate = game.Date = date.ToString("yyyy-MM-dd");
        game.Status = dailyStatus;
        game.Bonus.Status = bonusStatus;
        game.Bonus.IsUnlocked = dailyStatus == GameStatus.Won;
        game.DailyPerfectShot = RebuildPerfectShot(game.DailyPerfectShot, dailyPerfect);
        game.Bonus.PerfectShot = RebuildPerfectShot(game.Bonus.PerfectShot, bonusPerfect);
        game.FormatVersion = 2;
        game.Solution = null;
        game.Bonus.Solution = null;
        return new(true, string.Empty);
    }

    private static bool MatchesLegacySolution(string? saved, string expected) =>
        string.IsNullOrWhiteSpace(saved) || WordRepository.Normalize(saved) == expected;

    // Playing may be an interrupted save written immediately before recording the result.
    private static bool MatchesStatus(GameStatus declared, GameStatus actual) =>
        declared == actual || declared == GameStatus.Playing && actual is GameStatus.Won or GameStatus.Lost;

    private bool TryReplay(List<string>? guesses, string solution, out GameStatus status)
    {
        status = GameStatus.Playing;
        if (guesses is null || guesses.Count > 6) return false;
        for (var index = 0; index < guesses.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(guesses[index])) return false;
            var guess = WordRepository.Normalize(guesses[index]);
            if (status != GameStatus.Playing || guess.Length != solution.Length || !repository.IsValid(guess)) return false;
            guesses[index] = guess;
            if (GuessEvaluator.Evaluate(guess, solution).All(state => state == TileState.Correct)) status = GameStatus.Won;
            else if (index == 5) status = GameStatus.Lost;
        }
        return true;
    }

    private static PerfectShotState RebuildPerfectShot(PerfectShotState? saved, bool earned)
    {
        var result = earned ? saved?.Clone() ?? new PerfectShotState() : new PerfectShotState();
        result.IsPerfectShot = earned;
        return result;
    }
}

public sealed record SavedGameValidation(bool IsValid, string Message);
