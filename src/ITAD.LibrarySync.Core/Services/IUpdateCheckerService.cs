using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface IUpdateCheckerService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default);
    Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken ct = default);
    void ApplyUpdateAndRestart(string downloadedFilePath);
}
