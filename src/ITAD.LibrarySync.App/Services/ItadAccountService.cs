using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Scheduling;

namespace ITAD.LibrarySync.App.Services;

public sealed class ItadAccountService(
    ItadOAuthService oauth,
    IItadApiClient api,
    AppSettingsStorage appSettingsStorage)
{
    public event EventHandler? AccountInfoChanged;

    public string GetDisplayName()
    {
        var username = appSettingsStorage.Load().ItadUsername;
        return string.IsNullOrWhiteSpace(username) ? LanguageManager.Instance["ItadAccountDefault"] : username;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var accessToken = await oauth.GetValidAccessTokenAsync(ct);
            var userInfo = await api.GetUserInfoAsync(accessToken, ct);
            SaveUsername(userInfo.Username);
        }
        catch
        {
            // Keep cached username if refresh fails.
        }
    }

    public void Clear()
    {
        var settings = appSettingsStorage.Load();
        settings.ItadUsername = null;
        appSettingsStorage.Save(settings);
        AccountInfoChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveUsername(string username)
    {
        var settings = appSettingsStorage.Load();
        settings.ItadUsername = username;
        appSettingsStorage.Save(settings);
        AccountInfoChanged?.Invoke(this, EventArgs.Empty);
    }
}
