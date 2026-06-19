using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Profiles;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class WaitlistSyncService(
    IItadApiClient api,
    ItadOAuthService oauth,
    ProfileManager profiles,
    SyncPayloadBuilder payloadBuilder)
{
    public async Task<ItadSyncResponse?> SyncAsync(
        LauncherReadResult read,
        CancellationToken ct = default)
    {
        var filtered = WaitlistFilter.RemoveOwnedGames(read.Wishlist, read.Owned);
        if (WaitlistFilter.ShouldSkipWaitlistSync(read.WishlistReadable, filtered.Count))
            return null;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var profileToken = await profiles.GetOrLinkProfileTokenAsync(read.Launcher, ct);
        var payloads = filtered.Select(payloadBuilder.ToPayload).ToList();
        return await api.SyncWaitlistAsync(accessToken, profileToken, payloads, ct);
    }
}
