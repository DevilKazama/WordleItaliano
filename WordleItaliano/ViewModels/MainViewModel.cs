using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Velopack;
using WordleItaliano.Models;
using WordleItaliano.Services;

namespace WordleItaliano.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int DataMigrationVersion = 2;
    private static readonly DateOnly OfficialStartDate = new(2026, 9, 1);
    private readonly WordRepository _repository;
    private readonly DailyWordService _dailyWordService;
    private readonly StorageService _storage;
    private readonly AppSettings _settings;
    private readonly AppUpdateService _updateService;
    private readonly UserSettings _userSettings;
    private readonly ChangelogService _changelogService;
    private UpdateInfo? _pendingUpdate;
    private string _dailySolution = string.Empty;
    private string _bonusSolution = string.Empty;
    private string _infiniteSolution = string.Empty;
    private int _bonusWordLength;
    private string _todayKey;
    private readonly List<string> _dailyGuesses = [];
    private readonly List<string> _bonusGuesses = [];
    private readonly List<string> _infiniteGuesses = [];
    private int _currentRow;
    private int _currentWordLength = 5;
    private int _selectedColumn;
    private double _boardWidth = 350;
    private string _currentSolution = string.Empty;
    private GameStatus _dailyStatus = GameStatus.Playing;
    private GameStatus _bonusStatus = GameStatus.Playing;
    private GameStatus _infiniteStatus = GameStatus.Playing;
    private string _message = "Indovina la parola di oggi.";
    private bool _isBonusActive;
    private bool _isInfiniteActive;
    private bool _isBonusUnlocked;
    private bool _isBonusPromptVisible;
    private bool _isDarkTheme;
    private bool _isSplashVisible = true;
    private bool _isVirtualKeyboardVisible = true;
    private bool _isStatisticsVisible;
    private bool _isHistoryVisible;
    private bool _isHelpVisible;
    private bool _isCurrentResultCopyVisible;
    private bool _isBonusViewButtonVisible;
    private bool _isDailyViewButtonVisible;
    private bool _isNewInfiniteButtonVisible;
    private bool _isScoreLineVisible;
    private bool _dailyStatisticsAlreadyRecorded;
    private bool _isClipboardCopyRunning;
    private int _dailyElapsedSeconds;
    private bool _dailyTimerStarted;
    private DateTime? _dailyTimerStartedAt;
    private int _bonusElapsedSeconds;
    private bool _bonusTimerStarted;
    private DateTime? _bonusTimerStartedAt;
    private int _infiniteElapsedSeconds;
    private bool _infiniteTimerStarted;
    private DateTime? _infiniteTimerStartedAt;
    private string _dailyTimerText = "00:00";
    private bool _isDailyTimerVisible = true;
    private string _toastMessage = string.Empty;
    private bool _isToastVisible;
    private int _toastVersion;
    private string _historyFilter = "Tutte";
    private string _historyFilterLabel = "Filtro: Tutte";
    private string _historyEmptyMessage = string.Empty;
    private bool _isHistoryEmptyVisible;
    private bool _isWrappedVisible;
    private bool _isMonthlyRecapVisible;
    private bool _isResetConfirmVisible;
    private bool _isSettingsVisible;
    private bool _isUpdateDialogVisible;
    private bool _isUpdateBusy;
    private bool _isUpdateStatusVisible;
    private bool _isProfileDialogVisible;
    private bool _isChangelogVisible;
    private bool _updatePromptShownThisSession;
    private string _wrappedMode = "Mese";
    private DateOnly _wrappedPeriod = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private string _wrappedPeriodLabel = string.Empty;
    private string _wrappedMonthlyScoresText = string.Empty;
    private string _monthlyRecapTitle = string.Empty;
    private string _scoreLineText = string.Empty;
    private string _modeBadgeText = "Giornaliera";
    private string _modeBadgeDetail = "Sfida quotidiana";
    private string _updateStatusText = string.Empty;
    private string _updateDialogTitle = string.Empty;
    private string _updateDialogMessage = string.Empty;
    private string _updateReleaseNotes = string.Empty;
    private string _updateProgressText = string.Empty;
    private int _updateProgressValue;
    private string _availableVersionText = string.Empty;
    private string _playerName = string.Empty;
    private string _profileNameDraft = string.Empty;
    private string _profileError = string.Empty;
    private string _changelogTitle = string.Empty;
    private string _changelogText = string.Empty;

    public MainViewModel()
    {
        _settings = LoadSettings();
        _repository = new WordRepository();
        _dailyWordService = new DailyWordService(_repository, _settings);
        _storage = new StorageService();
        var userSettingsExists = _storage.UserSettingsExists;
        _userSettings = _storage.LoadUserSettings();
        _updateService = new AppUpdateService(_settings.UpdateRepositoryUrl);
        _changelogService = new ChangelogService();
        PlayerName = _userSettings.PlayerName.Trim();
        ProfileNameDraft = PlayerName;
        IsProfileDialogVisible = string.IsNullOrWhiteSpace(PlayerName);
        _todayKey = _dailyWordService.TodayKey;
        SetSolutionsForDate(DateOnly.FromDateTime(DateTime.Today));
        _currentSolution = _dailySolution;

        Tiles = [];
        KeyboardRows =
        [
            new ObservableCollection<KeyboardKeyViewModel>("QWERTYUIOP".Select(c => new KeyboardKeyViewModel(c.ToString()))),
            new ObservableCollection<KeyboardKeyViewModel>("ASDFGHJKL".Select(c => new KeyboardKeyViewModel(c.ToString()))),
            new ObservableCollection<KeyboardKeyViewModel>("ZXCVBNM".Select(c => new KeyboardKeyViewModel(c.ToString())))
        ];
        Statistics = _storage.LoadStatistics();
        ApplyDataMigrationIfNeeded();
        NormalizeStatistics();
        NormalizeStreakForToday();
        StatCards =
        [
            new StatCardViewModel("Punti mese"),
            new StatCardViewModel("Giocate"),
            new StatCardViewModel("Vinte"),
            new StatCardViewModel("Vittorie"),
            new StatCardViewModel("Streak"),
            new StatCardViewModel("Record")
        ];
        WinRows = new ObservableCollection<WinDistributionRowViewModel>(
            Enumerable.Range(1, 6).Select(attempt => new WinDistributionRowViewModel(attempt)));
        InfiniteStatCards =
        [
            new StatCardViewModel("Giocate"),
            new StatCardViewModel("Vinte"),
            new StatCardViewModel("Vittorie")
        ];
        InfiniteWinRows = new ObservableCollection<WinDistributionRowViewModel>(
            Enumerable.Range(1, 6).Select(attempt => new WinDistributionRowViewModel(attempt)));
        WrappedStatCards =
        [
            new StatCardViewModel("Punti"),
            new StatCardViewModel("Giocate"),
            new StatCardViewModel("Vinte"),
            new StatCardViewModel("Sconfitte"),
            new StatCardViewModel("Vittorie"),
            new StatCardViewModel("Media"),
            new StatCardViewModel("Streak"),
            new StatCardViewModel("Entro 3"),
            new StatCardViewModel("Media pt")
        ];
        MonthlyRecapCards =
        [
            new StatCardViewModel("Giocate"),
            new StatCardViewModel("Vinte"),
            new StatCardViewModel("Sconfitte"),
            new StatCardViewModel("Vittorie"),
            new StatCardViewModel("Media"),
            new StatCardViewModel("Miglior streak"),
            new StatCardViewModel("Entro 3"),
            new StatCardViewModel("Punti"),
            new StatCardViewModel("Tempo medio")
        ];
        WrappedTimeCards =
        [
            new StatCardViewModel("Media tempo"),
            new StatCardViewModel("Piu' veloce"),
            new StatCardViewModel("Piu' lenta"),
            new StatCardViewModel("Tempo totale")
        ];
        WrappedWinRows = new ObservableCollection<WinDistributionRowViewModel>(
            Enumerable.Range(1, 6).Select(attempt => new WinDistributionRowViewModel(attempt)));
        HistoryRows = [];

        KeyCommand = new RelayCommand(parameter => HandleInput(parameter?.ToString() ?? string.Empty));
        SelectTileCommand = new RelayCommand(parameter =>
        {
            if (int.TryParse(parameter?.ToString(), out var index))
            {
                SelectTile(index);
            }
        });
        ToggleThemeCommand = new RelayCommand(_ => IsDarkTheme = !IsDarkTheme);
        ToggleKeyboardCommand = new RelayCommand(_ => IsVirtualKeyboardVisible = !IsVirtualKeyboardVisible);
        HideSplashCommand = new RelayCommand(_ => IsSplashVisible = false);
        StartBonusCommand = new RelayCommand(_ => StartBonus());
        ViewDailyCommand = new RelayCommand(_ => ViewDaily());
        ViewBonusCommand = new RelayCommand(_ => ViewBonus());
        StartInfiniteCommand = new RelayCommand(_ => StartInfinite());
        ShowStatisticsCommand = new RelayCommand(_ => IsStatisticsVisible = true);
        ShowHistoryCommand = new RelayCommand(_ => ShowHistory());
        ShowWrappedCommand = new RelayCommand(_ => ShowWrapped());
        SetWrappedModeCommand = new RelayCommand(parameter => SetWrappedMode(parameter?.ToString() ?? "Mese"));
        PreviousWrappedPeriodCommand = new RelayCommand(_ => MoveWrappedPeriod(-1));
        NextWrappedPeriodCommand = new RelayCommand(_ => MoveWrappedPeriod(1));
        ShowResetConfirmCommand = new RelayCommand(_ => IsResetConfirmVisible = true);
        ConfirmResetCommand = new RelayCommand(_ => ResetGameData());
        CancelResetCommand = new RelayCommand(_ => IsResetConfirmVisible = false);
        OpenMonthlyRecapWrappedCommand = new RelayCommand(_ => OpenMonthlyRecapWrapped());
        SetHistoryFilterCommand = new RelayCommand(parameter =>
        {
            _historyFilter = parameter?.ToString() ?? "Tutte";
            HistoryFilterLabel = $"Filtro: {_historyFilter}";
            RefreshHistoryFilterStates();
            RefreshHistoryView();
        });
        CopyCurrentResultCommand = new RelayCommand(_ => CopyCurrentResult());
        CopyHistoryResultCommand = new RelayCommand(parameter => CopyText(parameter?.ToString()));
        CloseOverlayCommand = new RelayCommand(_ => CloseOverlays());
        ShowHelpCommand = new RelayCommand(_ => IsHelpVisible = true);
        ShowSettingsCommand = new RelayCommand(_ => ShowSettings());
        CheckUpdatesCommand = new RelayCommand(_ => _ = CheckForUpdatesManuallyAsync());
        InstallUpdateCommand = new RelayCommand(_ => _ = InstallPendingUpdateAsync());
        DismissUpdateCommand = new RelayCommand(_ => DismissUpdateDialog());
        SaveProfileCommand = new RelayCommand(_ => SaveProfileName());
        DismissChangelogCommand = new RelayCommand(_ => DismissChangelog());

        LoadOrStartGame();
        RefreshStatisticsView();
        RefreshHistoryView();
        RefreshWrappedView();
        CheckPendingMonthlyRecap();
        ShowChangelogIfNeeded(userSettingsExists);

        if (_settings.EnableAutomaticUpdateChecks)
        {
            _ = CheckForUpdatesOnStartupAsync();
        }
    }

    public event EventHandler<int>? ShakeRequested;
    public event EventHandler<int>? RevealRequested;
    public event EventHandler<int>? LetterEntered;
    public event EventHandler<int>? VictoryAnimationRequested;
    public event EventHandler<int>? DefeatAnimationRequested;
    public ObservableCollection<TileViewModel> Tiles { get; }
    public ObservableCollection<ObservableCollection<KeyboardKeyViewModel>> KeyboardRows { get; }
    public ObservableCollection<StatCardViewModel> StatCards { get; }
    public ObservableCollection<WinDistributionRowViewModel> WinRows { get; }
    public ObservableCollection<StatCardViewModel> InfiniteStatCards { get; }
    public ObservableCollection<WinDistributionRowViewModel> InfiniteWinRows { get; }
    public ObservableCollection<StatCardViewModel> WrappedStatCards { get; }
    public ObservableCollection<StatCardViewModel> MonthlyRecapCards { get; }
    public ObservableCollection<StatCardViewModel> WrappedTimeCards { get; }
    public ObservableCollection<WinDistributionRowViewModel> WrappedWinRows { get; }
    public ObservableCollection<HistoryEntryViewModel> HistoryRows { get; }
    public Statistics Statistics { get; }
    public string SplashSubtitle => string.IsNullOrWhiteSpace(PlayerName)
        ? "La sfida quotidiana"
        : $"La sfida quotidiana di {PlayerName}";
    public ICommand KeyCommand { get; }
    public ICommand SelectTileCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ToggleKeyboardCommand { get; }
    public ICommand HideSplashCommand { get; }
    public ICommand StartBonusCommand { get; }
    public ICommand ViewDailyCommand { get; }
    public ICommand ViewBonusCommand { get; }
    public ICommand StartInfiniteCommand { get; }
    public ICommand ShowStatisticsCommand { get; }
    public ICommand ShowHistoryCommand { get; }
    public ICommand ShowWrappedCommand { get; }
    public ICommand SetWrappedModeCommand { get; }
    public ICommand PreviousWrappedPeriodCommand { get; }
    public ICommand NextWrappedPeriodCommand { get; }
    public ICommand ShowResetConfirmCommand { get; }
    public ICommand ConfirmResetCommand { get; }
    public ICommand CancelResetCommand { get; }
    public ICommand OpenMonthlyRecapWrappedCommand { get; }
    public ICommand SetHistoryFilterCommand { get; }
    public ICommand CopyCurrentResultCommand { get; }
    public ICommand CopyHistoryResultCommand { get; }
    public ICommand CloseOverlayCommand { get; }
    public ICommand ShowHelpCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand InstallUpdateCommand { get; }
    public ICommand DismissUpdateCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand DismissChangelogCommand { get; }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string ModeBadgeText
    {
        get => _modeBadgeText;
        set => SetProperty(ref _modeBadgeText, value);
    }

    public string ModeBadgeDetail
    {
        get => _modeBadgeDetail;
        set => SetProperty(ref _modeBadgeDetail, value);
    }

    public string PlayerName
    {
        get => _playerName;
        set
        {
            if (SetProperty(ref _playerName, value))
            {
                OnPropertyChanged(nameof(SplashSubtitle));
            }
        }
    }

    public string ProfileNameDraft
    {
        get => _profileNameDraft;
        set => SetProperty(ref _profileNameDraft, value);
    }

    public string ProfileError
    {
        get => _profileError;
        set => SetProperty(ref _profileError, value);
    }

    public string ChangelogTitle
    {
        get => _changelogTitle;
        set => SetProperty(ref _changelogTitle, value);
    }

    public string ChangelogText
    {
        get => _changelogText;
        set => SetProperty(ref _changelogText, value);
    }

    public string AppVersionText => $"Versione {_updateService.CurrentVersionText}";

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetProperty(ref _updateStatusText, value);
    }

    public string UpdateDialogTitle
    {
        get => _updateDialogTitle;
        set => SetProperty(ref _updateDialogTitle, value);
    }

    public string UpdateDialogMessage
    {
        get => _updateDialogMessage;
        set => SetProperty(ref _updateDialogMessage, value);
    }

    public string UpdateReleaseNotes
    {
        get => _updateReleaseNotes;
        set => SetProperty(ref _updateReleaseNotes, value);
    }

    public string UpdateProgressText
    {
        get => _updateProgressText;
        set => SetProperty(ref _updateProgressText, value);
    }

    public int UpdateProgressValue
    {
        get => _updateProgressValue;
        set => SetProperty(ref _updateProgressValue, value);
    }

    public string AvailableVersionText
    {
        get => _availableVersionText;
        set => SetProperty(ref _availableVersionText, value);
    }

    public int BoardColumns
    {
        get => _currentWordLength;
        private set => SetProperty(ref _currentWordLength, value);
    }

    public double BoardWidth
    {
        get => _boardWidth;
        private set => SetProperty(ref _boardWidth, value);
    }

    public bool IsBonusPromptVisible
    {
        get => _isBonusPromptVisible;
        set => SetProperty(ref _isBonusPromptVisible, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    public bool IsSplashVisible
    {
        get => _isSplashVisible;
        set => SetProperty(ref _isSplashVisible, value);
    }

    public bool IsVirtualKeyboardVisible
    {
        get => _isVirtualKeyboardVisible;
        set => SetProperty(ref _isVirtualKeyboardVisible, value);
    }

    public bool IsStatisticsVisible
    {
        get => _isStatisticsVisible;
        set => SetProperty(ref _isStatisticsVisible, value);
    }

    public bool IsHistoryVisible
    {
        get => _isHistoryVisible;
        set => SetProperty(ref _isHistoryVisible, value);
    }

    public bool IsWrappedVisible
    {
        get => _isWrappedVisible;
        set => SetProperty(ref _isWrappedVisible, value);
    }

    public bool IsMonthlyRecapVisible
    {
        get => _isMonthlyRecapVisible;
        set => SetProperty(ref _isMonthlyRecapVisible, value);
    }

    public bool IsResetConfirmVisible
    {
        get => _isResetConfirmVisible;
        set => SetProperty(ref _isResetConfirmVisible, value);
    }

    public bool IsHelpVisible
    {
        get => _isHelpVisible;
        set => SetProperty(ref _isHelpVisible, value);
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set => SetProperty(ref _isSettingsVisible, value);
    }

    public bool IsUpdateDialogVisible
    {
        get => _isUpdateDialogVisible;
        set => SetProperty(ref _isUpdateDialogVisible, value);
    }

    public bool IsUpdateBusy
    {
        get => _isUpdateBusy;
        set => SetProperty(ref _isUpdateBusy, value);
    }

    public bool IsUpdateStatusVisible
    {
        get => _isUpdateStatusVisible;
        set => SetProperty(ref _isUpdateStatusVisible, value);
    }

    public bool IsProfileDialogVisible
    {
        get => _isProfileDialogVisible;
        set => SetProperty(ref _isProfileDialogVisible, value);
    }

    public bool IsChangelogVisible
    {
        get => _isChangelogVisible;
        set => SetProperty(ref _isChangelogVisible, value);
    }

    public bool IsCurrentResultCopyVisible
    {
        get => _isCurrentResultCopyVisible;
        set => SetProperty(ref _isCurrentResultCopyVisible, value);
    }

    public bool IsBonusViewButtonVisible
    {
        get => _isBonusViewButtonVisible;
        set => SetProperty(ref _isBonusViewButtonVisible, value);
    }

    public bool IsDailyViewButtonVisible
    {
        get => _isDailyViewButtonVisible;
        set => SetProperty(ref _isDailyViewButtonVisible, value);
    }

    public bool IsNewInfiniteButtonVisible
    {
        get => _isNewInfiniteButtonVisible;
        set => SetProperty(ref _isNewInfiniteButtonVisible, value);
    }

    public string ScoreLineText
    {
        get => _scoreLineText;
        set => SetProperty(ref _scoreLineText, value);
    }

    public bool IsScoreLineVisible
    {
        get => _isScoreLineVisible;
        set => SetProperty(ref _isScoreLineVisible, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        set => SetProperty(ref _isToastVisible, value);
    }

    public string HistoryFilterLabel
    {
        get => _historyFilterLabel;
        set => SetProperty(ref _historyFilterLabel, value);
    }

    public string HistoryEmptyMessage
    {
        get => _historyEmptyMessage;
        set => SetProperty(ref _historyEmptyMessage, value);
    }

    public bool IsHistoryEmptyVisible
    {
        get => _isHistoryEmptyVisible;
        set => SetProperty(ref _isHistoryEmptyVisible, value);
    }

    public bool IsHistoryFilterAllActive => _historyFilter == "Tutte";
    public bool IsHistoryFilterDailyActive => _historyFilter == "Giornaliere";
    public bool IsHistoryFilterBonusActive => _historyFilter == "Bonus";
    public bool IsHistoryFilterInfiniteActive => _historyFilter == "Infinite";
    public bool IsHistoryFilterWonActive => _historyFilter == "Vinte";
    public bool IsHistoryFilterLostActive => _historyFilter == "Perse";
    public bool IsInfiniteHistoryStatsVisible => _historyFilter == "Infinite";

    public string DailyTimerText
    {
        get => _dailyTimerText;
        set => SetProperty(ref _dailyTimerText, value);
    }

    public bool IsDailyTimerVisible
    {
        get => _isDailyTimerVisible;
        set => SetProperty(ref _isDailyTimerVisible, value);
    }

    public string WrappedPeriodLabel
    {
        get => _wrappedPeriodLabel;
        set => SetProperty(ref _wrappedPeriodLabel, value);
    }

    public string WrappedMonthlyScoresText
    {
        get => _wrappedMonthlyScoresText;
        set => SetProperty(ref _wrappedMonthlyScoresText, value);
    }

    public string MonthlyRecapTitle
    {
        get => _monthlyRecapTitle;
        set => SetProperty(ref _monthlyRecapTitle, value);
    }

    public bool IsWrappedMonthlyActive => _wrappedMode == "Mese";
    public bool IsWrappedYearlyActive => _wrappedMode == "Anno";

    public bool EnsureCurrentGame()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayKey = DailyWordService.FormatDateKey(today);
        if (_todayKey == todayKey)
        {
            return false;
        }

        PersistActiveGameTime();
        StopCurrentTimer();
        StartNewGameForDate(today, "E' disponibile una nuova sfida giornaliera.");
        ShowToast("Nuova sfida giornaliera disponibile.");
        CheckPendingMonthlyRecap();
        return true;
    }

    public void TickTimer()
    {
        if (CurrentStatus != GameStatus.Playing || GetCurrentTimerStartedAt() is null)
        {
            return;
        }

        RefreshCurrentTimerText();
    }

    public void PersistActiveGameTime()
    {
        SaveCurrentTimerCheckpoint();
        SaveGame();
    }

    public void HandlePhysicalKey(Key key)
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        if (IsBonusPromptVisible ||
            IsStatisticsVisible ||
            IsHistoryVisible ||
            IsHelpVisible ||
            IsWrappedVisible ||
            IsMonthlyRecapVisible ||
            IsResetConfirmVisible ||
            IsSettingsVisible ||
            IsUpdateDialogVisible ||
            IsProfileDialogVisible ||
            IsChangelogVisible)
        {
            return;
        }

        if (key is >= Key.A and <= Key.Z)
        {
            HandleInput(key.ToString());
        }
        else if (key == Key.Enter)
        {
            HandleInput("INVIO");
        }
        else if (key == Key.Back)
        {
            HandleInput("CANC");
        }
        else if (key == Key.Left)
        {
            MoveSelection(-1);
        }
        else if (key == Key.Right)
        {
            MoveSelection(1);
        }
    }

    private void HandleInput(string input)
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        if (IsProfileDialogVisible)
        {
            return;
        }

        if (IsSplashVisible)
        {
            IsSplashVisible = false;
        }

        var status = CurrentStatus;
        if (status != GameStatus.Playing)
        {
            Message = _isInfiniteActive
                ? "Partita infinita completata."
                : _isBonusActive
                ? "Bonus random gia' completato."
                : "Hai gia' completato la parola di oggi.";
            return;
        }

        input = input.ToUpperInvariant();
        if (input == "INVIO" || input == "ENTER")
        {
            SubmitGuess();
            return;
        }

        if (input == "CANC" || input == "BACK")
        {
            RemoveLetter();
            return;
        }

        if (input.Length == 1 && input[0] is >= 'A' and <= 'Z')
        {
            AddLetter(input);
        }
    }

    private GameStatus CurrentStatus => _isInfiniteActive
        ? _infiniteStatus
        : _isBonusActive ? _bonusStatus : _dailyStatus;

    private void SetCurrentStatus(GameStatus status)
    {
        if (_isInfiniteActive)
        {
            _infiniteStatus = status;
        }
        else if (_isBonusActive)
        {
            _bonusStatus = status;
        }
        else
        {
            _dailyStatus = status;
        }

        RefreshSelectedTile();
    }

    private void AddLetter(string letter)
    {
        if (CurrentStatus != GameStatus.Playing)
        {
            return;
        }

        StartCurrentTimerIfNeeded();
        var index = CurrentTileIndex(_selectedColumn);
        Tiles[index].Letter = letter;
        Tiles[index].State = TileState.Filled;
        LetterEntered?.Invoke(this, index);
        MoveSelectionTo(Math.Min(_selectedColumn + 1, _currentWordLength - 1));
    }

    private void RemoveLetter()
    {
        if (CurrentStatus != GameStatus.Playing)
        {
            return;
        }

        var index = CurrentTileIndex(_selectedColumn);
        if (string.IsNullOrEmpty(Tiles[index].Letter) && _selectedColumn > 0)
        {
            MoveSelectionTo(_selectedColumn - 1);
            index = CurrentTileIndex(_selectedColumn);
        }

        Tiles[index].Letter = string.Empty;
        Tiles[index].State = TileState.Empty;
    }

    private void SubmitGuess()
    {
        var guess = GetCurrentGuess();
        if (guess.Length != _currentWordLength)
        {
            Reject($"Servono {_currentWordLength} lettere.");
            return;
        }

        guess = WordRepository.Normalize(guess);
        if (!_repository.IsValid(guess))
        {
            Reject("Parola non presente nel dizionario.");
            return;
        }

        ApplyGuess(guess, true);
        SubmittedGuesses.Add(guess);
        SaveGame();

        if (guess == _currentSolution)
        {
            StopCurrentTimer();
            SetCurrentStatus(GameStatus.Won);
            Message = _isInfiniteActive
                ? "Infinita vinta. Puoi farne un'altra."
                : _isBonusActive
                ? "Bonus vinto: +1 punto."
                : _currentRow switch
                {
                    0 => "Geniale.",
                    1 => "Magnifico.",
                    2 => "Ottimo.",
                    3 => "Bene.",
                    4 => "Ci sei arrivato.",
                    _ => "All'ultimo respiro."
                };

            if (_isInfiniteActive)
            {
                RecordInfinite(true, _currentRow + 1);
            }
            else if (_isBonusActive)
            {
                RecordBonus(true, _currentRow + 1);
            }
            else
            {
                RecordDaily(true, _currentRow + 1);
                UnlockBonus();
            }

            RefreshCopyButtonVisibility();
            SaveGame();
            VictoryAnimationRequested?.Invoke(this, _currentRow);
            return;
        }

        _currentRow++;
        MoveSelectionTo(0);

        if (_currentRow == 6)
        {
            StopCurrentTimer();
            SetCurrentStatus(GameStatus.Lost);
            Message = _isInfiniteActive
                ? $"Infinita persa. La parola era {_currentSolution.ToUpperInvariant()}."
                : _isBonusActive
                ? $"Bonus perso. La parola era {_currentSolution.ToUpperInvariant()}."
                : $"La parola era {_currentSolution.ToUpperInvariant()}.";

            if (_isInfiniteActive)
            {
                RecordInfinite(false, 0);
            }
            else if (_isBonusActive)
            {
                RecordBonus(false, 0);
            }
            else
            {
                RecordDaily(false, 0);
            }

            RefreshCopyButtonVisibility();
            SaveGame();
            DefeatAnimationRequested?.Invoke(this, 5);
        }
        else
        {
            Message = BuildAttemptsLeftMessage(6 - _currentRow);
        }
    }

    public void SelectTile(int index)
    {
        if (EnsureCurrentGame() ||
            CurrentStatus != GameStatus.Playing ||
            index < 0 ||
            index >= Tiles.Count)
        {
            return;
        }

        var row = index / _currentWordLength;
        if (row != _currentRow)
        {
            return;
        }

        MoveSelectionTo(index % _currentWordLength);
    }

    private void MoveSelection(int delta)
    {
        if (CurrentStatus != GameStatus.Playing)
        {
            return;
        }

        MoveSelectionTo(Math.Clamp(_selectedColumn + delta, 0, _currentWordLength - 1));
    }

    private void MoveSelectionTo(int column)
    {
        _selectedColumn = Math.Clamp(column, 0, _currentWordLength - 1);
        RefreshSelectedTile();
    }

    private void RefreshSelectedTile()
    {
        for (var i = 0; i < Tiles.Count; i++)
        {
            var isCurrentRow = i / _currentWordLength == _currentRow;
            var isSelectedColumn = i % _currentWordLength == _selectedColumn;
            Tiles[i].IsSelected = CurrentStatus == GameStatus.Playing && isCurrentRow && isSelectedColumn;
        }
    }

    private int CurrentTileIndex(int column) => _currentRow * _currentWordLength + column;

    private string GetCurrentGuess()
    {
        var letters = Enumerable.Range(0, _currentWordLength)
            .Select(column => Tiles[CurrentTileIndex(column)].Letter);
        return string.Concat(letters).ToLowerInvariant();
    }

    private List<string> SubmittedGuesses => _isInfiniteActive
        ? _infiniteGuesses
        : _isBonusActive ? _bonusGuesses : _dailyGuesses;

    private void StartCurrentTimerIfNeeded()
    {
        if (CurrentStatus != GameStatus.Playing)
        {
            return;
        }

        var now = DateTime.Now;
        if (_isInfiniteActive)
        {
            if (!_infiniteTimerStarted)
            {
                _infiniteTimerStarted = true;
                _infiniteElapsedSeconds = 0;
            }

            _infiniteTimerStartedAt ??= now;
        }
        else if (_isBonusActive)
        {
            if (!_bonusTimerStarted)
            {
                _bonusTimerStarted = true;
                _bonusElapsedSeconds = 0;
            }

            _bonusTimerStartedAt ??= now;
        }
        else
        {
            if (!_dailyTimerStarted)
            {
                _dailyTimerStarted = true;
                _dailyElapsedSeconds = 0;
            }

            _dailyTimerStartedAt ??= now;
        }

        RefreshCurrentTimerText();
    }

    private void StopCurrentTimer()
    {
        SaveCurrentTimerCheckpoint();
        if (_isInfiniteActive)
        {
            _infiniteTimerStartedAt = null;
        }
        else if (_isBonusActive)
        {
            _bonusTimerStartedAt = null;
        }
        else
        {
            _dailyTimerStartedAt = null;
        }

        RefreshCurrentTimerText();
    }

    private void PauseCurrentTimer()
    {
        SaveCurrentTimerCheckpoint();
        if (_isInfiniteActive)
        {
            _infiniteTimerStartedAt = null;
        }
        else if (_isBonusActive)
        {
            _bonusTimerStartedAt = null;
        }
        else
        {
            _dailyTimerStartedAt = null;
        }
    }

    private void ResumeCurrentTimerIfNeeded()
    {
        if (CurrentStatus != GameStatus.Playing)
        {
            RefreshCurrentTimerText();
            return;
        }

        var now = DateTime.Now;
        if (_isInfiniteActive && _infiniteTimerStarted)
        {
            _infiniteTimerStartedAt ??= now;
        }
        else if (_isBonusActive && _bonusTimerStarted)
        {
            _bonusTimerStartedAt ??= now;
        }
        else if (!_isBonusActive && !_isInfiniteActive && _dailyTimerStarted)
        {
            _dailyTimerStartedAt ??= now;
        }

        RefreshCurrentTimerText();
    }

    private void SaveCurrentTimerCheckpoint()
    {
        var startedAt = GetCurrentTimerStartedAt();
        if (startedAt is null)
        {
            return;
        }

        var elapsed = Math.Max(0, (int)(DateTime.Now - startedAt.Value).TotalSeconds);
        if (_isInfiniteActive)
        {
            _infiniteElapsedSeconds += elapsed;
            _infiniteTimerStartedAt = DateTime.Now;
        }
        else if (_isBonusActive)
        {
            _bonusElapsedSeconds += elapsed;
            _bonusTimerStartedAt = DateTime.Now;
        }
        else
        {
            _dailyElapsedSeconds += elapsed;
            _dailyTimerStartedAt = DateTime.Now;
        }
    }

    private DateTime? GetCurrentTimerStartedAt()
    {
        return _isInfiniteActive
            ? _infiniteTimerStartedAt
            : _isBonusActive ? _bonusTimerStartedAt : _dailyTimerStartedAt;
    }

    private int GetCurrentElapsedSeconds()
    {
        var elapsedSeconds = _isInfiniteActive
            ? _infiniteElapsedSeconds
            : _isBonusActive ? _bonusElapsedSeconds : _dailyElapsedSeconds;
        var startedAt = GetCurrentTimerStartedAt();
        return startedAt is null
            ? elapsedSeconds
            : elapsedSeconds + Math.Max(0, (int)(DateTime.Now - startedAt.Value).TotalSeconds);
    }

    private int GetDailyElapsedSeconds()
    {
        return _dailyTimerStartedAt is null
            ? _dailyElapsedSeconds
            : _dailyElapsedSeconds + Math.Max(0, (int)(DateTime.Now - _dailyTimerStartedAt.Value).TotalSeconds);
    }

    private int GetBonusElapsedSeconds()
    {
        return _bonusTimerStartedAt is null
            ? _bonusElapsedSeconds
            : _bonusElapsedSeconds + Math.Max(0, (int)(DateTime.Now - _bonusTimerStartedAt.Value).TotalSeconds);
    }

    private int GetInfiniteElapsedSeconds()
    {
        return _infiniteTimerStartedAt is null
            ? _infiniteElapsedSeconds
            : _infiniteElapsedSeconds + Math.Max(0, (int)(DateTime.Now - _infiniteTimerStartedAt.Value).TotalSeconds);
    }

    private void RefreshCurrentTimerText()
    {
        DailyTimerText = FormatDuration(GetCurrentElapsedSeconds());
    }

    private static string FormatDuration(int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string BuildAttemptsLeftMessage(int attemptsLeft)
    {
        return attemptsLeft == 1
            ? "Ti rimane 1 tentativo."
            : $"Ti rimangono {attemptsLeft} tentativi.";
    }

    private void ApplyGuess(string guess, bool animate)
    {
        var states = GuessEvaluator.Evaluate(guess, _currentSolution);
        for (var i = 0; i < _currentWordLength; i++)
        {
            var tile = Tiles[_currentRow * _currentWordLength + i];
            tile.Letter = guess[i].ToString().ToUpperInvariant();
            tile.State = states[i];
            UpdateKeyboard(guess[i].ToString().ToUpperInvariant(), states[i]);
        }

        if (animate)
        {
            RevealRequested?.Invoke(this, _currentRow);
        }
    }

    private void Reject(string message)
    {
        ShowToast(message);
        ShakeRequested?.Invoke(this, _currentRow);
    }

    private void UpdateKeyboard(string letter, TileState state)
    {
        var key = KeyboardRows.SelectMany(row => row).FirstOrDefault(k => k.Label == letter);
        if (key is null || Rank(state) <= Rank(key.State))
        {
            return;
        }

        key.State = state;
    }

    private static int Rank(TileState state) => state switch
    {
        TileState.Correct => 3,
        TileState.Present => 2,
        TileState.Absent => 1,
        _ => 0
    };

    private void SetSolutionsForDate(DateOnly date)
    {
        _todayKey = DailyWordService.FormatDateKey(date);
        _dailySolution = _dailyWordService.GetWordForDate(date);
        (_bonusSolution, _bonusWordLength) = _dailyWordService.GetBonusWordForDate(date);
    }

    private void StartNewGameForDate(DateOnly date, string? message)
    {
        SetSolutionsForDate(date);
        _dailyGuesses.Clear();
        _bonusGuesses.Clear();
        _dailyStatus = GameStatus.Playing;
        _bonusStatus = GameStatus.Playing;
        _dailyElapsedSeconds = 0;
        _dailyTimerStarted = false;
        _dailyTimerStartedAt = null;
        _bonusElapsedSeconds = 0;
        _bonusTimerStarted = false;
        _bonusTimerStartedAt = null;
        _isBonusActive = false;
        _isBonusUnlocked = false;
        _dailyStatisticsAlreadyRecorded = false;
        IsBonusPromptVisible = false;
        CloseOverlays();
        SetupDailyBoard();
        if (!string.IsNullOrWhiteSpace(message))
        {
            Message = message;
        }

        SaveGame();
        RefreshStatisticsView();
        RefreshHistoryView();
        RefreshWrappedView();
        RefreshCopyButtonVisibility();
    }

    private void LoadOrStartGame()
    {
        var saved = _storage.LoadGame();
        var savedDate = string.IsNullOrWhiteSpace(saved?.GameDate)
            ? saved?.Date
            : saved.GameDate;
        if (saved is not null)
        {
            RestoreInfinite(saved.Infinite);
        }

        if (saved is null || IsBeforeOfficialStart(savedDate) || savedDate != _todayKey)
        {
            StartNewGameForDate(DateOnly.FromDateTime(DateTime.Today), null);
            return;
        }

        if (!string.IsNullOrWhiteSpace(saved.Solution))
        {
            _dailySolution = saved.Solution;
        }

        if (!string.IsNullOrWhiteSpace(saved.Bonus.Solution) && saved.Bonus.WordLength is >= 5 and <= 7)
        {
            _bonusSolution = saved.Bonus.Solution;
            _bonusWordLength = saved.Bonus.WordLength;
        }

        _dailyStatus = saved.Status;
        _dailyElapsedSeconds = Math.Max(0, saved.DailyElapsedSeconds);
        _dailyTimerStarted = saved.DailyTimerStarted;
        _dailyTimerStartedAt = _dailyTimerStarted && _dailyStatus == GameStatus.Playing
            ? DateTime.Now
            : null;
        _dailyGuesses.Clear();
        _dailyGuesses.AddRange(saved.Guesses.Take(6));
        _isBonusUnlocked = saved.Bonus.IsUnlocked || _dailyStatus == GameStatus.Won;
        _bonusStatus = saved.Bonus.Status;
        _bonusElapsedSeconds = Math.Max(0, saved.Bonus.ElapsedSeconds);
        _bonusTimerStarted = saved.Bonus.TimerStarted;
        _bonusGuesses.Clear();
        _bonusGuesses.AddRange(saved.Bonus.Guesses.Take(6));
        if (saved.Bonus.WordLength != _bonusWordLength)
        {
            _bonusStatus = GameStatus.Playing;
            _bonusGuesses.Clear();
            _bonusElapsedSeconds = 0;
            _bonusTimerStarted = false;
        }

        if (_bonusGuesses.Count > 0 && _bonusStatus == GameStatus.Playing)
        {
            StartBonus(false);
            RefreshCopyButtonVisibility();
            return;
        }

        SetupDailyBoard();
        LoadGuesses(_dailyGuesses);
        if (_dailyStatus == GameStatus.Won)
        {
            Message = "Parola di oggi gia' completata.";
            UpsertHistory(CreateDailyHistoryEntry(true, _dailyGuesses.Count));
            if (_isBonusUnlocked && _bonusStatus == GameStatus.Playing && _bonusGuesses.Count == 0)
            {
                IsBonusPromptVisible = true;
            }
        }
        else if (_dailyStatus == GameStatus.Lost)
        {
            Message = $"Parola di oggi completata. Era {_dailySolution.ToUpperInvariant()}.";
            UpsertHistory(CreateDailyHistoryEntry(false, 0));
        }

        if (_bonusStatus != GameStatus.Playing && _bonusGuesses.Count > 0)
        {
            UpsertHistory(CreateBonusHistoryEntry(_bonusStatus == GameStatus.Won, _bonusStatus == GameStatus.Won ? _bonusGuesses.Count : 0));
        }

        EnsureCompletedInfiniteInHistory();
        _storage.SaveStatistics(Statistics);
        RefreshHistoryView();
        RefreshCopyButtonVisibility();
    }

    private void RestoreInfinite(InfiniteGame infinite)
    {
        if (!string.IsNullOrWhiteSpace(infinite.Solution))
        {
            _infiniteSolution = infinite.Solution;
        }

        _infiniteStatus = infinite.Status;
        _infiniteElapsedSeconds = Math.Max(0, infinite.ElapsedSeconds);
        _infiniteTimerStarted = infinite.TimerStarted;
        _infiniteGuesses.Clear();
        _infiniteGuesses.AddRange(infinite.Guesses.Take(6));
    }

    private void SetupDailyBoard()
    {
        PauseCurrentTimer();
        _isInfiniteActive = false;
        _isBonusActive = false;
        IsDailyTimerVisible = true;
        _currentSolution = _dailySolution;
        SetModeBadge("Giornaliera", "Sfida quotidiana");
        SetupBoard(5);
        LoadGuesses(_dailyGuesses);
        ResumeCurrentTimerIfNeeded();
        Message = _dailyStatus == GameStatus.Playing ? "Indovina la parola di oggi." : Message;
        RefreshCopyButtonVisibility();
    }

    private void StartBonus()
    {
        StartBonus(true);
    }

    private void StartBonus(bool save)
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        IsSplashVisible = false;
        IsBonusPromptVisible = false;
        PauseCurrentTimer();
        _isInfiniteActive = false;
        _isBonusActive = true;
        IsDailyTimerVisible = true;
        _isBonusUnlocked = true;
        _currentSolution = _bonusSolution;
        SetModeBadge("Bonus random", $"{_bonusWordLength} lettere");
        SetupBoard(_bonusWordLength);
        LoadGuesses(_bonusGuesses);
        ResumeCurrentTimerIfNeeded();
        Message = $"Bonus random: parola da {_bonusWordLength} lettere.";
        RefreshCopyButtonVisibility();
        if (save)
        {
            SaveGame();
        }
    }

    private void StartInfinite()
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        IsSplashVisible = false;
        IsBonusPromptVisible = false;
        CloseOverlays();
        PauseCurrentTimer();
        _isInfiniteActive = true;
        _isBonusActive = false;
        IsDailyTimerVisible = true;
        if (string.IsNullOrWhiteSpace(_infiniteSolution) || _infiniteStatus != GameStatus.Playing)
        {
            _infiniteSolution = PickRandomInfiniteWord();
            _infiniteGuesses.Clear();
            _infiniteStatus = GameStatus.Playing;
            _infiniteElapsedSeconds = 0;
            _infiniteTimerStarted = false;
            _infiniteTimerStartedAt = null;
        }

        _currentSolution = _infiniteSolution;
        SetModeBadge("Infinita", "Statistiche separate");
        SetupBoard(5);
        LoadGuesses(_infiniteGuesses);
        ResumeCurrentTimerIfNeeded();
        Message = "Modalita' infinita: parola casuale da 5 lettere.";
        RefreshCopyButtonVisibility();
        SaveGame();
    }

    private string PickRandomInfiniteWord()
    {
        var words = _repository.DailyWords.Count > 0
            ? _repository.DailyWords
            : _repository.ValidWords.Where(word => word.Length == 5).ToList();
        return words[Random.Shared.Next(words.Count)];
    }

    private void ViewDaily()
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        IsSplashVisible = false;
        IsBonusPromptVisible = false;
        SetupDailyBoard();
        Message = _dailyStatus switch
        {
            GameStatus.Won => "Parola di oggi gia' completata.",
            GameStatus.Lost => $"Parola di oggi completata. Era {_dailySolution.ToUpperInvariant()}.",
            _ => "Indovina la parola di oggi."
        };
        RefreshCopyButtonVisibility();
    }

    private void ViewBonus()
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        IsSplashVisible = false;
        IsBonusPromptVisible = false;
        PauseCurrentTimer();
        _isInfiniteActive = false;
        _isBonusActive = true;
        IsDailyTimerVisible = true;
        _currentSolution = _bonusSolution;
        SetModeBadge("Bonus random", $"{_bonusWordLength} lettere");
        SetupBoard(_bonusWordLength);
        LoadGuesses(_bonusGuesses);
        ResumeCurrentTimerIfNeeded();
        Message = _bonusStatus switch
        {
            GameStatus.Won => "Bonus random gia' completato.",
            GameStatus.Lost => $"Bonus random completato. La parola era {_bonusSolution.ToUpperInvariant()}.",
            _ => $"Bonus random: parola da {_bonusWordLength} lettere."
        };
        RefreshCopyButtonVisibility();
    }

    private void RefreshCopyButtonVisibility()
    {
        IsCurrentResultCopyVisible = _isBonusActive
            ? _bonusStatus != GameStatus.Playing && _bonusGuesses.Count > 0
            : _isInfiniteActive
            ? _infiniteStatus != GameStatus.Playing && _infiniteGuesses.Count > 0
            : _dailyStatus != GameStatus.Playing && _dailyGuesses.Count > 0;
        IsBonusViewButtonVisible = !_isBonusActive && _bonusGuesses.Count > 0;
        IsDailyViewButtonVisible = _isBonusActive || _isInfiniteActive;
        IsNewInfiniteButtonVisible = _isInfiniteActive && _infiniteStatus != GameStatus.Playing;
        RefreshScoreLine();
    }

    private void RefreshScoreLine()
    {
        var entry = GetCurrentCompetitiveResultEntry();
        if (entry is null)
        {
            ScoreLineText = string.Empty;
            IsScoreLineVisible = false;
            return;
        }

        var monthLabel = GetShareMonthLabel(entry);
        ScoreLineText = $"+{GetEntryScore(entry)} pt · {monthLabel}: {GetShareMonthScore(entry)} pt";
        IsScoreLineVisible = true;
    }

    private GameHistoryEntry? GetCurrentCompetitiveResultEntry()
    {
        if (_isInfiniteActive)
        {
            return null;
        }

        if (_isBonusActive)
        {
            return _bonusStatus == GameStatus.Playing || _bonusGuesses.Count == 0
                ? null
                : CreateBonusHistoryEntry(_bonusStatus == GameStatus.Won, _bonusStatus == GameStatus.Won ? _bonusGuesses.Count : 0);
        }

        return _dailyStatus == GameStatus.Playing || _dailyGuesses.Count == 0
            ? null
            : CreateDailyHistoryEntry(_dailyStatus == GameStatus.Won, _dailyStatus == GameStatus.Won ? _dailyGuesses.Count : 0);
    }

    private void SetModeBadge(string text, string detail)
    {
        ModeBadgeText = text;
        ModeBadgeDetail = detail;
    }

    private void SetupBoard(int wordLength)
    {
        BoardColumns = wordLength;
        var tileSize = wordLength switch
        {
            5 => 62,
            6 => 54,
            _ => 48
        };
        BoardWidth = (tileSize + 8) * wordLength;
        _currentRow = 0;
        _selectedColumn = 0;
        Tiles.Clear();
        for (var i = 0; i < wordLength * 6; i++)
        {
            Tiles.Add(new TileViewModel { Index = i, Size = tileSize });
        }

        foreach (var key in KeyboardRows.SelectMany(row => row))
        {
            key.State = TileState.Empty;
        }
    }

    private void LoadGuesses(IReadOnlyList<string> guesses)
    {
        _currentRow = 0;
        foreach (var guess in guesses.Take(6))
        {
            if (guess.Length != _currentWordLength)
            {
                continue;
            }

            ApplyGuess(guess, false);
            _currentRow++;
        }

        MoveSelectionTo(0);
    }

    private void UnlockBonus()
    {
        _isBonusUnlocked = true;
        IsBonusPromptVisible = true;
    }

    private void SaveGame()
    {
        SaveCurrentTimerCheckpoint();
        _storage.SaveGame(new SavedGame
        {
            GameDate = _todayKey,
            Date = _todayKey,
            Solution = _dailySolution,
            WordLength = 5,
            Guesses = [.. _dailyGuesses],
            Status = _dailyStatus,
            DailyElapsedSeconds = GetDailyElapsedSeconds(),
            DailyTimerStarted = _dailyTimerStarted,
            Bonus = new BonusGame
            {
                IsUnlocked = _isBonusUnlocked,
                Solution = _bonusSolution,
                WordLength = _bonusWordLength,
                Guesses = [.. _bonusGuesses],
                Status = _bonusStatus,
                ElapsedSeconds = GetBonusElapsedSeconds(),
                TimerStarted = _bonusTimerStarted
            },
            Infinite = new InfiniteGame
            {
                Solution = _infiniteSolution,
                Guesses = [.. _infiniteGuesses],
                Status = _infiniteStatus,
                ElapsedSeconds = GetInfiniteElapsedSeconds(),
                TimerStarted = _infiniteTimerStarted
            }
        });
    }

    private void RecordDaily(bool won, int attempts)
    {
        if (_dailyStatisticsAlreadyRecorded)
        {
            return;
        }

        var today = DateOnly.Parse(_todayKey);
        var previousWinDate = DateOnly.TryParse(Statistics.LastWinDate, out var parsedWinDate)
            ? parsedWinDate
            : (DateOnly?)null;

        Statistics.Played++;
        Statistics.LastPlayedDate = _todayKey;
        if (won)
        {
            var score = CalculateScore(5, attempts);
            Statistics.Won++;
            Statistics.Points += score;
            Statistics.CurrentStreak = previousWinDate == today.AddDays(-1)
                ? Statistics.CurrentStreak + 1
                : 1;
            Statistics.BestStreak = Math.Max(Statistics.BestStreak, Statistics.CurrentStreak);
            Statistics.LastWinDate = _todayKey;
            Statistics.WinDistribution[attempts - 1]++;
        }
        else
        {
            Statistics.CurrentStreak = 0;
        }

        UpsertHistory(CreateDailyHistoryEntry(won, attempts));
        _dailyStatisticsAlreadyRecorded = true;
        _storage.SaveStatistics(Statistics);
        RefreshStatisticsView();
        RefreshHistoryView();
        RefreshWrappedView();
    }

    private void RecordBonus(bool won, int attempts)
    {
        Statistics.BonusPlayed++;
        if (won)
        {
            var score = CalculateScore(_bonusWordLength, attempts);
            Statistics.BonusWon++;
            Statistics.Points += score;
            if (_dailyStatus == GameStatus.Won)
            {
                Statistics.TwoPointDays++;
            }
        }

        UpsertHistory(CreateBonusHistoryEntry(won, attempts));
        _storage.SaveStatistics(Statistics);
        RefreshStatisticsView();
        RefreshHistoryView();
        RefreshWrappedView();
    }

    private void RecordInfinite(bool won, int attempts)
    {
        Statistics.InfinitePlayed++;
        if (won)
        {
            Statistics.InfiniteWon++;
            var index = Math.Clamp(attempts - 1, 0, 5);
            Statistics.InfiniteWinDistribution[index]++;
        }

        UpsertHistory(CreateInfiniteHistoryEntry());
        _storage.SaveStatistics(Statistics);
        RefreshStatisticsView();
        RefreshHistoryView();
        RefreshWrappedView();
    }

    private GameHistoryEntry CreateDailyHistoryEntry(bool won, int attempts)
    {
        return new GameHistoryEntry
        {
            Date = _todayKey,
            Solution = _dailySolution,
            IsBonus = false,
            IsInfinite = false,
            WordLength = 5,
            Won = won,
            Attempts = won ? attempts : 6,
            Points = CalculateScore(5, won ? attempts : 0),
            ScoreEarned = CalculateScore(5, won ? attempts : 0),
            DurationSeconds = _dailyTimerStarted ? GetDailyElapsedSeconds() : null,
            Guesses = [.. _dailyGuesses]
        };
    }

    private GameHistoryEntry CreateBonusHistoryEntry(bool won, int attempts)
    {
        return new GameHistoryEntry
        {
            Date = _todayKey,
            Solution = _bonusSolution,
            IsBonus = true,
            IsInfinite = false,
            WordLength = _bonusWordLength,
            Won = won,
            Attempts = won ? attempts : 6,
            Points = CalculateScore(_bonusWordLength, won ? attempts : 0),
            ScoreEarned = CalculateScore(_bonusWordLength, won ? attempts : 0),
            DurationSeconds = _bonusTimerStarted ? GetBonusElapsedSeconds() : null,
            Guesses = [.. _bonusGuesses]
        };
    }

    private GameHistoryEntry CreateInfiniteHistoryEntry()
    {
        return new GameHistoryEntry
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Solution = _infiniteSolution,
            IsBonus = false,
            IsInfinite = true,
            WordLength = 5,
            Won = _infiniteStatus == GameStatus.Won,
            Attempts = _infiniteStatus == GameStatus.Won ? _infiniteGuesses.Count : 6,
            Points = 0,
            ScoreEarned = 0,
            DurationSeconds = _infiniteTimerStarted ? GetInfiniteElapsedSeconds() : null,
            Guesses = [.. _infiniteGuesses]
        };
    }

    private void NormalizeStatistics()
    {
        if (Statistics.WinDistribution.Length != 6)
        {
            Statistics.WinDistribution = new int[6];
        }

        if (Statistics.InfiniteWinDistribution.Length != 6)
        {
            Statistics.InfiniteWinDistribution = new int[6];
        }

        Statistics.Points = Statistics.History.Where(IsCompetitiveEntry).Sum(GetEntryScore);
    }

    private void ApplyDataMigrationIfNeeded()
    {
        if (Statistics.DataMigrationVersion >= DataMigrationVersion)
        {
            return;
        }

        Statistics.History = Statistics.History
            .Where(IsOnOrAfterOfficialStart)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.IsBonus ? 1 : 0)
            .ToList();

        RebuildDerivedStatisticsFromHistory();
        Statistics.LastMonthlyRecapShown = string.Empty;
        Statistics.DataMigrationVersion = DataMigrationVersion;
        _storage.SaveStatistics(Statistics);
    }

    private void RebuildDerivedStatisticsFromHistory()
    {
        Statistics.Played = 0;
        Statistics.Won = 0;
        Statistics.Points = 0;
        Statistics.BonusPlayed = 0;
        Statistics.BonusWon = 0;
        Statistics.TwoPointDays = 0;
        Statistics.InfinitePlayed = 0;
        Statistics.InfiniteWon = 0;
        Statistics.CurrentStreak = 0;
        Statistics.BestStreak = 0;
        Statistics.WinDistribution = new int[6];
        Statistics.InfiniteWinDistribution = new int[6];
        Statistics.LastPlayedDate = string.Empty;
        Statistics.LastWinDate = string.Empty;

        var completedDailyEntries = Statistics.History
            .Where(entry => !entry.IsBonus && !entry.IsInfinite)
            .Select(entry => new { Entry = entry, Date = TryGetHistoryDate(entry.Date) })
            .Where(item => item.Date is not null)
            .GroupBy(item => item.Date!.Value)
            .OrderBy(group => group.Key)
            .Select(group => group.Last().Entry)
            .ToList();

        foreach (var entry in completedDailyEntries)
        {
            Statistics.Played++;
            Statistics.LastPlayedDate = entry.Date;
            if (!entry.Won)
            {
                continue;
            }

            Statistics.Won++;
            Statistics.LastWinDate = entry.Date;
            if (entry.Attempts is >= 1 and <= 6)
            {
                Statistics.WinDistribution[entry.Attempts - 1]++;
            }
        }

        var dailyByDate = completedDailyEntries
            .Select(entry => new { Entry = entry, Date = TryGetHistoryDate(entry.Date) })
            .Where(item => item.Date is not null)
            .OrderBy(item => item.Date!.Value)
            .ToList();
        var currentStreak = 0;
        DateOnly? previousDate = null;
        foreach (var item in dailyByDate)
        {
            if (!item.Entry.Won)
            {
                currentStreak = 0;
                previousDate = item.Date!.Value;
                continue;
            }

            currentStreak = previousDate == item.Date!.Value.AddDays(-1) ? currentStreak + 1 : 1;
            Statistics.BestStreak = Math.Max(Statistics.BestStreak, currentStreak);
            previousDate = item.Date.Value;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastDaily = dailyByDate.LastOrDefault();
        Statistics.CurrentStreak = lastDaily is not null &&
                                   lastDaily.Entry.Won &&
                                   lastDaily.Date is { } lastDate &&
                                   lastDate >= today.AddDays(-1)
            ? currentStreak
            : 0;

        foreach (var entry in Statistics.History.Where(entry => entry.IsBonus))
        {
            Statistics.BonusPlayed++;
            if (entry.Won)
            {
                Statistics.BonusWon++;
            }
        }

        Statistics.TwoPointDays = Statistics.History
            .Where(entry => entry.IsBonus && entry.Won)
            .Count(entry => Statistics.History.Any(daily =>
                !daily.IsBonus &&
                !daily.IsInfinite &&
                daily.Won &&
                TryGetHistoryDate(daily.Date) == TryGetHistoryDate(entry.Date)));

        foreach (var entry in Statistics.History.Where(entry => entry.IsInfinite))
        {
            Statistics.InfinitePlayed++;
            if (!entry.Won)
            {
                continue;
            }

            Statistics.InfiniteWon++;
            var index = Math.Clamp(entry.Attempts - 1, 0, 5);
            Statistics.InfiniteWinDistribution[index]++;
        }

        Statistics.Points = Statistics.History.Where(IsCompetitiveEntry).Sum(GetEntryScore);
    }

    private void NormalizeStreakForToday()
    {
        var today = DateOnly.Parse(_todayKey);
        if (!DateOnly.TryParse(Statistics.LastWinDate, out var lastWinDate))
        {
            return;
        }

        if (lastWinDate < today.AddDays(-1) && Statistics.CurrentStreak != 0)
        {
            Statistics.CurrentStreak = 0;
            _storage.SaveStatistics(Statistics);
        }
    }

    private void RefreshStatisticsView()
    {
        var currentMonth = DateOnly.FromDateTime(DateTime.Today);
        StatCards[0].Value = GetPeriodScore(new DateOnly(currentMonth.Year, currentMonth.Month, 1), new DateOnly(currentMonth.Year, currentMonth.Month, 1).AddMonths(1)).ToString(CultureInfo.InvariantCulture);
        StatCards[1].Value = Statistics.Played.ToString();
        StatCards[2].Value = Statistics.Won.ToString();
        StatCards[3].Value = $"{Statistics.WinPercentage}%";
        StatCards[4].Value = Statistics.CurrentStreak.ToString();
        StatCards[5].Value = Statistics.BestStreak.ToString();
        InfiniteStatCards[0].Value = Statistics.InfinitePlayed.ToString();
        InfiniteStatCards[1].Value = Statistics.InfiniteWon.ToString();
        InfiniteStatCards[2].Value = $"{Statistics.InfiniteWinPercentage}%";

        var maxWins = Math.Max(1, Statistics.WinDistribution.Max());
        for (var i = 0; i < WinRows.Count; i++)
        {
            var wins = Statistics.WinDistribution.ElementAtOrDefault(i);
            WinRows[i].Wins = wins;
            WinRows[i].BarWidth = wins == 0 ? 18 : Math.Max(34, wins * 220.0 / maxWins);
        }

        var maxInfiniteWins = Math.Max(1, Statistics.InfiniteWinDistribution.Max());
        for (var i = 0; i < InfiniteWinRows.Count; i++)
        {
            var wins = Statistics.InfiniteWinDistribution.ElementAtOrDefault(i);
            InfiniteWinRows[i].Wins = wins;
            InfiniteWinRows[i].BarWidth = wins == 0 ? 18 : Math.Max(34, wins * 220.0 / maxInfiniteWins);
        }
    }

    private void ShowWrapped()
    {
        RefreshWrappedView();
        IsWrappedVisible = true;
    }

    private void SetWrappedMode(string mode)
    {
        _wrappedMode = mode == "Anno" ? "Anno" : "Mese";
        _wrappedPeriod = _wrappedMode == "Anno"
            ? new DateOnly(_wrappedPeriod.Year, 1, 1)
            : ClampWrappedPeriod(new DateOnly(_wrappedPeriod.Year, _wrappedPeriod.Month, 1));
        RefreshWrappedFilterStates();
        RefreshWrappedView();
    }

    private void MoveWrappedPeriod(int direction)
    {
        _wrappedPeriod = ClampWrappedPeriod(_wrappedMode == "Anno"
            ? _wrappedPeriod.AddYears(direction)
            : _wrappedPeriod.AddMonths(direction));
        RefreshWrappedView();
    }

    private void RefreshWrappedFilterStates()
    {
        OnPropertyChanged(nameof(IsWrappedMonthlyActive));
        OnPropertyChanged(nameof(IsWrappedYearlyActive));
    }

    private void RefreshWrappedView()
    {
        var start = _wrappedMode == "Anno"
            ? new DateOnly(_wrappedPeriod.Year, 1, 1)
            : new DateOnly(_wrappedPeriod.Year, _wrappedPeriod.Month, 1);
        var end = _wrappedMode == "Anno"
            ? start.AddYears(1)
            : start.AddMonths(1);
        WrappedPeriodLabel = _wrappedMode == "Anno"
            ? start.Year.ToString(CultureInfo.InvariantCulture)
            : CultureInfo.GetCultureInfo("it-IT").TextInfo.ToTitleCase(start.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT")));

        var entries = Statistics.History
            .Where(IsCompetitiveEntry)
            .Select(entry => new { Entry = entry, Date = TryGetHistoryDate(entry.Date) })
            .Where(item => item.Date is not null && item.Date.Value >= start && item.Date.Value < end)
            .Select(item => item.Entry)
            .ToList();

        var played = entries.Count;
        var won = entries.Count(entry => entry.Won);
        var losses = played - won;
        var totalScore = entries.Sum(GetEntryScore);
        var wonAttempts = entries.Where(entry => entry.Won && entry.Attempts is >= 1 and <= 6).Select(entry => entry.Attempts).ToList();
        var quickWins = entries.Count(entry => entry.Won && entry.Attempts is >= 1 and <= 3);
        var bestStreak = CalculateBestPeriodStreak(entries);

        WrappedStatCards[0].Value = totalScore.ToString(CultureInfo.InvariantCulture);
        WrappedStatCards[1].Value = played.ToString(CultureInfo.InvariantCulture);
        WrappedStatCards[2].Value = won.ToString(CultureInfo.InvariantCulture);
        WrappedStatCards[3].Value = losses.ToString(CultureInfo.InvariantCulture);
        WrappedStatCards[4].Value = played == 0 ? "0%" : $"{(int)Math.Round(won * 100.0 / played)}%";
        WrappedStatCards[5].Value = wonAttempts.Count == 0 ? "n/d" : wonAttempts.Average().ToString("0.0", CultureInfo.InvariantCulture);
        WrappedStatCards[6].Value = bestStreak.ToString(CultureInfo.InvariantCulture);
        WrappedStatCards[7].Value = quickWins.ToString(CultureInfo.InvariantCulture);
        WrappedStatCards[8].Value = played == 0 ? "0" : (totalScore * 1.0 / played).ToString("0.0", CultureInfo.InvariantCulture);
        WrappedMonthlyScoresText = _wrappedMode == "Anno" ? BuildYearlyMonthlyScoreText(start) : string.Empty;

        var maxWrappedWins = Math.Max(1, wonAttempts.GroupBy(attempt => attempt).Select(group => group.Count()).DefaultIfEmpty(0).Max());
        for (var i = 0; i < WrappedWinRows.Count; i++)
        {
            var attempts = i + 1;
            var count = wonAttempts.Count(value => value == attempts);
            WrappedWinRows[i].Wins = count;
            WrappedWinRows[i].BarWidth = count == 0 ? 18 : Math.Max(34, count * 220.0 / maxWrappedWins);
        }

        var timedEntries = entries
            .Where(entry => entry.DurationSeconds is not null)
            .Select(entry => entry.DurationSeconds!.Value)
            .ToList();
        if (timedEntries.Count == 0)
        {
            WrappedTimeCards[0].Value = "n/d";
            WrappedTimeCards[1].Value = "n/d";
            WrappedTimeCards[2].Value = "n/d";
            WrappedTimeCards[3].Value = "n/d";
            return;
        }

        WrappedTimeCards[0].Value = FormatDuration((int)Math.Round(timedEntries.Average()));
        WrappedTimeCards[1].Value = FormatDuration(timedEntries.Min());
        WrappedTimeCards[2].Value = FormatDuration(timedEntries.Max());
        WrappedTimeCards[3].Value = FormatDuration(timedEntries.Sum());
    }

    private void CheckPendingMonthlyRecap()
    {
        var currentMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lastShown = ParseMonthKey(Statistics.LastMonthlyRecapShown);
        var pendingMonth = Statistics.History
            .Where(IsCompetitiveEntry)
            .Select(entry => TryGetHistoryDate(entry.Date))
            .Where(date => date is not null)
            .Select(date => new DateOnly(date!.Value.Year, date.Value.Month, 1))
            .Where(month => month < currentMonth && (lastShown is null || month > lastShown.Value))
            .Distinct()
            .OrderByDescending(month => month)
            .FirstOrDefault();

        if (pendingMonth == default)
        {
            return;
        }

        BuildMonthlyRecap(pendingMonth);
        Statistics.LastMonthlyRecapShown = FormatMonthKey(pendingMonth);
        _storage.SaveStatistics(Statistics);
        IsSplashVisible = false;
        IsMonthlyRecapVisible = true;
    }

    private void BuildMonthlyRecap(DateOnly month)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        MonthlyRecapTitle = culture.TextInfo.ToTitleCase(start.ToString("MMMM yyyy", culture));

        var entries = Statistics.History
            .Where(IsCompetitiveEntry)
            .Select(entry => new { Entry = entry, Date = TryGetHistoryDate(entry.Date) })
            .Where(item => item.Date is not null && item.Date.Value >= start && item.Date.Value < end)
            .Select(item => item.Entry)
            .ToList();
        var played = entries.Count;
        var won = entries.Count(entry => entry.Won);
        var losses = played - won;
        var wonAttempts = entries.Where(entry => entry.Won && entry.Attempts is >= 1 and <= 6).Select(entry => entry.Attempts).ToList();
        var timedEntries = entries.Where(entry => entry.DurationSeconds is not null).Select(entry => entry.DurationSeconds!.Value).ToList();

        MonthlyRecapCards[0].Value = played.ToString(CultureInfo.InvariantCulture);
        MonthlyRecapCards[1].Value = won.ToString(CultureInfo.InvariantCulture);
        MonthlyRecapCards[2].Value = losses.ToString(CultureInfo.InvariantCulture);
        MonthlyRecapCards[3].Value = played == 0 ? "0%" : $"{(int)Math.Round(won * 100.0 / played)}%";
        MonthlyRecapCards[4].Value = wonAttempts.Count == 0 ? "n/d" : wonAttempts.Average().ToString("0.00", CultureInfo.InvariantCulture);
        MonthlyRecapCards[5].Value = CalculateBestPeriodStreak(entries).ToString(CultureInfo.InvariantCulture);
        MonthlyRecapCards[6].Value = entries.Count(entry => entry.Won && entry.Attempts is >= 1 and <= 3).ToString(CultureInfo.InvariantCulture);
        MonthlyRecapCards[7].Value = entries.Sum(GetEntryScore).ToString(CultureInfo.InvariantCulture);
        MonthlyRecapCards[8].Value = timedEntries.Count == 0 ? "n/d" : FormatDuration((int)Math.Round(timedEntries.Average()));
    }

    private void OpenMonthlyRecapWrapped()
    {
        var recapMonth = ParseMonthKey(Statistics.LastMonthlyRecapShown);
        if (recapMonth is not null)
        {
            _wrappedMode = "Mese";
            _wrappedPeriod = recapMonth.Value;
            RefreshWrappedFilterStates();
            RefreshWrappedView();
        }

        IsMonthlyRecapVisible = false;
        IsWrappedVisible = true;
    }

    private void ResetGameData()
    {
        _dailyGuesses.Clear();
        _bonusGuesses.Clear();
        _infiniteGuesses.Clear();
        _dailyStatus = GameStatus.Playing;
        _bonusStatus = GameStatus.Playing;
        _infiniteStatus = GameStatus.Playing;
        _dailyElapsedSeconds = 0;
        _dailyTimerStarted = false;
        _dailyTimerStartedAt = null;
        _bonusElapsedSeconds = 0;
        _bonusTimerStarted = false;
        _bonusTimerStartedAt = null;
        _infiniteElapsedSeconds = 0;
        _infiniteTimerStarted = false;
        _infiniteTimerStartedAt = null;
        _isBonusActive = false;
        _isInfiniteActive = false;
        _isBonusUnlocked = false;
        _dailyStatisticsAlreadyRecorded = false;
        _infiniteSolution = string.Empty;

        Statistics.Played = 0;
        Statistics.Won = 0;
        Statistics.Points = 0;
        Statistics.BonusPlayed = 0;
        Statistics.BonusWon = 0;
        Statistics.TwoPointDays = 0;
        Statistics.InfinitePlayed = 0;
        Statistics.InfiniteWon = 0;
        Statistics.CurrentStreak = 0;
        Statistics.BestStreak = 0;
        Statistics.WinDistribution = new int[6];
        Statistics.InfiniteWinDistribution = new int[6];
        Statistics.LastPlayedDate = string.Empty;
        Statistics.LastWinDate = string.Empty;
        Statistics.LastMonthlyRecapShown = string.Empty;
        Statistics.History.Clear();

        IsResetConfirmVisible = false;
        IsMonthlyRecapVisible = false;
        StartNewGameForDate(DateOnly.FromDateTime(DateTime.Today), "Dati di gioco azzerati.");
        _storage.SaveStatistics(Statistics);
        ShowToast("Dati di gioco azzerati.");
    }

    private static DateOnly? TryGetHistoryDate(string value)
    {
        if (DateOnly.TryParse(value, out var date))
        {
            return date;
        }

        return DateTime.TryParse(value, out var dateTime)
            ? DateOnly.FromDateTime(dateTime)
            : null;
    }

    private static bool IsBeforeOfficialStart(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               TryGetHistoryDate(value) is { } date &&
               date < OfficialStartDate;
    }

    private static bool IsOnOrAfterOfficialStart(GameHistoryEntry entry)
    {
        return TryGetHistoryDate(entry.Date) is { } date && date >= OfficialStartDate;
    }

    private DateOnly ClampWrappedPeriod(DateOnly period)
    {
        if (_wrappedMode == "Anno")
        {
            return period.Year < OfficialStartDate.Year
                ? new DateOnly(OfficialStartDate.Year, 1, 1)
                : new DateOnly(period.Year, 1, 1);
        }

        var normalized = new DateOnly(period.Year, period.Month, 1);
        return normalized < OfficialStartDate ? OfficialStartDate : normalized;
    }

    private static string FormatMonthKey(DateOnly month)
    {
        return month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private static DateOnly? ParseMonthKey(string value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var month)
            ? new DateOnly(month.Year, month.Month, 1)
            : null;
    }

    private static int CalculateBestPeriodStreak(IEnumerable<GameHistoryEntry> entries)
    {
        var dailyResults = entries
            .Where(entry => !entry.IsBonus && !entry.IsInfinite)
            .Select(entry => new { Entry = entry, Date = TryGetHistoryDate(entry.Date) })
            .Where(item => item.Date is not null)
            .GroupBy(item => item.Date!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new { Date = group.Key, Won = group.Any(item => item.Entry.Won) })
            .ToList();

        var best = 0;
        var current = 0;
        DateOnly? previous = null;
        foreach (var item in dailyResults)
        {
            if (!item.Won)
            {
                current = 0;
                previous = item.Date;
                continue;
            }

            current = previous == item.Date.AddDays(-1) ? current + 1 : 1;
            best = Math.Max(best, current);
            previous = item.Date;
        }

        return best;
    }

    private string BuildYearlyMonthlyScoreText(DateOnly yearStart)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        var firstMonthOffset = yearStart.Year == OfficialStartDate.Year ? OfficialStartDate.Month - 1 : 0;
        var parts = Enumerable.Range(firstMonthOffset, 12 - firstMonthOffset)
            .Select(offset =>
            {
                var month = yearStart.AddMonths(offset);
                var score = GetPeriodScore(month, month.AddMonths(1));
                return $"{culture.TextInfo.ToTitleCase(month.ToString("MMM", culture))}: {score}";
            });

        var topMonth = Enumerable.Range(firstMonthOffset, 12 - firstMonthOffset)
            .Select(offset =>
            {
                var month = yearStart.AddMonths(offset);
                return new { Month = month, Score = GetPeriodScore(month, month.AddMonths(1)) };
            })
            .OrderByDescending(item => item.Score)
            .First();

        var topLabel = culture.TextInfo.ToTitleCase(topMonth.Month.ToString("MMMM", culture));
        return $"Mese migliore: {topLabel} ({topMonth.Score} pt)  |  {string.Join("  ·  ", parts)}";
    }

    private void RefreshHistoryView()
    {
        HistoryRows.Clear();
        foreach (var entry in Statistics.History
                     .Where(item => item.Won || item.Guesses.Count >= 6)
                     .Where(MatchesHistoryFilter)
                     .OrderByDescending(item => item.Date)
                     .ThenBy(item => item.IsBonus ? 1 : 0))
        {
            var date = DateOnly.TryParse(entry.Date, out var parsedDate)
                ? parsedDate.ToString("dd/MM/yyyy")
                : DateTime.TryParse(entry.Date, out var parsedDateTime)
                ? parsedDateTime.ToString("dd/MM/yyyy HH:mm")
                : entry.Date;
            var result = entry.Won ? "Vinta" : "Persa";
            var attempts = entry.Won ? $"{entry.Attempts}/6" : "-/6";
            var guesses = entry.Guesses.Count == 0
                ? "Nessun tentativo salvato"
                : string.Join("  ", entry.Guesses.Select(guess => guess.ToUpperInvariant()));
            var mode = entry.IsBonus
                ? $"Bonus random · {entry.WordLength} lettere"
                : "Giornaliera";
            if (entry.IsInfinite)
            {
                mode = "Infinita";
            }

            var points = GetEntryScore(entry);

            HistoryRows.Add(new HistoryEntryViewModel(
                date,
                entry.Solution.ToUpperInvariant(),
                result,
                attempts,
                guesses,
                mode,
                IsCompetitiveEntry(entry) ? FormatPoints(points) : string.Empty,
                entry.DurationSeconds is not null ? FormatDuration(entry.DurationSeconds.Value) : string.Empty,
                BuildShareText(entry)));
        }

        IsHistoryEmptyVisible = HistoryRows.Count == 0;
        HistoryEmptyMessage = _historyFilter == "Infinite" && Statistics.InfinitePlayed > 0
            ? "Le partite infinite precedenti hanno solo statistiche: le parole non erano ancora salvate nello storico. Le prossime infinite completate appariranno qui."
            : "Nessuna partita salvata per questo filtro.";
    }

    private void ShowHistory()
    {
        EnsureCompletedInfiniteInHistory();
        RefreshHistoryView();
        IsHistoryVisible = true;
    }

    private void EnsureCompletedInfiniteInHistory()
    {
        if (_infiniteStatus == GameStatus.Playing ||
            _infiniteGuesses.Count == 0 ||
            string.IsNullOrWhiteSpace(_infiniteSolution) ||
            Statistics.History.Any(IsSameInfiniteHistoryEntry))
        {
            return;
        }

        UpsertHistory(CreateInfiniteHistoryEntry());
        _storage.SaveStatistics(Statistics);
    }

    private bool IsSameInfiniteHistoryEntry(GameHistoryEntry entry)
    {
        return entry.IsInfinite &&
               string.Equals(entry.Solution, _infiniteSolution, StringComparison.OrdinalIgnoreCase) &&
               entry.Guesses.SequenceEqual(_infiniteGuesses);
    }

    private bool MatchesHistoryFilter(GameHistoryEntry entry)
    {
        return _historyFilter switch
        {
            "Giornaliere" => !entry.IsBonus && !entry.IsInfinite,
            "Bonus" => entry.IsBonus,
            "Infinite" => entry.IsInfinite,
            "Vinte" => entry.Won,
            "Perse" => !entry.Won,
            _ => true
        };
    }

    private void RefreshHistoryFilterStates()
    {
        OnPropertyChanged(nameof(IsHistoryFilterAllActive));
        OnPropertyChanged(nameof(IsHistoryFilterDailyActive));
        OnPropertyChanged(nameof(IsHistoryFilterBonusActive));
        OnPropertyChanged(nameof(IsHistoryFilterInfiniteActive));
        OnPropertyChanged(nameof(IsHistoryFilterWonActive));
        OnPropertyChanged(nameof(IsHistoryFilterLostActive));
        OnPropertyChanged(nameof(IsInfiniteHistoryStatsVisible));
    }

    private void CopyCurrentResult()
    {
        if (EnsureCurrentGame())
        {
            return;
        }

        var entry = _isInfiniteActive
            ? CreateInfiniteHistoryEntry()
            : _isBonusActive
            ? CreateBonusHistoryEntry(_bonusStatus == GameStatus.Won, _bonusStatus == GameStatus.Won ? _bonusGuesses.Count : 0)
            : CreateDailyHistoryEntry(_dailyStatus == GameStatus.Won, _dailyStatus == GameStatus.Won ? _dailyGuesses.Count : 0);
        CopyText(BuildShareText(entry));
    }

    private void CopyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_isClipboardCopyRunning)
        {
            return;
        }

        _isClipboardCopyRunning = true;
        var thread = new Thread(() =>
        {
            var copied = TryCopyToClipboard(text);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _isClipboardCopyRunning = false;
                if (copied)
                {
                    ShowToast("Risultato copiato.");
                }
            });
        });
        thread.IsBackground = true;
        thread.Start();
    }

    private async void ShowToast(string message)
    {
        var version = ++_toastVersion;
        ToastMessage = message;
        IsToastVisible = true;
        await Task.Delay(1800);
        if (version == _toastVersion)
        {
            IsToastVisible = false;
        }
    }

    private void ShowSettings()
    {
        CloseOverlays();
        ProfileNameDraft = PlayerName;
        ProfileError = string.Empty;
        UpdateStatusText = string.Empty;
        IsUpdateStatusVisible = false;
        IsSettingsVisible = true;
    }

    private void SaveProfileName()
    {
        var name = ProfileNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ProfileError = "Inserisci un nome.";
            return;
        }

        PlayerName = name;
        ProfileNameDraft = name;
        ProfileError = string.Empty;
        IsProfileDialogVisible = false;
        _userSettings.PlayerName = name;
        if (string.IsNullOrWhiteSpace(_userSettings.LastSeenChangelogVersion))
        {
            _userSettings.LastSeenChangelogVersion = _updateService.CurrentVersionText;
        }

        _storage.SaveUserSettings(_userSettings);
        ShowToast("Nome salvato.");
    }

    private void ShowChangelogIfNeeded(bool userSettingsExists)
    {
        var currentVersion = _updateService.CurrentVersionText;
        if (!userSettingsExists || string.IsNullOrWhiteSpace(currentVersion) ||
            _userSettings.LastSeenChangelogVersion == currentVersion)
        {
            return;
        }

        var entry = _changelogService.GetEntry(currentVersion);
        if (entry is null)
        {
            _userSettings.LastSeenChangelogVersion = currentVersion;
            _storage.SaveUserSettings(_userSettings);
            return;
        }

        CloseOverlays();
        IsSplashVisible = false;
        IsProfileDialogVisible = false;
        ChangelogTitle = string.IsNullOrWhiteSpace(entry.Title)
            ? $"Novita' della versione {entry.Version}"
            : entry.Title;
        ChangelogText = string.Join(Environment.NewLine, entry.Items.Select(item => $"- {item}"));
        IsChangelogVisible = true;
    }

    private void DismissChangelog()
    {
        IsChangelogVisible = false;
        _userSettings.LastSeenChangelogVersion = _updateService.CurrentVersionText;
        _storage.SaveUserSettings(_userSettings);
        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            IsProfileDialogVisible = true;
        }
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (_updatePromptShownThisSession)
        {
            return;
        }

        var result = await _updateService.CheckForUpdatesAsync();
        if (result.Status == AppUpdateCheckStatus.Available && result.Update is not null)
        {
            ShowUpdateDialog(result.Update);
        }
    }

    private async Task CheckForUpdatesManuallyAsync()
    {
        if (IsUpdateBusy)
        {
            return;
        }

        IsUpdateBusy = true;
        IsUpdateStatusVisible = true;
        UpdateStatusText = "Controllo aggiornamenti in corso...";

        var result = await _updateService.CheckForUpdatesAsync();
        IsUpdateBusy = false;

        switch (result.Status)
        {
            case AppUpdateCheckStatus.Available when result.Update is not null:
                UpdateStatusText = "Aggiornamento disponibile.";
                ShowUpdateDialog(result.Update);
                break;
            case AppUpdateCheckStatus.NoUpdates:
                UpdateStatusText = "Hai gia' l'ultima versione.";
                ShowToast("Nessun aggiornamento disponibile.");
                break;
            case AppUpdateCheckStatus.NotInstalled:
                UpdateStatusText = "Gli aggiornamenti funzionano dopo l'installazione con Setup.";
                ShowToast("Versione di sviluppo: updater non attivo.");
                break;
            default:
                UpdateStatusText = $"Aggiornamento non riuscito: {result.ErrorMessage ?? "errore sconosciuto"}";
                ShowToast("Controllo aggiornamenti non riuscito.");
                break;
        }
    }

    private void ShowUpdateDialog(UpdateInfo update)
    {
        _pendingUpdate = update;
        _updatePromptShownThisSession = true;
        CloseOverlays();
        IsSplashVisible = false;
        IsProfileDialogVisible = false;
        UpdateDialogTitle = "Aggiornamento disponibile";
        AvailableVersionText = $"Nuova versione {update.TargetFullRelease.Version}";
        UpdateDialogMessage = "Puoi aggiornare ora e l'app si riaprira' automaticamente. Storico, punti e impostazioni restano nella cartella dati locale.";
        UpdateReleaseNotes = string.IsNullOrWhiteSpace(update.TargetFullRelease.NotesMarkdown)
            ? "Note di versione non disponibili."
            : update.TargetFullRelease.NotesMarkdown;
        UpdateProgressText = string.Empty;
        UpdateProgressValue = 0;
        IsUpdateBusy = false;
        IsUpdateDialogVisible = true;
    }

    private async Task InstallPendingUpdateAsync()
    {
        if (_pendingUpdate is null || IsUpdateBusy)
        {
            return;
        }

        IsUpdateBusy = true;
        UpdateProgressText = "Download aggiornamento...";
        UpdateProgressValue = 0;
        PersistActiveGameTime();

        var result = await _updateService.DownloadAndRestartAsync(
            _pendingUpdate,
            progress => Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateProgressValue = Math.Clamp(progress, 0, 100);
                UpdateProgressText = $"Download {UpdateProgressValue}%";
            }));

        if (!result.WasStarted)
        {
            IsUpdateBusy = false;
            UpdateProgressText = string.Empty;
            UpdateProgressValue = 0;
            UpdateDialogMessage = $"Aggiornamento non riuscito: {result.ErrorMessage ?? "errore sconosciuto"}";
        }
    }

    private void DismissUpdateDialog()
    {
        IsUpdateDialogVisible = false;
        IsUpdateBusy = false;
        UpdateProgressText = string.Empty;
        UpdateProgressValue = 0;
        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            IsProfileDialogVisible = true;
        }
    }

    private static bool TryCopyToClipboard(string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (TrySetClipboardTextNative(text))
            {
                return true;
            }

            Thread.Sleep(120);
        }

        return false;
    }

    private static bool TrySetClipboardTextNative(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        IntPtr memory = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
            {
                return false;
            }

            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            memory = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (memory == IntPtr.Zero)
            {
                return false;
            }

            var lockedMemory = GlobalLock(memory);
            if (lockedMemory == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                Marshal.Copy(bytes, 0, lockedMemory, bytes.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (SetClipboardData(CfUnicodeText, memory) == IntPtr.Zero)
            {
                return false;
            }

            memory = IntPtr.Zero;
            return true;
        }
        finally
        {
            CloseClipboard();
            if (memory != IntPtr.Zero)
            {
                GlobalFree(memory);
            }
        }
    }

    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private string BuildShareText(GameHistoryEntry entry)
    {
        var mode = entry.IsInfinite
            ? "Infinita"
            : entry.IsBonus
            ? $"Bonus random {entry.WordLength}"
            : "Giornaliera";
        var score = entry.Won ? $"{entry.Attempts}/6" : "X/6";
        var timer = entry.DurationSeconds is not null ? $" - {FormatDuration(entry.DurationSeconds.Value)}" : string.Empty;
        var rows = entry.Guesses.Select(guess => BuildShareRow(guess, entry.Solution));
        var header = $"Wordle Italiano - {mode} - {entry.Date} - {score}{timer}";
        if (!IsCompetitiveEntry(entry))
        {
            return $"{header}{Environment.NewLine}" +
                   string.Join(Environment.NewLine, rows);
        }

        return $"{header}{Environment.NewLine}" +
               $"+{GetEntryScore(entry)} pt | {GetShareMonthLabel(entry)}: {GetShareMonthScore(entry)} pt{Environment.NewLine}" +
               string.Join(Environment.NewLine, rows);
    }

    private static int CalculateScore(int wordLength, int attempts)
    {
        if (attempts is < 1 or > 6 || wordLength <= 0)
        {
            return 0;
        }

        return wordLength * (7 - attempts);
    }

    private static int GetEntryScore(GameHistoryEntry entry)
    {
        if (!IsCompetitiveEntry(entry) || !entry.Won)
        {
            return 0;
        }

        if (entry.ScoreEarned is > 0)
        {
            return entry.ScoreEarned.Value;
        }

        if (entry.Points > 1)
        {
            return entry.Points;
        }

        return CalculateScore(entry.WordLength, entry.Attempts);
    }

    private static bool IsCompetitiveEntry(GameHistoryEntry entry)
    {
        return !entry.IsInfinite;
    }

    private static string FormatPoints(int points)
    {
        return points == 1 ? "1 punto" : $"{points} punti";
    }

    private int GetPeriodScore(DateOnly start, DateOnly end)
    {
        return Statistics.History
            .Where(IsCompetitiveEntry)
            .Select(entry => new { Entry = entry, Date = TryGetHistoryDate(entry.Date) })
            .Where(item => item.Date is not null && item.Date.Value >= start && item.Date.Value < end)
            .Sum(item => GetEntryScore(item.Entry));
    }

    private int GetShareMonthScore(GameHistoryEntry entry)
    {
        var date = TryGetHistoryDate(entry.Date) ?? DateOnly.FromDateTime(DateTime.Today);
        var start = new DateOnly(date.Year, date.Month, 1);
        var end = start.AddMonths(1);
        var historyTotal = GetPeriodScore(start, end);
        var currentEntryAlreadySaved = Statistics.History.Any(item =>
            item.Date == entry.Date &&
            item.IsBonus == entry.IsBonus &&
            item.IsInfinite == entry.IsInfinite);
        return currentEntryAlreadySaved ? historyTotal : historyTotal + GetEntryScore(entry);
    }

    private static string GetShareMonthLabel(GameHistoryEntry entry)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        var date = TryGetHistoryDate(entry.Date) ?? DateOnly.FromDateTime(DateTime.Today);
        return culture.TextInfo.ToTitleCase(date.ToString("MMMM", culture));
    }

    private static string BuildShareRow(string guess, string solution)
    {
        var states = GuessEvaluator.Evaluate(guess, solution);
        return string.Concat(states.Select(state => state switch
        {
            TileState.Correct => "🟩",
            TileState.Present => "🟨",
            _ => "⬛"
        }));
    }

    private void UpsertHistory(GameHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Date) ||
            string.IsNullOrWhiteSpace(entry.Solution) ||
            (!entry.Won && entry.Guesses.Count < 6))
        {
            return;
        }

        if (!entry.IsInfinite)
        {
            Statistics.History.RemoveAll(item => item.Date == entry.Date && item.IsBonus == entry.IsBonus && !item.IsInfinite);
        }

        Statistics.History.Add(entry);
        Statistics.History = Statistics.History
            .OrderByDescending(item => item.Date)
            .Take(365 * 2)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.IsBonus ? 1 : 0)
            .ToList();
    }

    private void CloseOverlays()
    {
        IsStatisticsVisible = false;
        IsHistoryVisible = false;
        IsHelpVisible = false;
        IsWrappedVisible = false;
        IsMonthlyRecapVisible = false;
        IsResetConfirmVisible = false;
        IsSettingsVisible = false;
        IsUpdateDialogVisible = false;
        IsChangelogVisible = false;
    }

    private static AppSettings LoadSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var settings = new AppSettings();
        if (root.TryGetProperty("baseDate", out var baseDate) &&
            DateOnly.TryParse(baseDate.GetString(), out var parsed))
        {
            settings.BaseDate = parsed;
        }

        if (root.TryGetProperty("updateRepositoryUrl", out var updateRepositoryUrl))
        {
            settings.UpdateRepositoryUrl = updateRepositoryUrl.GetString() ?? settings.UpdateRepositoryUrl;
        }

        if (root.TryGetProperty("enableAutomaticUpdateChecks", out var automaticUpdateChecks))
        {
            settings.EnableAutomaticUpdateChecks = automaticUpdateChecks.GetBoolean();
        }

        return settings;
    }
}
