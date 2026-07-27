namespace ITAD.LibrarySync.Core.Models;

public sealed record UnmatchedTitle(
    LauncherId Launcher,
    string StoreId,
    string Title,
    string Reason,
    DateTime DetectedAt,
    UnmatchedReason ReasonCode = UnmatchedReason.NotInCatalog);
