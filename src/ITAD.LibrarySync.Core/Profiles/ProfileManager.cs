using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Profiles;

public sealed class ProfileManager(IItadApiClient api, ItadOAuthService oauth, ProfileTokenStorage storage)
{
    public async Task<string> GetOrLinkProfileTokenAsync(LauncherId launcher, CancellationToken ct = default)
    {
        if (storage.TryGet(launcher, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return existing;

        return await LinkProfileTokenAsync(launcher, ct);
    }

    public async Task<string> LinkProfileTokenAsync(LauncherId launcher, CancellationToken ct = default)
    {
        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var (accountId, accountName) = ProfileConfig.Get(launcher);
        var profileToken = await api.LinkProfileAsync(accessToken, accountId, accountName, ct);
        storage.Save(launcher, profileToken);
        return profileToken;
    }

    public async Task<T> ExecuteProfileSyncAsync<T>(
        LauncherId launcher,
        Func<string, string, Task<T>> sync,
        CancellationToken ct = default)
    {
        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var profileToken = await GetOrLinkProfileTokenAsync(launcher, ct);

        try
        {
            return await sync(accessToken, profileToken);
        }
        catch (HttpRequestException ex) when (ItadApiClient.IsProfileNotFound(ex))
        {
            storage.Remove(launcher);
            profileToken = await LinkProfileTokenAsync(launcher, ct);
            return await sync(accessToken, profileToken);
        }
    }
}
