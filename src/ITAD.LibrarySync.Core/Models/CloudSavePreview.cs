namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// State of a save folder relative to its cloud backup, as computed by a dry run.
/// </summary>
public enum CloudSaveStatus
{
    /// <summary>Source exists, not migrated yet, no conflicts.</summary>
    Ready,

    /// <summary>Source folder does not exist on disk.</summary>
    SourceMissing,

    /// <summary>Source is a junction and an active mapping exists.</summary>
    AlreadyMigrated,

    /// <summary>Source is a junction but no mapping is recorded.</summary>
    OrphanJunction,

    /// <summary>A mapping exists but the source is no longer a junction.</summary>
    StaleMapping,

    /// <summary>The target cloud folder already exists and would not be overwritten.</summary>
    TargetConflict,

    /// <summary>The selected cloud provider root is not available.</summary>
    Unavailable
}

/// <summary>
/// Dry-run result describing what a migration would do for one save folder.
/// </summary>
public sealed record CloudSavePreview(
    string Title,
    string SourcePath,
    string TargetPath,
    CloudSaveStatus Status,
    string? Warning = null);
