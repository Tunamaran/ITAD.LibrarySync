namespace ITAD.LibrarySync.Core.Auth.Xbox;

public sealed record XboxOAuthOptions(
    string ClientId,
    string RedirectUri,
    string Scopes)
{
    public static XboxOAuthOptions CreateDefault() => new(
        ClientId: "38cd2fa8-66fd-4760-afb2-405eb65d5b0c",
        RedirectUri: "https://login.live.com/oauth20_desktop.srf",
        Scopes: "Xboxlive.signin Xboxlive.offline_access");
}
