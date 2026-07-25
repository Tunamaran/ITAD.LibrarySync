using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Profiles;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class WaitlistSyncService(
    IItadApiClient api,
    ProfileManager profiles,
    SyncPayloadBuilder payloadBuilder) : IWaitlistSyncService
{
    public async Task<ItadSyncResponse?> SyncAsync(
        LauncherReadResult read,
        CancellationToken ct = default)
    {
        var filtered = WaitlistFilter.RemoveOwnedGames(read.Wishlist, read.Owned);
        if (WaitlistFilter.ShouldSkipWaitlistSync(read.WishlistReadable, filtered.Count))
            return null;

        var payloads = new List<SyncGamePayload>();
        foreach (var game in filtered)
        {
            var payload = await payloadBuilder.ToPayloadAsync(game, ct);
            if (SyncPayloadBuilder.IsValid(payload))
                payloads.Add(payload);
        }
        return await profiles.ExecuteProfileSyncAsync(
            read.Launcher,
            (accessToken, profileToken) => api.SyncWaitlistAsync(accessToken, profileToken, payloads, ct),
            ct);
    }
}
