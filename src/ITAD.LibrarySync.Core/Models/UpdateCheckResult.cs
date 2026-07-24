namespace ITAD.LibrarySync.Core.Models;

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseNotesUrl,
    string DownloadUrl);
