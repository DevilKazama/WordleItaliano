using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;
using WordleItaliano.Models;
using WordleItaliano.Services;

namespace WordleItaliano.ViewModels;

public sealed partial class MainViewModel
{
    private bool _historyOnlyFavorites;
    private DateTime _favoritesAnimationUntil;
    public event EventHandler? FavoriteInserted;
    public event EventHandler? FavoriteFormClosed;
    private bool _isManualFavoriteOpen;
    private string _manualFavoriteText = string.Empty;
    private string _manualFavoriteMessage = string.Empty;
    private DispatcherTimer? _favoriteMessageTimer;
    public ICommand OpenFavoriteFormCommand { get; private set; } = null!;
    public ICommand ConfirmManualFavoriteCommand { get; private set; } = null!;
    public ICommand CancelFavoriteFormCommand { get; private set; } = null!;
    public bool IsManualFavoriteOpen
    {
        get => _isManualFavoriteOpen;
        private set { if (SetProperty(ref _isManualFavoriteOpen, value)) RefreshFavoriteInputState(); }
    }
    public string ManualFavoriteText
    {
        get => _manualFavoriteText;
        set { if (SetProperty(ref _manualFavoriteText, value)) ManualFavoriteMessage = string.Empty; }
    }
    public string ManualFavoriteMessage
    {
        get => _manualFavoriteMessage;
        private set { if (SetProperty(ref _manualFavoriteMessage, value)) OnPropertyChanged(nameof(HasManualFavoriteMessage)); }
    }
    public bool HasManualFavoriteMessage => !string.IsNullOrEmpty(ManualFavoriteMessage);
    private void OpenFavoriteForm()
    {
        _favoriteMessageTimer?.Stop();
        ManualFavoriteMessage = string.Empty;
        IsManualFavoriteOpen = true;
    }
    public void CancelFavoriteForm()
    {
        if (!IsManualFavoriteOpen) return;
        ManualFavoriteMessage = string.Empty;
        IsManualFavoriteOpen = false;
        FavoriteFormClosed?.Invoke(this, EventArgs.Empty);
    }
    private void ConfirmManualFavorite()
    {
        if (!IsManualFavoriteOpen) return;
        var word = WordRepository.Normalize(ManualFavoriteText);
        if (word.Length is < 5 or > 7) { ManualFavoriteMessage = "Scrivi una parola da 5, 6 o 7 lettere."; return; }
        if (!_repository.IsValid(word)) { ManualFavoriteMessage = "La parola non è presente nel dizionario."; return; }
        if (IsFavorite(word)) { ManualFavoriteMessage = "Questa parola è già nei preferiti."; return; }
        if (!AddFavorite(word)) return;
        IsManualFavoriteOpen = false;
        ManualFavoriteText = string.Empty;
        ManualFavoriteMessage = word.Length == _currentWordLength ? "Parola aggiunta ai preferiti."
            : $"Parola aggiunta: sarà visibile nelle partite da {word.Length} lettere.";
        _favoriteMessageTimer?.Stop();
        _favoriteMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _favoriteMessageTimer.Tick += (_, _) => { _favoriteMessageTimer.Stop(); ManualFavoriteMessage = string.Empty; };
        _favoriteMessageTimer.Start();
        FavoriteFormClosed?.Invoke(this, EventArgs.Empty);
    }
    public ICommand SetFavoritesSideCommand { get; private set; } = null!;
    public bool IsFavoritesRight => FavoritesPanelSide == "Destra";
    public bool IsFavoritesLeft => !IsFavoritesRight;
    public string FavoritesPanelSide
    {
        get => _userSettings.FavoritesPanelSide == "Sinistra" ? "Sinistra" : "Destra";
        set { if (value is not ("Destra" or "Sinistra") || value == FavoritesPanelSide) return; _userSettings.FavoritesPanelSide = value; SaveFavorites(); OnPropertyChanged(); RefreshFavoritesLayout(); }
    }
    public HorizontalAlignment FavoritesTabAlignment => FavoritesPanelSide == "Destra" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public string FavoritesTabArrow => (FavoritesPanelSide == "Destra") != IsFavoritesDocked ? ">" : "<";
    public bool IsFavoritesAvailable => !IsSplashVisible;
    public bool IsFavoritesDocked => IsFavoritesAvailable && IsFavoritesPanelOpen;
    public string FavoritesToggleTooltip => IsFavoritesDocked ? "Chiudi preferiti" : "Apri preferiti";
    private void RefreshFavoritesLayout()
    {
        OnPropertyChanged(nameof(IsFavoritesDocked));
        OnPropertyChanged(nameof(IsFavoritesAvailable));
        OnPropertyChanged(nameof(FavoritesToggleTooltip));
        OnPropertyChanged(nameof(FavoritesTabAlignment));
        OnPropertyChanged(nameof(FavoritesTabArrow));
        OnPropertyChanged(nameof(IsFavoritesRight));
        OnPropertyChanged(nameof(IsFavoritesLeft));
    }
    public ObservableCollection<FavoriteWordViewModel> FavoriteWords { get; } = [];
    public ICommand ToggleFavoriteCommand { get; private set; } = null!;
    public ICommand InsertFavoriteCommand { get; private set; } = null!;
    public ICommand AddCurrentFavoriteCommand { get; private set; } = null!;
    public ICommand RemoveFavoriteCommand { get; private set; } = null!;
    public ICommand ShowAllFavoritesCommand { get; private set; } = null!;
    public ICommand ToggleFavoritesPanelCommand { get; private set; } = null!;
    public bool HistoryOnlyFavorites
    {
        get => _historyOnlyFavorites;
        set { if (SetProperty(ref _historyOnlyFavorites, value)) RefreshHistoryView(); }
    }
    public bool IsFavoritesPanelOpen
    {
        get => _userSettings.FavoritesPanelOpen;
        set { if (_userSettings.FavoritesPanelOpen == value) return; _userSettings.FavoritesPanelOpen = value; if (!value) CancelFavoriteForm(); SaveFavorites(); OnPropertyChanged(); RefreshFavoritesLayout(); }
    }
    public bool HideFavoritesWithAbsentLetters
    {
        get => _userSettings.HideFavoritesWithAbsentLetters;
        set { _userSettings.HideFavoritesWithAbsentLetters = value; SaveFavorites(); OnPropertyChanged(); RefreshFavorites(); }
    }
    public string FavoritesEmptyText => _userSettings.FavoriteWords.Count == 0
        ? "Aggiungi una parola qui o con la stella nello Storico."
        : !_userSettings.FavoriteWords.Any(word => word.Length == _currentWordLength)
        ? $"Non hai ancora preferiti da {_currentWordLength} lettere."
        : "Tutte le parole di questa lunghezza sono escluse dal filtro.";
    public bool CanShowAllFavorites => IsFavoritesEmpty && HideFavoritesWithAbsentLetters &&
        _userSettings.FavoriteWords.Any(word => word.Length == _currentWordLength);
    public bool IsFavoritesEmpty => FavoriteWords.Count == 0;
    public bool CanUseFavoriteWord => CanAcceptGameInput;
    private bool HasActiveGuessRow => CurrentStatus == GameStatus.Playing && _currentRow is >= 0 and < 6 &&
        _currentWordLength > 0 && Tiles is not null && (_currentRow + 1) * _currentWordLength <= Tiles.Count;
    private bool CanAcceptGameInput => !_savedGameBlocked && !_storage.IsFaulted && HasActiveGuessRow && !IsManualFavoriteOpen && DateTime.UtcNow >= _favoritesAnimationUntil &&
        !IsBonusPromptVisible && !IsStatisticsVisible && !IsHistoryVisible && !IsHelpVisible && !IsWrappedVisible &&
        !IsMonthlyRecapVisible && !IsResetConfirmVisible && !IsSettingsVisible && !IsUpdateDialogVisible &&
        !IsProfileDialogVisible && !IsChangelogVisible && !IsPerfectShotCelebrationVisible && !IsPerfectShotPrizeDialogVisible;
    public bool CanFavoriteCurrentWord => CanAcceptGameInput && GetCurrentGuess().Length == _currentWordLength &&
        _repository.IsValid(WordRepository.Normalize(GetCurrentGuess())) && !IsFavorite(GetCurrentGuess());
    public string SaveFavoriteText => HasActiveGuessRow && GetCurrentGuess().Length == _currentWordLength && IsFavorite(GetCurrentGuess())
        ? "Parola già salvata" : "Salva parola della riga";
    public string SaveFavoriteTooltip => CurrentStatus != GameStatus.Playing
        ? "La partita è conclusa."
        : !CanAcceptGameInput ? "Attendi che il gioco accetti nuovamente input."
        : GetCurrentGuess().Length != _currentWordLength ? "Completa la parola nella riga attiva."
        : !_repository.IsValid(WordRepository.Normalize(GetCurrentGuess())) ? "La parola non è valida per questa modalità."
        : IsFavorite(GetCurrentGuess()) ? "Questa parola è già nei preferiti."
        : "Aggiungi la parola della riga attiva ai preferiti.";
    public string FavoriteInsertionStatus => CurrentStatus != GameStatus.Playing
        ? "Partita conclusa. Puoi consultare e riordinare i preferiti" : string.Empty;

