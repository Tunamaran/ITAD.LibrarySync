namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// Standardized reasons why a game could not be matched in the ITAD catalog.
/// UI layer translates these enum values via LanguageManager.
/// </summary>
public enum UnmatchedReason
{
    /// <summary>Game's store ID was not found in ITAD's shop catalog lookup.</summary>
    NotInCatalog,

    /// <summary>Game's store ID resolved to a tracking/fallback ID (itadlibsync/...).</summary>
    TrackingIdFallback,

    /// <summary>ITAD API lookup returned no match for the given shop game ID.</summary>
    NoApiMatch
}
