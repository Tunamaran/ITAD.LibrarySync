using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface IPcgwApiClient
{
    /// <summary>
    /// Looks up the Windows save folder for a game title on PCGamingWiki,
    /// or returns <c>null</c> when the page, the save section or a resolvable
    /// Windows path cannot be found. Never throws for lookup failures.
    /// </summary>
    Task<PcgwSaveInfo?> LookupSavePathAsync(string gameTitle, CancellationToken ct = default);
}
