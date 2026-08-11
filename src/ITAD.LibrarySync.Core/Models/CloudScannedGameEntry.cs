namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// A persisted row of the last Cloud Saves game scan. Kept in settings so the
/// scanned game list survives app restarts and is only replaced by the next scan.
/// </summary>
public sealed class CloudScannedGameEntry
{
    public string Title { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    /// <summary>Detected or manually set save folder; empty when none is known.</summary>
    public string SourcePath { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public string StatusColor { get; set; } = "#64748B";
}
