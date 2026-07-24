using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public sealed class UpdateCheckerService(HttpClient httpClient, FileLogger? logger = null) : IUpdateCheckerService
{
    private const string GitHubLatestReleaseUrl = "https://api.github.com/repos/Tunamaran/ITAD.LibrarySync/releases/latest";
    private const string RepositoryReleasesUrl = "https://github.com/Tunamaran/ITAD.LibrarySync/releases";

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var currentVersion = GetCurrentAssemblyVersion();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubLatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("ITAD-LibrarySync");

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogInfo($"UpdateCheckerService: GitHub API returned status {response.StatusCode}.");
                return new UpdateCheckResult(false, currentVersion, currentVersion, RepositoryReleasesUrl, RepositoryReleasesUrl);
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseResponse>(cancellationToken: ct);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResult(false, currentVersion, currentVersion, RepositoryReleasesUrl, RepositoryReleasesUrl);
            }

            var rawTag = release.TagName.TrimStart('v', 'V');
            if (Version.TryParse(rawTag, out var latestParsed) && Version.TryParse(currentVersion, out var currentParsed))
            {
                var hasUpdate = latestParsed > currentParsed;
                return new UpdateCheckResult(
                    hasUpdate,
                    currentVersion,
                    release.TagName,
                    release.HtmlUrl ?? RepositoryReleasesUrl,
                    release.Assets?.FirstOrDefault()?.BrowserDownloadUrl ?? release.HtmlUrl ?? RepositoryReleasesUrl);
            }

            return new UpdateCheckResult(
                false,
                currentVersion,
                release.TagName,
                release.HtmlUrl ?? RepositoryReleasesUrl,
                RepositoryReleasesUrl);
        }
        catch (Exception ex)
        {
            logger?.LogError($"UpdateCheckerService: failed to check for updates — {ex.Message}");
            return new UpdateCheckResult(false, currentVersion, currentVersion, RepositoryReleasesUrl, RepositoryReleasesUrl);
        }
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ITADLibrarySyncUpdate");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "ITAD.LibrarySync.new.exe");

        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        var totalRead = 0L;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            totalRead += read;
            if (totalBytes > 0)
            {
                progress?.Report((double)totalRead / totalBytes * 100.0);
            }
        }

        return tempFile;
    }

    public void ApplyUpdateAndRestart(string downloadedFilePath)
    {
        var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(currentExe)) return;

        var script = $"Start-Sleep -Seconds 2; Copy-Item -Path '{downloadedFilePath}' -Destination '{currentExe}' -Force; Start-Process '{currentExe}'";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    public static string GetCurrentAssemblyVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }

    private sealed record GitHubReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] List<GitHubAssetResponse>? Assets);

    private sealed record GitHubAssetResponse(
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
