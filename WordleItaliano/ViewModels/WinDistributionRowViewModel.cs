namespace WordleItaliano.ViewModels;

public sealed class WinDistributionRowViewModel : ObservableObject
{
    private int _wins;
    private double _barWidth;

    public WinDistributionRowViewModel(int attempt)
    {
        Attempt = attempt;
    }

    public int Attempt { get; }

    public int Wins
    {
        get => _wins;
        set => SetProperty(ref _wins, value);
    }

    public double BarWidth
    {
        get => _barWidth;
        set => SetProperty(ref _barWidth, value);
    }
}
