namespace ITAD.LibrarySync.Core.Auth.Xbox;

public sealed record XboxOAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string? UserId);
