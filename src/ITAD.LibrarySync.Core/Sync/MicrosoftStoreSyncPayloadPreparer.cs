using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Launchers.Xbox;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class MicrosoftStoreSyncPayloadPreparer(IItadApiClient api, FileLogger logger)
{
    private const string UnknownProductIdPrefix = "itadlibsync/";

    public async Task<IReadOnlyList<SyncGamePayload>> PrepareAsync(
        IReadOnlyList<SyncGamePayload> payloads,
        CancellationToken ct = default)
    {
        if (payloads.Count == 0)
            return payloads;

        var shopId = payloads[0].Shop;
        var lookupIds = payloads
            .SelectMany(payload => GetLookupCandidates(payload.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lookup = lookupIds.Count > 0
            ? await api.LookupShopGameIdsAsync(shopId, lookupIds, ct)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var prepared = new List<SyncGamePayload>(payloads.Count);
        foreach (var payload in payloads)
        {
            var id = ResolveSyncId(payload, lookup);
            var title = SyncTitleSanitizer.Sanitize(payload.Title);
            prepared.Add(payload with { Id = id, Title = title });
        }

        return prepared;
    }

    private string ResolveSyncId(SyncGamePayload payload, IReadOnlyDictionary<string, string?> lookup)
    {
        var knownId = FindKnownShopGameId(payload.Id, lookup);
        if (knownId is not null)
            return knownId;

        if (!MicrosoftStoreId.IsProductId(payload.Id))
            return payload.Id;

        var fallbackId = UnknownProductIdPrefix + payload.Id.ToUpperInvariant();
        logger.LogInfo(
            $"Microsoft: '{payload.Title}' ({payload.Id}) is not in ITAD's Microsoft Store catalog; syncing with tracking id '{fallbackId}'.");
        return fallbackId;
    }

    internal static string? FindKnownShopGameId(
        string storeId,
        IReadOnlyDictionary<string, string?> lookup)
    {
        foreach (var candidate in GetLookupCandidates(storeId))
        {
            if (lookup.TryGetValue(candidate, out var gameId) && !string.IsNullOrWhiteSpace(gameId))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> GetLookupCandidates(string storeId)
    {
        yield return storeId;
        yield return storeId.ToUpperInvariant();
        yield return storeId.ToLowerInvariant();
    }
}
