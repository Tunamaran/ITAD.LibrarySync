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

        var payloads = filtered.Select(payloadBuilder.ToPayload).ToList();
        return await profiles.ExecuteProfileSyncAsync(
            read.Launcher,
            (accessToken, profileToken) => api.SyncWaitlistAsync(accessToken, profileToken, payloads, ct),
            ct);
    }
}
