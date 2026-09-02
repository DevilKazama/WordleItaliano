namespace WordleItaliano.ViewModels;

public sealed class StatCardViewModel : ObservableObject
{
    private string _value = "0";

    public StatCardViewModel(string label)
    {
        Label = label;
    }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
