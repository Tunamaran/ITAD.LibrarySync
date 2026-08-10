namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// A detected game save folder candidate (from the embedded save-path database or a manual entry).
/// </summary>
public sealed record GameSaveInfo(
    string Title,
    string SourcePath,
    string? SourceUrl = null,
    bool IsInstalled = false,
    bool Exists = false,
    bool IsManual = false);
