using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Profiles;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class CollectionSyncService(
    IItadApiClient api,
    ItadOAuthService oauth,
    ProfileManager profiles,
    SyncPayloadBuilder payloadBuilder)
{
    public async Task<ItadSyncResponse?> SyncAsync(
        LauncherReadResult read,
        CancellationToken ct = default)
    {
        if (WaitlistFilter.ShouldSkipCollectionSync(read.Owned))
            return null;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var profileToken = await profiles.GetOrLinkProfileTokenAsync(read.Launcher, ct);
        var payloads = read.Owned.Select(payloadBuilder.ToPayload).ToList();
        return await api.SyncCollectionAsync(accessToken, profileToken, payloads, ct);
    }
}
