using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text.Json;
using ITAD.LibrarySync.Core.Auth.Ea;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

[SupportedOSPlatform("windows")]
public sealed class EaJunoClient
{
    private readonly HttpClient _httpClient;
    private readonly EaOAuthOptions _options;
    private readonly EaOAuthService _oauthService;

    public EaJunoClient(HttpClient httpClient, EaOAuthOptions options, EaOAuthService oauthService)
    {
        _httpClient = httpClient;
        _options = options;
        _oauthService = oauthService;
    }

    public async Task<EaSessionInfo> GetIdentityAsync(CancellationToken ct = default)
    {
        var response = await GetGraphQlAsync(EaJunoGraphQl.IdentityQuery, ct);
        if (!response.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("me", out var me) ||
            !me.TryGetProperty("player", out var player))
        {
            throw new InvalidOperationException("EA identity response was missing player data.");
        }

        var userId = player.GetProperty("pd").GetString() ?? string.Empty;
        var personaId = player.GetProperty("psd").GetString() ?? string.Empty;
        var displayName = player.GetProperty("displayName").GetString() ?? userId;
        return new EaSessionInfo(userId, personaId, displayName);
    }

    public async Task<IReadOnlyList<JsonElement>> GetOwnedEntitlementsAsync(CancellationToken ct = default)
    {
        var response = await GetGraphQlAsync(EaJunoGraphQl.OwnedGamesQuery, ct);
        if (!response.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("me", out var me) ||
            !me.TryGetProperty("ownedGameProducts", out var owned) ||
            !owned.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("EA owned games response was missing items.");
        }

        return items.EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private async Task<JsonElement> GetGraphQlAsync(string query, CancellationToken ct)
    {
        var accessToken = await _oauthService.GetValidAccessTokenAsync(ct);
        var url = $"{_options.GraphQlEndpoint}?query={Uri.EscapeDataString(query)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("x-client-id", _options.JunoClientIdHeader);
        request.Headers.TryAddWithoutValidation("referer", _options.Referer);
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"EA GraphQL request failed ({(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement.Clone();
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var first = errors[0].GetRawText();
            throw new InvalidOperationException($"EA GraphQL returned errors: {first}");
        }

        return root;
    }
}
