using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAD.LibrarySync.Core.Auth.Xbox;

[SupportedOSPlatform("windows")]
public sealed class XboxOAuthService
{
    private const string AuthorizeEndpoint = "https://login.live.com/oauth20_authorize.srf";
    private const string TokenEndpoint = "https://login.live.com/oauth20_token.srf";
    private const string UserAuthEndpoint = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsEndpoint = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Xbox Live auth endpoints expect PascalCase JSON property names.
    private static readonly JsonSerializerOptions XboxRequestJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly HttpClient _httpClient;
    private readonly XboxOAuthOptions _options;
    private readonly XboxTokenStorage _storage;
    private bool _loginRefreshed;

    public XboxOAuthService(HttpClient httpClient, XboxOAuthOptions options, XboxTokenStorage storage)
    {
        _httpClient = httpClient;
        _options = options;
        _storage = storage;
    }

    public string BuildAuthorizeUrl()
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["approval_prompt"] = "auto",
            ["scope"] = _options.Scopes,
            ["redirect_uri"] = _options.RedirectUri
        };

        var qs = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{AuthorizeEndpoint}?{qs}";
    }

    public async Task ExchangeCodeAsync(string code, CancellationToken ct)
    {
        var tokenResponse = await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["scope"] = _options.Scopes
            },
            ct);

        SaveLoginFromResponse(tokenResponse);
        _loginRefreshed = true;
        await EnsureXstsAsync(ct);
        await EnsureLicensingXstsAsync(ct);
        _loginRefreshed = false;
    }

    public async Task RefreshLoginAsync(CancellationToken ct)
    {
        var login = _storage.LoadLogin();
        if (login is null)
            return;

        if (login.ExpiresAt > DateTimeOffset.UtcNow.Add(ExpiryBuffer))
        {
            await EnsureXstsAsync(ct);
            await EnsureLicensingXstsAsync(ct);
            return;
        }

        var tokenResponse = await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = login.RefreshToken,
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["scope"] = _options.Scopes
            },
            ct);

        SaveLoginFromResponse(tokenResponse);
        _loginRefreshed = true;
        await EnsureXstsAsync(ct);
        await EnsureLicensingXstsAsync(ct);
        _loginRefreshed = false;
    }

    public async Task EnsureXstsAsync(CancellationToken ct)
    {
        var login = _storage.LoadLogin();
        if (login is null)
            return;

        if (!_loginRefreshed && _storage.LoadXsts() is not null)
            return;

        var userToken = await AuthenticateUserAsync(login.AccessToken, ct);
        var xsts = await AuthorizeXstsAsync(userToken, "http://xboxlive.com", ct);
        _storage.SaveXsts(xsts);
    }

    public async Task EnsureLicensingXstsAsync(CancellationToken ct)
    {
        var login = _storage.LoadLogin();
        if (login is null)
            return;

        if (!_loginRefreshed && _storage.LoadLicensingXsts() is not null)
            return;

        var userToken = await AuthenticateUserAsync(login.AccessToken, ct);
        var xsts = await AuthorizeXstsAsync(userToken, "http://licensing.xboxlive.com", ct);
        _storage.SaveLicensingXsts(xsts);
    }

    public async Task<XboxAuthorizationData> GetLicensingAuthorizationAsync(CancellationToken ct)
    {
        if (_storage.LoadLogin() is null)
            throw new XboxAuthRequiredException();

        await RefreshLoginAsync(ct);
        await EnsureLicensingXstsAsync(ct);

        var xsts = _storage.LoadLicensingXsts();
        if (xsts is null)
            throw new XboxAuthRequiredException();

        return xsts;
    }

    public async Task<XboxAuthorizationData> GetAuthorizationAsync(CancellationToken ct)
    {
        if (_storage.LoadLogin() is null)
            throw new XboxAuthRequiredException();

        await RefreshLoginAsync(ct);
        await EnsureXstsAsync(ct);

        var xsts = _storage.LoadXsts();
        if (xsts is null)
            throw new XboxAuthRequiredException();

        return xsts;
    }

    public static string BuildAuthorizationHeader(XboxAuthorizationData auth)
    {
        var xui = auth.DisplayClaims.Xui[0];
        return $"XBL3.0 x={xui.Uhs};{auth.Token}";
    }

    public string? GetGamertag()
    {
        return _storage.LoadXsts()?.DisplayClaims.Xui.FirstOrDefault()?.Gtg;
    }

    public bool IsAuthenticated()
    {
        return _storage.LoadXsts() is not null;
    }

    public Task ClearAsync()
    {
        _storage.ClearAll();
        _loginRefreshed = false;
        return Task.CompletedTask;
    }

    private void SaveLoginFromResponse(MsaTokenResponse tokenResponse)
    {
        var tokens = new XboxOAuthTokens(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            null);

        _storage.SaveLogin(tokens);
    }

    private async Task<MsaTokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await _httpClient.PostAsync(TokenEndpoint, content, ct);
        await EnsureSuccessAsync(response, "Microsoft account token exchange", ct);

        var tokenResponse = await response.Content.ReadFromJsonAsync<MsaTokenResponse>(JsonOptions, ct);
        if (tokenResponse is null ||
            string.IsNullOrEmpty(tokenResponse.AccessToken) ||
            string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            throw new InvalidOperationException("MSA token response was invalid.");
        }

        return tokenResponse;
    }

    private async Task<string> AuthenticateUserAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, UserAuthEndpoint);
        request.Headers.Add("x-xbl-contract-version", "1");
        request.Headers.Add("Accept", "application/json");
        request.Content = CreateXboxJsonContent(new XboxUserAuthRequest
        {
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
            Properties = new XboxUserAuthProperties
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={accessToken}"
            }
        });

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Xbox user authentication", ct);

        var authResponse = await response.Content.ReadFromJsonAsync<XboxUserAuthResponse>(JsonOptions, ct);
        if (authResponse is null || string.IsNullOrEmpty(authResponse.Token))
            throw new InvalidOperationException("Xbox user authentication response was invalid.");

        return authResponse.Token;
    }

    private async Task<XboxAuthorizationData> AuthorizeXstsAsync(
        string userToken,
        string relyingParty,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, XstsEndpoint);
        request.Headers.Add("x-xbl-contract-version", "1");
        request.Headers.Add("Accept", "application/json");
        request.Content = CreateXboxJsonContent(new XboxXstsRequest
        {
            RelyingParty = relyingParty,
            TokenType = "JWT",
            Properties = new XboxXstsProperties
            {
                SandboxId = "RETAIL",
                UserTokens = [userToken]
            }
        });

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Xbox XSTS authorization", ct);

        var xstsResponse = await response.Content.ReadFromJsonAsync<XboxXstsResponse>(JsonOptions, ct);
        if (xstsResponse is null ||
            string.IsNullOrEmpty(xstsResponse.Token) ||
            xstsResponse.DisplayClaims?.Xui is not { Count: > 0 })
        {
            throw new InvalidOperationException("XSTS authorization response was invalid.");
        }

        return new XboxAuthorizationData
        {
            Token = xstsResponse.Token,
            DisplayClaims = new XboxDisplayClaims
            {
                Xui = xstsResponse.DisplayClaims.Xui
                    .Select(claim => new XboxXuiClaim
                    {
                        Xid = claim.Xid,
                        Uhs = claim.Uhs,
                        Gtg = claim.Gtg
                    })
                    .ToList()
            }
        };
    }

    private static StringContent CreateXboxJsonContent<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, XboxRequestJsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string stepName,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var detail = string.IsNullOrWhiteSpace(body)
            ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
            : body.Trim();

        throw new HttpRequestException($"{stepName} failed: {detail}");
    }

    private sealed class MsaTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed class XboxUserAuthRequest
    {
        public string RelyingParty { get; init; } = "";
        public string TokenType { get; init; } = "";
        public XboxUserAuthProperties Properties { get; init; } = new();
    }

    private sealed class XboxUserAuthProperties
    {
        public string AuthMethod { get; init; } = "";
        public string SiteName { get; init; } = "";
        public string RpsTicket { get; init; } = "";
    }

    private sealed class XboxUserAuthResponse
    {
        public string Token { get; init; } = "";
    }

    private sealed class XboxXstsRequest
    {
        public string RelyingParty { get; init; } = "";
        public string TokenType { get; init; } = "";
        public XboxXstsProperties Properties { get; init; } = new();
    }

    private sealed class XboxXstsProperties
    {
        public string SandboxId { get; init; } = "";
        public IReadOnlyList<string> UserTokens { get; init; } = [];
    }

    private sealed class XboxXstsResponse
    {
        public string Token { get; init; } = "";
        public XboxXstsDisplayClaims? DisplayClaims { get; init; }
    }

    private sealed class XboxXstsDisplayClaims
    {
        [JsonPropertyName("xui")]
        public IReadOnlyList<XboxXstsXuiClaim> Xui { get; init; } = [];
    }

    private sealed class XboxXstsXuiClaim
    {
        [JsonPropertyName("xid")]
        public string Xid { get; init; } = "";

        [JsonPropertyName("uhs")]
        public string Uhs { get; init; } = "";

        [JsonPropertyName("gtg")]
        public string? Gtg { get; init; }
    }
}