    private void InitializeFavorites()
    {
        _userSettings.FavoriteWords = (_userSettings.FavoriteWords ?? []).Where(word => !string.IsNullOrWhiteSpace(word)).Select(WordRepository.Normalize)
            .Where(word => word.Length is >= 5 and <= 7).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        ToggleFavoriteCommand = new RelayCommand(value => ToggleFavorite(value?.ToString()));
        InsertFavoriteCommand = new RelayCommand(value => InsertFavorite(value?.ToString()), _ => CanUseFavoriteWord);
        AddCurrentFavoriteCommand = new RelayCommand(_ => { if (CanFavoriteCurrentWord) AddFavorite(GetCurrentGuess()); }, _ => CanFavoriteCurrentWord);
        OpenFavoriteFormCommand = new RelayCommand(_ => OpenFavoriteForm());
        ConfirmManualFavoriteCommand = new RelayCommand(_ => ConfirmManualFavorite());
        CancelFavoriteFormCommand = new RelayCommand(_ => CancelFavoriteForm());
        RemoveFavoriteCommand = new RelayCommand(value => { if (value is string word && IsFavorite(word)) ToggleFavorite(word); });
        ShowAllFavoritesCommand = new RelayCommand(_ => HideFavoritesWithAbsentLetters = false);
        ToggleFavoritesPanelCommand = new RelayCommand(_ => IsFavoritesPanelOpen = !IsFavoritesPanelOpen);
        SetFavoritesSideCommand = new RelayCommand(value => { if (value is string side) FavoritesPanelSide = side; });
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsSplashVisible)) { if (IsSplashVisible) CancelFavoriteForm(); RefreshFavoritesLayout(); }
            if (args.PropertyName == nameof(BoardColumns)) RefreshFavorites();
            if (args.PropertyName?.EndsWith("Visible", StringComparison.Ordinal) == true)
            {
                RefreshFavoriteInputState();
            }
        };
        // Queue one refresh after the keyboard has received every letter of a guess.
        RevealRequested += (_, _) => BlockFavoriteInsertion(_currentWordLength * 110 + 260);
        ShakeRequested += (_, _) => BlockFavoriteInsertion(330);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            foreach (var key in KeyboardRows.SelectMany(row => row))
                key.PropertyChanged += (_, _) => QueueFavoritesRefresh();
            RefreshFavorites();
        }));
    }
    private void BlockFavoriteInsertion(int milliseconds)
    {
        _favoritesAnimationUntil = new[] { _favoritesAnimationUntil, DateTime.UtcNow.AddMilliseconds(milliseconds) }.Max();
        RefreshFavorites();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds + 10) };
        timer.Tick += (_, _) => { timer.Stop(); RefreshFavorites(); };
        timer.Start();
    }
    private bool _favoritesRefreshQueued;
    private void QueueFavoritesRefresh()
    {
        if (_favoritesRefreshQueued) return;
        _favoritesRefreshQueued = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        { _favoritesRefreshQueued = false; RefreshFavorites(); }));
    }
    private bool IsFavorite(string word) => _userSettings.FavoriteWords.Contains(WordRepository.Normalize(word), StringComparer.OrdinalIgnoreCase);
    private void SaveFavorites() => _storage.SaveUserSettings(_userSettings);
    private void ToggleFavorite(string? word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = WordRepository.Normalize(word);
        if (IsFavorite(word)) _userSettings.FavoriteWords.RemoveAll(item => string.Equals(item, word, StringComparison.OrdinalIgnoreCase));
        else { AddFavorite(word); return; }
        PublishFavoritesChange();
    }
    private bool AddFavorite(string word)
    {
        word = WordRepository.Normalize(word);
        if (word.Length is < 5 or > 7 || !_repository.IsValid(word) || IsFavorite(word)) return false;
        _userSettings.FavoriteWords.Add(word);
        PublishFavoritesChange();
        return true;
    }
    public string? GetSubmittedFavoriteWord(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= Tiles.Count || _currentWordLength <= 0 || DateTime.UtcNow < _favoritesAnimationUntil) return null;
        var row = Tiles.Skip(tileIndex / _currentWordLength * _currentWordLength).Take(_currentWordLength).ToArray();
        if (row.Length != _currentWordLength || row.Any(tile => tile.State is not (TileState.Correct or TileState.Present or TileState.Absent))) return null;
        return WordRepository.Normalize(string.Concat(row.Select(tile => tile.Letter)));
    }
    public bool SaveSubmittedFavorite(string word)
    {
        word = WordRepository.Normalize(word);
        if (!Enumerable.Range(0, Tiles.Count).Any(index => GetSubmittedFavoriteWord(index) == word)) return false;
        if (IsFavorite(word)) { ManualFavoriteMessage = "Questa parola è già nei preferiti."; return false; }
        var added = AddFavorite(word);
        if (added) ManualFavoriteMessage = "Parola aggiunta ai preferiti.";
        _favoriteMessageTimer?.Stop();
        _favoriteMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _favoriteMessageTimer.Tick += (_, _) => { _favoriteMessageTimer.Stop(); ManualFavoriteMessage = string.Empty; };
        _favoriteMessageTimer.Start();
        return added;
    }
    private void PublishFavoritesChange()
    {
        SaveFavorites();
        foreach (var row in HistoryRows) row.IsFavorite = IsFavorite(row.Solution);
        if (HistoryOnlyFavorites) RefreshHistoryView();
        RefreshFavorites();
    }
    private void RefreshFavorites()
    {
        if (KeyboardRows is null) return;
        var states = KeyboardRows.SelectMany(row => row).ToDictionary(key => key.Label[0], key => key.State);
        FavoriteWords.Clear();
        foreach (var word in _userSettings.FavoriteWords.Where(word => word.Length == _currentWordLength))
        {
            var upper = word.ToUpperInvariant();
            if (HideFavoritesWithAbsentLetters && upper.Any(letter => states.GetValueOrDefault(letter) == TileState.Absent)) continue;
            FavoriteWords.Add(new FavoriteWordViewModel(upper, upper.Select(letter => new KeyboardKeyViewModel(letter.ToString())
                { State = states.GetValueOrDefault(letter) }).ToArray()));
        }
        OnPropertyChanged(nameof(IsFavoritesEmpty));
        OnPropertyChanged(nameof(FavoritesEmptyText));
        OnPropertyChanged(nameof(CanShowAllFavorites));
        RefreshFavoriteInputState();
    }
    private void RefreshFavoriteInputState()
    {
        OnPropertyChanged(nameof(CanFavoriteCurrentWord));
        OnPropertyChanged(nameof(CanUseFavoriteWord));
        OnPropertyChanged(nameof(SaveFavoriteText));
        OnPropertyChanged(nameof(SaveFavoriteTooltip));
        OnPropertyChanged(nameof(FavoriteInsertionStatus));
        (InsertFavoriteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddCurrentFavoriteCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
    private void InsertFavorite(string? word)
    {
        if (!CanAcceptGameInput || EnsureCurrentGame() || !CanAcceptGameInput || string.IsNullOrWhiteSpace(word)) return;
        word = WordRepository.Normalize(word);
        if (word.Length != _currentWordLength || !_repository.IsValid(word) || !IsFavorite(word)) return;
        for (var column = 0; column < _currentWordLength; column++)
        { Tiles[CurrentTileIndex(column)].Letter = string.Empty; Tiles[CurrentTileIndex(column)].State = TileState.Empty; }
        MoveSelectionTo(0);
        foreach (var letter in word) HandleInput(letter.ToString());
        FavoriteInserted?.Invoke(this, EventArgs.Empty);
    }
    public void MoveFavorite(string source, string target)
    {
        source = WordRepository.Normalize(source); target = WordRepository.Normalize(target);
        var from = _userSettings.FavoriteWords.IndexOf(source);
        var to = _userSettings.FavoriteWords.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return;
        _userSettings.FavoriteWords.RemoveAt(from);
        _userSettings.FavoriteWords.Insert(to, source);
        SaveFavorites(); RefreshFavorites();
    }
    public void MoveFavoriteToSlot(string source, int slot)
    {
        source = WordRepository.Normalize(source);
        var visible = FavoriteWords.Select(word => WordRepository.Normalize(word.Word)).ToList();
        var sourceIndex = visible.IndexOf(source);
        if (sourceIndex < 0 || slot < 0 || slot > visible.Count) return;
        if (slot == sourceIndex || slot == sourceIndex + 1) return;
        visible.RemoveAt(sourceIndex);
        if (sourceIndex < slot) slot--;
        if (visible.Count == 0) return;
        _userSettings.FavoriteWords.Remove(source);
        var anchor = slot < visible.Count ? visible[slot] : visible[^1];
        var index = _userSettings.FavoriteWords.IndexOf(anchor) + (slot == visible.Count ? 1 : 0);
        _userSettings.FavoriteWords.Insert(index, source);
        SaveFavorites(); RefreshFavorites();
    }
}

public sealed record FavoriteWordViewModel(string Word, IReadOnlyList<KeyboardKeyViewModel> Letters);
