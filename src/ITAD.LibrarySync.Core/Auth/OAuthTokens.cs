namespace ITAD.LibrarySync.Core.Auth;

public sealed record OAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
