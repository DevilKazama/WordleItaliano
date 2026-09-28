using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace WordleItaliano.Services;

public sealed class AppUpdateService
{
    private static readonly HttpClient HttpClient = new();
    private readonly UpdateManager _manager;
    private readonly string _repositoryUrl;

    public AppUpdateService(string repositoryUrl)
    {
        _repositoryUrl = repositoryUrl;
        _manager = new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));
    }

    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersionText =>
        _manager.CurrentVersion?.ToString() ?? GetAssemblyVersionText();

    public string DisplayVersionText => TrimVersionMetadata(CurrentVersionText);

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

    public async Task<string> GetReleaseNotesAsync(string version)
    {
        try
        {
            if (!TryGetGitHubRepository(_repositoryUrl, out var owner, out var repository))
            {
                return string.Empty;
            }

            var tag = version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? version
                : $"v{version}";
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repository}/releases/tags/{tag}");
            request.Headers.UserAgent.ParseAdd("WordleItaliano");

            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("body", out var bodyElement))
            {
                return string.Empty;
            }

            return CleanReleaseNotes(bodyElement.GetString());
        }
        catch
        {
            return string.Empty;
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

    private static string TrimVersionMetadata(string version)
    {
        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex > 0 ? version[..metadataIndex] : version;
    }

    private static bool TryGetGitHubRepository(string repositoryUrl, out string owner, out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;

        if (!Uri.TryCreate(repositoryUrl.TrimEnd('/'), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        owner = parts[0];
        repository = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4]
            : parts[1];
        return true;
    }

    private static string CleanReleaseNotes(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) ||
            body.StartsWith("Aggiornamento Wordle Italiano", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var lines = body
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.StartsWith("- ", StringComparison.Ordinal) ? $"• {line[2..].Trim()}" : line.TrimStart('#').Trim())
            .ToList();

        return string.Join(Environment.NewLine, lines);
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
