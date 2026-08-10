namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// Outcome of a migrate / restore operation for one save folder.
/// </summary>
public sealed record CloudSaveResult(
    string Title,
    string SourcePath,
    bool Success,
    string Message);
