using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITAD.LibrarySync.Core.Launchers.Ea;

namespace ITAD.LibrarySync.Core.Auth.Ea;

[SupportedOSPlatform("windows")]
public sealed class EaOAuthService
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly EaOAuthOptions _options;
    private readonly EaTokenStorage _storage;

    public EaOAuthService(HttpClient httpClient, EaOAuthOptions options, EaTokenStorage storage)
    {
        _httpClient = httpClient;
        _options = options;
        _storage = storage;
    }

    public bool HasStoredLogin() => _storage.LoadLogin() is not null;

    public bool IsAuthenticated() => HasStoredLogin();

    public EaSessionInfo? GetStoredSession() => _storage.LoadSession();

    public string RedirectUri => _options.RedirectUri;

    public string BuildAuthorizeUrl(string pcSign)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["display"] = "junoClient/login",
            ["redirect_uri"] = _options.RedirectUri,
            ["locale"] = "en_US",
            ["pc_sign"] = pcSign
        };

        var qs = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{_options.AuthorizeEndpoint}?{qs}";
    }

    public string BuildWebAuthorizeUrl()
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["display"] = "junoWeb/login",
            ["redirect_uri"] = _options.RedirectUri,
            ["locale"] = "en_US"
        };

        var qs = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{_options.AuthorizeEndpoint}?{qs}";
    }

    public static string GenerateCodeVerifier()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var chars = new char[32];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(chars))
            .TrimEnd('=');
    }

    public async Task ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = code,
            ["client_id"] = _options.ClientId
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        if (_options.ClientId.Equals("JUNO_PC_CLIENT", StringComparison.Ordinal))
        {
            form["token_format"] = "JWS";
            form["code_verifier"] = codeVerifier;
        }

        var response = await RequestTokenAsync(form, ct);

        SaveTokens(response);
        await EnsureSessionAsync(ct);
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        var login = _storage.LoadLogin();
        if (login is null)
            throw new InvalidOperationException("EA account is not connected.");

        if (login.ExpiresAt > DateTimeOffset.UtcNow.Add(ExpiryBuffer))
            return login.AccessToken;

        if (string.IsNullOrWhiteSpace(login.RefreshToken))
            throw new InvalidOperationException("EA refresh token is missing. Connect EA again.");

        var refreshForm = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = login.RefreshToken
        };
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            refreshForm["client_secret"] = _options.ClientSecret;

        var response = await RequestTokenAsync(refreshForm, ct);

        SaveTokens(response);
        return response.AccessToken;
    }

    public void Disconnect() => _storage.ClearAll();

    private async Task EnsureSessionAsync(CancellationToken ct)
    {
        var login = _storage.LoadLogin();
        if (login is null)
            return;

        var session = EaJwtHelper.TryGetSessionInfo(login.AccessToken);
        if (session is not null)
        {
            _storage.SaveSession(session);
            return;
        }

        var client = new EaJunoClient(_httpClient, _options, this);
        session = await client.GetIdentityAsync(ct);
        _storage.SaveSession(session);
    }

    private void SaveTokens(TokenResponse response)
    {
        var expiresAt = response.ExpiresIn > 0
            ? DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn - 60)
            : EaJwtHelper.TryGetExpiry(response.AccessToken) ?? DateTimeOffset.UtcNow.AddHours(4);

        _storage.SaveLogin(new EaOAuthTokens(
            response.AccessToken,
            response.RefreshToken ?? _storage.LoadLogin()?.RefreshToken ?? string.Empty,
            expiresAt));
    }

    private async Task<TokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"EA token request failed ({(int)response.StatusCode}): {body}");

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("EA token response was empty.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("EA token response did not include an access token.");

        return token;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
