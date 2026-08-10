using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface IGameSaveDiscoveryService
{
    /// <summary>
    /// Resolves every entry of the embedded save-path database: expands environment
    /// variables and reports whether the resolved folder currently exists on disk.
    /// </summary>
    Task<IReadOnlyList<GameSaveInfo>> DiscoverAsync(CancellationToken ct = default);

    /// <summary>
    /// Finds the first known save folder for a game title (exact normalized title or
    /// alias match against the embedded database), or <c>null</c> when unknown.
    /// </summary>
    Task<GameSaveInfo?> FindForTitleAsync(string title, CancellationToken ct = default);

    /// <summary>Creates a user-supplied save-folder candidate for the manual flow.</summary>
    GameSaveInfo CreateManual(string title, string sourcePath);
}
