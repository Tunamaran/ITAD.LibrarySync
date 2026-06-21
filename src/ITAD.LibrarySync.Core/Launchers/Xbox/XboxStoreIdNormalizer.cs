using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public sealed class XboxStoreIdNormalizer(IMicrosoftStoreCatalogClient catalog)
{
    public async Task<IReadOnlyList<StoreGame>> NormalizeAsync(
        IReadOnlyList<StoreGame> games,
        CancellationToken ct = default)
    {
        if (games.Count == 0)
            return games;

        var pfnToResolve = games
            .Select(game => game.StoreId)
            .Where(MicrosoftStoreId.IsPackageFamilyName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolvedPfns = pfnToResolve.Count > 0
            ? await catalog.ResolveStoreIdsByPfnAsync(pfnToResolve, ct)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normalized = new List<StoreGame>(games.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in games)
        {
            if (!TryResolveStoreId(game.StoreId, resolvedPfns, out var storeId))
                continue;

            if (!seen.Add(storeId))
                continue;

            normalized.Add(string.Equals(game.StoreId, storeId, StringComparison.OrdinalIgnoreCase)
                ? game
                : game with { StoreId = storeId });
        }

        return normalized;
    }

    private static bool TryResolveStoreId(
        string rawId,
        IReadOnlyDictionary<string, string> resolvedPfns,
        out string storeId)
    {
        if (MicrosoftStoreId.IsProductId(rawId))
        {
            storeId = rawId;
            return true;
        }

        if (MicrosoftStoreId.IsPackageFamilyName(rawId)
            && resolvedPfns.TryGetValue(rawId, out var resolved)
            && MicrosoftStoreId.IsProductId(resolved))
        {
            storeId = resolved;
            return true;
        }

        storeId = string.Empty;
        return false;
    }
}
