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

    public StorageService()
    {
        _folder = Environment.GetEnvironmentVariable("WORDLE_STORAGE_FOLDER")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WordleItaliano");
        _gamePath = Path.Combine(_folder, "game.json");
        _statsPath = Path.Combine(_folder, "statistics.json");
        _userSettingsPath = Path.Combine(_folder, "userSettings.json");
    }

    public bool UserSettingsExists => File.Exists(_userSettingsPath);

    public SavedGame? LoadGame()
    {
        return Load<SavedGame>(_gamePath);
    }

    public Statistics LoadStatistics()
    {
        return Load<Statistics>(_statsPath) ?? new Statistics();
    }

    public UserSettings LoadUserSettings()
    {
        return Load<UserSettings>(_userSettingsPath) ?? new UserSettings();
    }

    public void SaveGame(SavedGame game)
    {
        Save(_gamePath, game);
    }

    public void SaveStatistics(Statistics statistics)
    {
        Save(_statsPath, statistics);
    }

    public void SaveUserSettings(UserSettings settings)
    {
        Save(_userSettingsPath, settings);
    }

    private T? Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options);
        }
        catch
        {
            return default;
        }
    }

    private void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, JsonSerializer.Serialize(value, _options));
    }
}
