namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaStoreIdResolver
{
    internal static string? Resolve(string? baseSlug, string originId)
    {
        if (!string.IsNullOrWhiteSpace(baseSlug))
            return baseSlug.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(originId))
            return originId.Trim();

        return null;
    }

    internal static IEnumerable<string> GetLookupCandidates(string storeId)
    {
        yield return storeId;

        var lower = storeId.ToLowerInvariant();
        if (!string.Equals(lower, storeId, StringComparison.Ordinal))
            yield return lower;
    }
}
