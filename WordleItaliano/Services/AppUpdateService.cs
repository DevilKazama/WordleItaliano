using System.Net.Http;
using System.Reflection;
using System.Text;
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
            var currentVersionText = TrimVersionMetadata(CurrentVersionText);
            if (!SemanticVersion.TryParse(currentVersionText, out var currentVersion) ||
                !SemanticVersion.TryParse(version, out var targetVersion))
            {
                return await GetSingleReleaseNotesAsync(version);
            }

            var releaseNotes = await GetReleaseNotesEntriesAsync();
            var relevantReleaseNotes = releaseNotes
                .Where(entry => entry.Version.CompareTo(currentVersion) > 0 &&
                                entry.Version.CompareTo(targetVersion) <= 0)
                .OrderBy(entry => entry.Version)
                .ToArray();

            if (relevantReleaseNotes.Length == 0)
            {
                return await GetSingleReleaseNotesAsync(version);
            }

            var builder = new StringBuilder();
            foreach (var entry in relevantReleaseNotes)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.AppendLine(entry.DisplayVersion);
                builder.Append(string.IsNullOrWhiteSpace(entry.Notes)
                    ? "Nessuna nota disponibile."
                    : entry.Notes);
            }

            return builder.ToString().Trim();
        }
        catch
        {
            return await GetSingleReleaseNotesAsync(version);
        }
    }

    private async Task<string> GetSingleReleaseNotesAsync(string version)
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
                $"https://api.github.com/repos/{owner}/{repository}/releases/tags/{Uri.EscapeDataString(tag)}");
            request.Headers.UserAgent.ParseAdd("WordleItaliano");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

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

    private async Task<IReadOnlyList<ReleaseNotesEntry>> GetReleaseNotesEntriesAsync()
    {
        if (!TryGetGitHubRepository(_repositoryUrl, out var owner, out var repository))
        {
            return [];
        }

        var entries = new List<ReleaseNotesEntry>();
        for (var page = 1; page <= 5; page++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repository}/releases?per_page=100&page={page}");
            request.Headers.UserAgent.ParseAdd("WordleItaliano");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return entries;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }

            var pageCount = 0;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                pageCount++;
                if (release.TryGetProperty("draft", out var draftElement) &&
                    draftElement.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                if (release.TryGetProperty("prerelease", out var prereleaseElement) &&
                    prereleaseElement.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                var tag = release.TryGetProperty("tag_name", out var tagElement)
                    ? tagElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(tag) ||
                    !SemanticVersion.TryParse(tag, out var semanticVersion))
                {
                    continue;
                }

                var body = release.TryGetProperty("body", out var bodyElement)
                    ? bodyElement.GetString()
                    : null;
                var displayVersion = tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? tag
                    : $"v{tag}";
                entries.Add(new ReleaseNotesEntry(
                    semanticVersion,
                    displayVersion,
                    CleanReleaseNotes(body)));
            }

            if (pageCount < 100)
            {
                break;
            }
        }

        return entries;
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

    private sealed record ReleaseNotesEntry(SemanticVersion Version, string DisplayVersion, string Notes);

    private sealed record SemanticVersion(int Major, int Minor, int Patch, int Revision) : IComparable<SemanticVersion>
    {
        public static bool TryParse(string? value, out SemanticVersion version)
        {
            version = new SemanticVersion(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[1..];
            }

            var metadataIndex = normalized.IndexOf('+', StringComparison.Ordinal);
            if (metadataIndex >= 0)
            {
                normalized = normalized[..metadataIndex];
            }

            var prereleaseIndex = normalized.IndexOf('-', StringComparison.Ordinal);
            if (prereleaseIndex >= 0)
            {
                normalized = normalized[..prereleaseIndex];
            }

            var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 4)
            {
                return false;
            }

            var numbers = new[] { 0, 0, 0, 0 };
            for (var index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out var number) || number < 0)
                {
                    return false;
                }

                numbers[index] = number;
            }

            version = new SemanticVersion(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        public int CompareTo(SemanticVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0)
            {
                return majorComparison;
            }

            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0)
            {
                return minorComparison;
            }

            var patchComparison = Patch.CompareTo(other.Patch);
            if (patchComparison != 0)
            {
                return patchComparison;
            }

            return Revision.CompareTo(other.Revision);
        }
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
