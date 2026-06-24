using System.Runtime.Versioning;
using ITAD.LibrarySync.Core.Auth.Ea;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

[SupportedOSPlatform("windows")]
public sealed class EaOnlineLibraryReader(EaOAuthService oauthService, EaJunoClient junoClient)
{
    public const string ConnectEaMessage = "Connect your EA account in Settings.";

    public bool CanReadOnline() => oauthService.HasStoredLogin();

    public async Task<LauncherReadResult?> TryReadAsync(CancellationToken ct = default)
    {
        if (!CanReadOnline())
            return null;

        ct.ThrowIfCancellationRequested();

        try
        {
            await oauthService.GetValidAccessTokenAsync(ct);
            var entitlements = await junoClient.GetOwnedEntitlementsAsync(ct);
            var owned = EaJunoOwnedGamesMapper.Map(entitlements);

            return new LauncherReadResult(
                LauncherId.Ea,
                IsDetected: true,
                IsLoggedIn: true,
                Owned: owned,
                Wishlist: [],
                WishlistReadable: false);
        }
        catch (Exception ex) when (IsAuthFailure(ex))
        {
            return new LauncherReadResult(
                LauncherId.Ea,
                IsDetected: true,
                IsLoggedIn: false,
                Owned: [],
                Wishlist: [],
                WishlistReadable: false,
                ConnectEaMessage);
        }
    }

    private static bool IsAuthFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("refresh token", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
