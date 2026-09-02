using WordleItaliano.Models;

namespace WordleItaliano.ViewModels;

public sealed class KeyboardKeyViewModel : ObservableObject
{
    private TileState _state = TileState.Empty;

    public KeyboardKeyViewModel(string label)
    {
        Label = label;
    }

    public string Label { get; }

    public TileState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }
}
