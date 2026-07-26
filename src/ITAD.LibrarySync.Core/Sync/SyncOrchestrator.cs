using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class SyncOrchestrator(
    IItadApiClient api,
    ShopIdResolver shopIds,
    IReadOnlyList<ILauncherReader> readers,
    ICollectionSyncService collectionSync,
    IWaitlistSyncService waitlistSync,
    IWaitlistCleanupService waitlistCleanup,
    IDelayProvider delayProvider,
    FileLogger logger) : ISyncOrchestrator
{
    private static readonly TimeSpan InterLauncherDelay = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default)
    {
        logger.LogInfo("Loading ITAD shop map…");
        var shopMap = await api.GetShopMapAsync(ct);
        shopIds.LoadFromShopMap(shopMap);
        logger.LogInfo($"Shop map loaded ({shopMap.Count} shops).");

        var selectedReaders = readers
            .Where(r => launchers is null || launchers.Contains(r.Launcher))
            .ToList();

        var results = new List<SyncResult>();
        var allOwned = new List<StoreGame>();

        for (var i = 0; i < selectedReaders.Count; i++)
        {
            var reader = selectedReaders[i];
            var launcherName = FormatLauncher(reader.Launcher);
            LauncherReadResult read;

            logger.LogInfo($"Reading {launcherName} library…");

            try
            {
                read = await reader.ReadAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError($"{launcherName}: library read failed — {ex.Message}");
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

            logger.LogInfo(
                $"{launcherName}: {read.Owned.Count} owned, {read.Wishlist.Count} wishlist" +
                (read.WishlistReadable ? string.Empty : " (wishlist unavailable)"));

            ItadSyncResponse? collectionResponse = null;
            ItadSyncResponse? waitlistResponse = null;
            string? syncError = read.Error;

            try
            {
                if (WaitlistFilter.ShouldSkipCollectionSync(read.Owned))
                {
                    logger.LogInfo($"{launcherName}: skipping collection sync (no owned games).");
                }
                else
                {
                    logger.LogInfo($"{launcherName}: syncing collection ({read.Owned.Count} games)…");
                    collectionResponse = await collectionSync.SyncAsync(read, ct);
                    logger.LogInfo(
                        $"{launcherName}: collection +{collectionResponse?.Added ?? 0}/-{collectionResponse?.Removed ?? 0} " +
                        $"(total {collectionResponse?.Total ?? 0})");
                }
            }
            catch (Exception ex)
            {
                syncError = ex.Message;
                logger.LogError($"{launcherName}: collection sync failed — {ex.Message}");
            }

            try
            {
                var filteredWishlist = WaitlistFilter.RemoveOwnedGames(read.Wishlist, read.Owned);
                if (WaitlistFilter.ShouldSkipWaitlistSync(read.WishlistReadable, filteredWishlist.Count))
                {
                    logger.LogInfo($"{launcherName}: skipping waitlist sync.");
                }
                else
                {
                    logger.LogInfo($"{launcherName}: syncing waitlist ({filteredWishlist.Count} games)…");
                    waitlistResponse = await waitlistSync.SyncAsync(read, ct);
                    logger.LogInfo(
                        $"{launcherName}: waitlist +{waitlistResponse?.Added ?? 0}/-{waitlistResponse?.Removed ?? 0} " +
                        $"(total {waitlistResponse?.Total ?? 0})");
                }
            }
            catch (Exception ex)
            {
                syncError = syncError is null ? ex.Message : $"{syncError}; {ex.Message}";
                logger.LogError($"{launcherName}: waitlist sync failed — {ex.Message}");
            }

            if (read.Error is null)
                allOwned.AddRange(read.Owned);

            results.Add(CreateSyncResult(read, collectionResponse, waitlistResponse, globalWaitlistRemoved: 0, syncError));

            if (i < selectedReaders.Count - 1)
            {
                logger.LogInfo($"Waiting {InterLauncherDelay.TotalSeconds:0} seconds before next launcher…");
                await delayProvider.DelayAsync(InterLauncherDelay, ct);
            }
        }

        logger.LogInfo("Cleaning owned games from global waitlist…");
        var globalRemoved = await waitlistCleanup.RemoveOwnedFromGlobalWaitlistAsync(allOwned, ct);
        logger.LogInfo($"Global waitlist cleanup removed {globalRemoved} game(s).");

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
        int globalWaitlistRemoved,
        string? syncError = null)
    {
        var isConnectPromptOnly = read.Owned.Count == 0 &&
                                  read.Wishlist.Count == 0 &&
                                  IsInformationalConnectMessage(read.Error);
        var effectiveError = syncError ?? (isConnectPromptOnly ? null : read.Error);

        return new(
            read.Launcher,
            Success: effectiveError is null,
            CollectionTotal: collection?.Total ?? 0,
            CollectionAdded: collection?.Added ?? 0,
            CollectionRemoved: collection?.Removed ?? 0,
            WaitlistTotal: waitlist?.Total ?? 0,
            WaitlistAdded: waitlist?.Added ?? 0,
            WaitlistRemoved: waitlist?.Removed ?? 0,
            GlobalWaitlistRemoved: globalWaitlistRemoved,
            Error: effectiveError);
    }

    private static bool IsInformationalConnectMessage(string? error) =>
        !string.IsNullOrWhiteSpace(error) && (
            error.Contains("Connect your", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Settings", StringComparison.OrdinalIgnoreCase));

    private static string FormatLauncher(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic",
        LauncherId.Ubisoft => "Ubisoft",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Microsoft",
        LauncherId.Ea => "EA App",
        _ => launcher.ToString()
    };
}
