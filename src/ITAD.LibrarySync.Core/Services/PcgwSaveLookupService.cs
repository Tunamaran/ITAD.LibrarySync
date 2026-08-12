using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Composes the PCGamingWiki API client with the file cache and maps results
/// into the app's <see cref="GameSaveInfo"/> shape (env vars expanded,
/// existence checked).
/// </summary>
public sealed class PcgwSaveLookupService(
    IPcgwApiClient api,
    PcgwSavePathCache cache,
    FileLogger? logger = null) : IPcgwSaveLookupService
{
    public async Task<PcgwLookupResult> LookupAsync(string gameTitle, bool forceLive = false, CancellationToken ct = default)
    {
        if (!forceLive)
        {
            var (isHit, cached) = await cache.TryGetAsync(gameTitle, ct);
            if (isHit)
            {
                if (cached is null)
                {
                    logger?.LogInfo($"PCGW lookup: '{gameTitle}' served from negative cache.");
                    return new PcgwLookupResult(UsedLiveRequest: false, Info: null);
                }

                return new PcgwLookupResult(UsedLiveRequest: false, Info: ToGameSaveInfo(gameTitle, cached));
            }
        }

        var found = await api.LookupSavePathAsync(gameTitle, ct);
        await cache.PutAsync(gameTitle, found, ct);

        if (found is null)
        {
            logger?.LogInfo($"PCGW lookup: no save path found for '{gameTitle}'.");
            return new PcgwLookupResult(UsedLiveRequest: true, Info: null);
        }

        return new PcgwLookupResult(UsedLiveRequest: true, Info: ToGameSaveInfo(gameTitle, found));
    }

    private static GameSaveInfo ToGameSaveInfo(string gameTitle, PcgwSaveInfo info)
    {
        var candidates = info.CandidatePaths ?? [info.SavePath];
        string bestPath = info.SavePath;
        bool bestExists = false;

        foreach (var candidate in candidates)
        {
            var (resolvedPath, exists) = WildcardPathResolver.Resolve(candidate);
            if (exists)
            {
                bestPath = resolvedPath;
                bestExists = true;
                break;
            }

            if (string.Equals(bestPath, info.SavePath, StringComparison.OrdinalIgnoreCase))
            {
                bestPath = resolvedPath;
            }
        }

        return new GameSaveInfo(
            Title: gameTitle,
            SourcePath: bestPath,
            SourceUrl: info.SourceUrl,
            IsInstalled: true,
            Exists: bestExists);
    }
}
