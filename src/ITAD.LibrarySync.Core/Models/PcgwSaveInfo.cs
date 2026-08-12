namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// A save-folder location resolved from PCGamingWiki.
/// </summary>
public sealed record PcgwSaveInfo(
    string PageTitle,
    string SavePath,
    string? SourceUrl = null,
    IReadOnlyList<string>? CandidatePaths = null);
