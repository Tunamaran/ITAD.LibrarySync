namespace ITAD.LibrarySync.Core.Auth.Ea;

public sealed record EaOAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
