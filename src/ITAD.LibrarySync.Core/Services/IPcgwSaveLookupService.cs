using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>Outcome of a save-folder lookup: whether a live request was made and what was found.</summary>
public sealed record PcgwLookupResult(bool UsedLiveRequest, GameSaveInfo? Info);

public interface IPcgwSaveLookupService
{
    /// <summary>
    /// Resolves the Windows save folder for a game title: cached result first,
    /// then a paced PCGamingWiki lookup (results are cached, negatives too).
    /// <see cref="PcgwLookupResult.UsedLiveRequest"/> is true only when an actual
    /// API request was made (cache hits do not consume a live-lookup budget).
    /// </summary>
    Task<PcgwLookupResult> LookupAsync(string gameTitle, CancellationToken ct = default);
}
