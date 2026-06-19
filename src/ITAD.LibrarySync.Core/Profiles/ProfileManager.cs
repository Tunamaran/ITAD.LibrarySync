using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Profiles;

public sealed class ProfileManager(IItadApiClient api, ItadOAuthService oauth, ProfileTokenStorage storage)
{
    public async Task<string> GetOrLinkProfileTokenAsync(LauncherId launcher, CancellationToken ct = default)
    {
        if (storage.TryGet(launcher, out var existing))
            return existing;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var (accountId, accountName) = ProfileConfig.Get(launcher);
        var profileToken = await api.LinkProfileAsync(accessToken, accountId, accountName, ct);
        storage.Save(launcher, profileToken);
        return profileToken;
    }
}
