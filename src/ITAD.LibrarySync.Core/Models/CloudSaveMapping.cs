namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// Persisted link between an original save folder and its cloud-mirrored location.
/// </summary>
public sealed record CloudSaveMapping(
    string Title,
    string SourcePath,
    string TargetPath,
    CloudProvider Provider,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastVerified = null,
    string? BackupPath = null);
