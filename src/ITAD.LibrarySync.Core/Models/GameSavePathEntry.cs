namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// A single entry of the embedded game-save-path database (derived from PCGamingWiki).
/// </summary>
public sealed record GameSavePathEntry(
    string Title,
    string[] Titles,
    string[] SavePaths,
    string? SourceUrl = null);
