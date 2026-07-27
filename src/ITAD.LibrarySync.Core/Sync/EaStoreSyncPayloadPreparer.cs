using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Launchers.Ea;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class EaStoreSyncPayloadPreparer(
    IItadApiClient api,
    FileLogger logger,
    IUnmatchedTitlesService? unmatchedTitles = null,
    ICustomMappingService? customMappings = null)
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
            .SelectMany(payload => EaStoreIdResolver.GetLookupCandidates(payload.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lookup = lookupIds.Count > 0
            ? await api.LookupShopGameIdsAsync(shopId, lookupIds, ct)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var prepared = new List<SyncGamePayload>(payloads.Count);
        var unmatchedList = new List<UnmatchedTitle>();

        foreach (var payload in payloads)
        {
            var custom = customMappings != null ? await customMappings.GetMappingAsync(LauncherId.Ea, payload.Id, ct) : null;
            string id;
            bool isUnmatched;

            if (custom != null && !string.IsNullOrWhiteSpace(custom.MappedId))
            {
                id = custom.MappedId;
                isUnmatched = false;
            }
            else
            {
                (id, isUnmatched) = ResolveSyncIdWithStatus(payload, lookup);
            }

            var title = SyncTitleSanitizer.Sanitize(payload.Title);
            prepared.Add(payload with { Id = id, Title = title });

            if (isUnmatched)
            {
                unmatchedList.Add(new UnmatchedTitle(
                    LauncherId.Ea,
                    payload.Id,
                    payload.Title,
                    nameof(UnmatchedReason.NotInCatalog),
                    DateTime.Now,
                    UnmatchedReason.NotInCatalog));
            }
        }

        if (unmatchedList.Count > 0 && unmatchedTitles is not null)
        {
            await unmatchedTitles.AddRangeAsync(unmatchedList, ct);
        }

        return prepared;
    }

    private (string Id, bool IsUnmatched) ResolveSyncIdWithStatus(SyncGamePayload payload, IReadOnlyDictionary<string, string?> lookup)
    {
        var knownId = FindKnownShopGameId(payload.Id, lookup);
        if (knownId is not null)
            return (knownId, false);

        var fallbackId = UnknownProductIdPrefix + payload.Id.ToLowerInvariant();
        logger.LogInfo(
            $"EA: '{payload.Title}' ({payload.Id}) is not in ITAD's EA Store catalog; syncing with tracking id '{fallbackId}'.");
        return (fallbackId, true);
    }

    internal static string? FindKnownShopGameId(
        string storeId,
        IReadOnlyDictionary<string, string?> lookup)
    {
        foreach (var candidate in EaStoreIdResolver.GetLookupCandidates(storeId))
        {
            if (lookup.TryGetValue(candidate, out var gameId) && !string.IsNullOrWhiteSpace(gameId))
                return candidate;
        }

        return null;
    }
}
