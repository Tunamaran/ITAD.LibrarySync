using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ITAD.LibrarySync.Core.Api;

namespace ITAD.LibrarySync.Core.Auth;

[SupportedOSPlatform("windows")]
public sealed class ItadOAuthService(HttpClient httpClient, ItadOptions options, TokenStorage storage)
{
    private const string AuthorizeUrl = "https://isthereanydeal.com/oauth/authorize/";
    private const string TokenUrl = "https://isthereanydeal.com/oauth/token/";
    private const string Scopes = "profiles wait_read wait_write coll_read coll_write";
    private const int ExpiryBufferSeconds = 60;

    private string? _pendingCodeVerifier;

    public string BuildAuthorizeUrl(string? state = null) =>
        BuildAuthorizeUrl(state, GenerateCodeVerifier());

    public string BuildAuthorizeUrl(string? state, string codeVerifier)
    {
        _pendingCodeVerifier = codeVerifier;

        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["code_challenge"] = GenerateCodeChallenge(codeVerifier),
            ["code_challenge_method"] = "S256"
        };

        if (!string.IsNullOrEmpty(state))
            query["state"] = state;

        return BuildUrl(AuthorizeUrl, query);
    }

    public async Task<OAuthTokens> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_pendingCodeVerifier))
            throw new InvalidOperationException("Authorize URL must be built before exchanging an authorization code.");

        var tokens = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["code_verifier"] = _pendingCodeVerifier
        }, refreshTokenFallback: null, ct);

        _pendingCodeVerifier = null;
        storage.Save(tokens);
        return tokens;
    }

    public async Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokens = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId
        }, refreshTokenFallback: refreshToken, ct);

        storage.Save(tokens);
        return tokens;
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        var tokens = storage.Load()
            ?? throw new InvalidOperationException("Not authenticated with ITAD.");

        if (tokens.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(ExpiryBufferSeconds))
            tokens = await RefreshAsync(tokens.RefreshToken, ct);

        return tokens.AccessToken;
    }

    public Task ClearAsync()
    {
        storage.Clear();
        _pendingCodeVerifier = null;
        return Task.CompletedTask;
    }

    private async Task<OAuthTokens> RequestTokensAsync(
        Dictionary<string, string> form,
        string? refreshTokenFallback,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await httpClient.PostAsync(TokenUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var message = string.IsNullOrWhiteSpace(body)
                ? $"ITAD OAuth token request failed with {(int)response.StatusCode} {response.ReasonPhrase}."
                : $"ITAD OAuth token request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}";
            throw new HttpRequestException(message);
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("ITAD OAuth token endpoint returned an empty response.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new InvalidOperationException("ITAD OAuth token endpoint did not return an access token.");

        var refreshToken = string.IsNullOrWhiteSpace(payload.RefreshToken)
            ? refreshTokenFallback
            : payload.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("ITAD OAuth token endpoint did not return a refresh token.");

        return new OAuthTokens(
            payload.AccessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string BuildUrl(string baseUrl, IReadOnlyDictionary<string, string> query)
    {
        var sb = new StringBuilder(baseUrl);
        var first = true;

        foreach (var (key, value) in query)
        {
            sb.Append(first ? '?' : '&');
            first = false;
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        return sb.ToString();
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
