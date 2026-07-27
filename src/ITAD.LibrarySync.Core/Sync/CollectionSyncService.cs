using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Launchers.Xbox;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Profiles;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class CollectionSyncService(
    IItadApiClient api,
    ProfileManager profiles,
    SyncPayloadBuilder payloadBuilder,
    XboxStoreIdNormalizer xboxStoreIdNormalizer,
    MicrosoftStoreSyncPayloadPreparer microsoftStoreSyncPayloadPreparer,
    EaStoreSyncPayloadPreparer eaStoreSyncPayloadPreparer,
    CollectionSyncFaultIsolator faultIsolator,
    FileLogger logger,
    IUnmatchedTitlesService? unmatchedTitles = null) : ICollectionSyncService
{
    public async Task<ItadSyncResponse?> SyncAsync(
        LauncherReadResult read,
        CancellationToken ct = default)
    {
        if (WaitlistFilter.ShouldSkipCollectionSync(read.Owned))
            return null;

        var owned = read.Launcher == LauncherId.Xbox
            ? await xboxStoreIdNormalizer.NormalizeAsync(read.Owned, ct)
            : read.Owned;

        if (WaitlistFilter.ShouldSkipCollectionSync(owned))
        {
            if (read.Owned.Count > 0)
            {
                var sampleIds = read.Owned
                    .Take(5)
                    .Select(game => game.StoreId)
                    .ToList();
                logger.LogInfo(
                    $"{FormatLauncher(read.Launcher)}: no games with resolvable Microsoft Store IDs; skipping collection sync." +
                    (sampleIds.Count > 0 ? $" Sample IDs: {string.Join(", ", sampleIds)}" : string.Empty));
            }

            return null;
        }

        if (read.Launcher == LauncherId.Xbox && owned.Count != read.Owned.Count)
        {
            logger.LogInfo(
                $"Microsoft: normalized {read.Owned.Count} library entries to {owned.Count} Microsoft Store product ID(s).");
        }

        var obsoleteIds = new List<string>();
        var payloads = new List<SyncGamePayload>();
        foreach (var game in owned)
        {
            var obsolete = AutoMatchResolver.GetObsoleteIdIfReplaced(game.StoreId, game.Title);
            if (!string.IsNullOrEmpty(obsolete))
                obsoleteIds.Add(obsolete);

            var payload = await payloadBuilder.ToPayloadAsync(game, ct);
            if (SyncPayloadBuilder.IsValid(payload))
                payloads.Add(payload);
        }

        if (payloads.Count == 0)
            return null;

        if (read.Launcher == LauncherId.Xbox)
        {
            payloads = (await microsoftStoreSyncPayloadPreparer.PrepareAsync(payloads, ct)).ToList();
            logger.LogInfo(
                $"Microsoft: syncing IDs: {string.Join(", ", payloads.Select(payload => payload.Id))}");
        }
        else if (read.Launcher == LauncherId.Ea)
        {
            payloads = (await eaStoreSyncPayloadPreparer.PrepareAsync(payloads, ct)).ToList();
            logger.LogInfo(
                $"EA: syncing IDs: {string.Join(", ", payloads.Select(payload => payload.Id))}");
        }
        else
        {
            // Epic, Ubisoft, Battle.net — track unmatched games via ITAD shop lookup
            await TrackUnmatchedForGenericLauncherAsync(read.Launcher, owned, payloads, ct);
        }

        return await profiles.ExecuteProfileSyncAsync(
            read.Launcher,
            async (accessToken, profileToken) =>
            {
                var syncResponse = await SyncWithRecoveryAsync(
                    read.Launcher,
                    accessToken,
                    profileToken,
                    payloads,
                    ct);

                if (obsoleteIds.Count > 0)
                {
                    try
                    {
                        logger.LogInfo($"Auto-Cleanup: Purging {obsoleteIds.Count} obsolete mismatched IDs ({string.Join(", ", obsoleteIds)}) from ITAD...");
                        await api.DeleteWaitlistGamesAsync(accessToken, obsoleteIds, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInfo($"Auto-Cleanup Note: {ex.Message}");
                    }
                }

                return syncResponse;
            },
            ct);
    }

    private async Task<ItadSyncResponse> SyncWithRecoveryAsync(
        LauncherId launcher,
        string accessToken,
        string profileToken,
        IReadOnlyList<SyncGamePayload> payloads,
        CancellationToken ct)
    {
        var launcherName = FormatLauncher(launcher);

        try
        {
            return await api.SyncCollectionAsync(accessToken, profileToken, payloads, ct);
        }
        catch (HttpRequestException ex) when (IsServerError(ex) && HasOptionalFields(payloads))
        {
            logger.LogInfo(
                $"{launcherName}: collection sync failed with server error; retrying without playtime and last played.");

            var minimalPayloads = payloads
                .Select(payload => payload with { Playtime = null, LastPlayed = null })
                .ToList();

            try
            {
                return await api.SyncCollectionAsync(accessToken, profileToken, minimalPayloads, ct);
            }
            catch (HttpRequestException retryEx) when (IsServerError(retryEx))
            {
                return await faultIsolator.SyncCollectionAsync(
                    accessToken,
                    profileToken,
                    minimalPayloads,
                    launcherName,
                    ct);
            }
        }
        catch (HttpRequestException ex) when (IsServerError(ex))
        {
            return await faultIsolator.SyncCollectionAsync(
                accessToken,
                profileToken,
                payloads,
                launcherName,
                ct);
        }
    }

    private static bool HasOptionalFields(IReadOnlyList<SyncGamePayload> payloads) =>
        payloads.Any(payload => payload.Playtime is not null || payload.LastPlayed is not null);

    private static bool IsServerError(HttpRequestException exception) =>
        exception.Message.Contains("500", StringComparison.Ordinal);

    private async Task TrackUnmatchedForGenericLauncherAsync(
        LauncherId launcher,
        IReadOnlyList<StoreGame> owned,
        IReadOnlyList<SyncGamePayload> payloads,
        CancellationToken ct)
    {
        if (unmatchedTitles is null || payloads.Count == 0)
            return;

        try
        {
            var shopId = payloads[0].Shop;
            var lookupIds = payloads.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var lookup = await api.LookupShopGameIdsAsync(shopId, lookupIds, ct);

            var unmatchedList = new List<UnmatchedTitle>();
            var ownedLookup = owned.ToDictionary(g => g.StoreId, g => g, StringComparer.OrdinalIgnoreCase);

            foreach (var payload in payloads)
            {
                var isKnown = lookup.TryGetValue(payload.Id, out var gameId) && !string.IsNullOrWhiteSpace(gameId);
                if (!isKnown)
                {
                    var originalTitle = ownedLookup.TryGetValue(payload.Id, out var original)
                        ? original.Title
                        : payload.Title;

                    unmatchedList.Add(new UnmatchedTitle(
                        launcher,
                        payload.Id,
                        originalTitle,
                        nameof(UnmatchedReason.NoApiMatch),
                        DateTime.Now,
                        UnmatchedReason.NoApiMatch));
                }
            }

            if (unmatchedList.Count > 0)
            {
                logger.LogInfo(
                    $"{FormatLauncher(launcher)}: {unmatchedList.Count} game(s) not found in ITAD catalog.");
                await unmatchedTitles.AddRangeAsync(unmatchedList, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogInfo($"{FormatLauncher(launcher)}: unmatched tracking skipped — {ex.Message}");
        }
    }

    private static string FormatLauncher(LauncherId launcher) => launcher switch
    {
        LauncherId.Xbox => "Microsoft",
        LauncherId.Ea => "EA",
        _ => launcher.ToString()
    };
}

