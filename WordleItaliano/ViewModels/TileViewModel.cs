using WordleItaliano.Models;

namespace WordleItaliano.ViewModels;

public sealed class TileViewModel : ObservableObject
{
    private int _index;
    private string _letter = string.Empty;
    private TileState _state = TileState.Empty;
    private double _size = 62;
    private bool _isSelected;

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public string Letter
    {
        get => _letter;
        set => SetProperty(ref _letter, value);
    }

    public TileState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public double Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
