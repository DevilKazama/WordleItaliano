using System.Reflection;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace WordleItaliano.Services;

public sealed class AppUpdateService
{
    private readonly UpdateManager _manager;

    public AppUpdateService(string repositoryUrl)
    {
        _manager = new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));
    }

    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersionText =>
        _manager.CurrentVersion?.ToString() ?? GetAssemblyVersionText();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            if (!_manager.IsInstalled)
            {
                return AppUpdateCheckResult.NotInstalled();
            }

            var update = await _manager.CheckForUpdatesAsync();
            return update is null
                ? AppUpdateCheckResult.NoUpdates()
                : AppUpdateCheckResult.Available(update);
        }
        catch (NotInstalledException)
        {
            return AppUpdateCheckResult.NotInstalled();
        }
        catch (Exception ex)
        {
            return AppUpdateCheckResult.Failed(ex.Message);
        }
    }

    public async Task<AppUpdateInstallResult> DownloadAndRestartAsync(UpdateInfo update, Action<int> progress)
    {
        try
        {
            await _manager.DownloadUpdatesAsync(update, progress);
            _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
            return AppUpdateInstallResult.Success();
        }
        catch (Exception ex)
        {
            return AppUpdateInstallResult.Failed(ex.Message);
        }
    }

    private static string GetAssemblyVersionText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString(3) ?? "sviluppo"
            : informationalVersion;
    }
}

public sealed record AppUpdateCheckResult(
    AppUpdateCheckStatus Status,
    UpdateInfo? Update,
    string? ErrorMessage)
{
    public static AppUpdateCheckResult Available(UpdateInfo update) =>
        new(AppUpdateCheckStatus.Available, update, null);

    public static AppUpdateCheckResult NoUpdates() =>
        new(AppUpdateCheckStatus.NoUpdates, null, null);

    public static AppUpdateCheckResult NotInstalled() =>
        new(AppUpdateCheckStatus.NotInstalled, null, null);

    public static AppUpdateCheckResult Failed(string errorMessage) =>
        new(AppUpdateCheckStatus.Failed, null, errorMessage);
}

public sealed record AppUpdateInstallResult(bool WasStarted, string? ErrorMessage)
{
    public static AppUpdateInstallResult Success() => new(true, null);
    public static AppUpdateInstallResult Failed(string errorMessage) => new(false, errorMessage);
}

public enum AppUpdateCheckStatus
{
    Available,
    NoUpdates,
    NotInstalled,
    Failed
}
