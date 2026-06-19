using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class SyncOrchestrator(
    IItadApiClient api,
    ShopIdResolver shopIds,
    IReadOnlyList<ILauncherReader> readers,
    ICollectionSyncService collectionSync,
    IWaitlistSyncService waitlistSync,
    IWaitlistCleanupService waitlistCleanup,
    IDelayProvider delayProvider) : ISyncOrchestrator
{
    private static readonly TimeSpan InterLauncherDelay = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default)
    {
        var shopMap = await api.GetShopMapAsync(ct);
        shopIds.LoadFromShopMap(shopMap);

        var selectedReaders = readers
            .Where(r => launchers is null || launchers.Contains(r.Launcher))
            .ToList();

        var results = new List<SyncResult>();
        var allOwned = new List<StoreGame>();

        for (var i = 0; i < selectedReaders.Count; i++)
        {
            var reader = selectedReaders[i];
            LauncherReadResult read;

            try
            {
                read = await reader.ReadAsync(ct);
            }
            catch (Exception ex)
            {
                results.Add(new SyncResult(
                    reader.Launcher,
                    Success: false,
                    CollectionTotal: 0,
                    CollectionAdded: 0,
                    CollectionRemoved: 0,
                    WaitlistTotal: 0,
                    WaitlistAdded: 0,
                    WaitlistRemoved: 0,
                    GlobalWaitlistRemoved: 0,
                    Error: ex.Message));
                continue;
            }

            ItadSyncResponse? collectionResponse = null;
            if (!WaitlistFilter.ShouldSkipCollectionSync(read.Owned))
                collectionResponse = await collectionSync.SyncAsync(read, ct);

            var waitlistResponse = await waitlistSync.SyncAsync(read, ct);

            if (read.Error is null)
                allOwned.AddRange(read.Owned);

            results.Add(CreateSyncResult(read, collectionResponse, waitlistResponse, globalWaitlistRemoved: 0));

            if (i < selectedReaders.Count - 1)
                await delayProvider.DelayAsync(InterLauncherDelay, ct);
        }

        var globalRemoved = await waitlistCleanup.RemoveOwnedFromGlobalWaitlistAsync(allOwned, ct);

        if (results.Count > 0)
        {
            var first = results[0];
            results[0] = first with { GlobalWaitlistRemoved = globalRemoved };
        }

        return results;
    }

    private static SyncResult CreateSyncResult(
        LauncherReadResult read,
        ItadSyncResponse? collection,
        ItadSyncResponse? waitlist,
        int globalWaitlistRemoved) =>
        new(
            read.Launcher,
            Success: read.Error is null,
            CollectionTotal: collection?.Total ?? 0,
            CollectionAdded: collection?.Added ?? 0,
            CollectionRemoved: collection?.Removed ?? 0,
            WaitlistTotal: waitlist?.Total ?? 0,
            WaitlistAdded: waitlist?.Added ?? 0,
            WaitlistRemoved: waitlist?.Removed ?? 0,
            GlobalWaitlistRemoved: globalWaitlistRemoved,
            Error: read.Error);
}
