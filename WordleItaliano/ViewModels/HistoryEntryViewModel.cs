namespace WordleItaliano.ViewModels;

public sealed class HistoryEntryViewModel : ObservableObject
{
    public HistoryEntryViewModel(string date, string solution, string result, string attempts, string guesses, string mode, string points, string timeText, string shareText)
    {
        Date = date;
        Solution = solution;
        Result = result;
        Attempts = attempts;
        Guesses = guesses;
        Mode = mode;
        Points = points;
        IsPointsVisible = !string.IsNullOrWhiteSpace(points);
        ResultSummary = $"{result} {attempts}";
        GuessesSummary = $"Tentativi: {guesses}";
        ResultTone = result == "Vinta" ? "Completata" : "Non indovinata";
        TimeText = timeText;
        IsTimeVisible = !string.IsNullOrWhiteSpace(timeText);
        ShareText = shareText;
    }

    public string Date { get; }
    public string Solution { get; }
    public string Result { get; }
    public string Attempts { get; }
    public string Mode { get; }
    public string Points { get; }
    public bool IsPointsVisible { get; }
    public string ResultSummary { get; }
    public string ResultTone { get; }
    public string TimeText { get; }
    public bool IsTimeVisible { get; }
    public string GuessesSummary { get; }
    public string Guesses { get; }
    public string ShareText { get; }
}
