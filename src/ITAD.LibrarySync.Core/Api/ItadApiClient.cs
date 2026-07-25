using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public sealed class ItadApiClient(HttpClient httpClient) : IItadApiClient
{
    public static bool IsProfileNotFound(HttpRequestException exception) =>
        exception.Message.Contains("404", StringComparison.Ordinal) &&
        exception.Message.Contains("/profiles/", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> LinkProfileAsync(
        string accessToken,
        string accountId,
        string accountName,
        CancellationToken ct = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Put, "/profiles/link/v1", accessToken);
        request.Content = JsonContent.Create(
            new LinkProfileRequest(accountId, accountName),
            options: JsonOptions);

        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "link profile", ct);

        var payload = await response.Content.ReadFromJsonAsync<LinkProfileResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("ITAD API link profile returned an empty response.");

        if (string.IsNullOrWhiteSpace(payload.Token))
            throw new InvalidOperationException("ITAD API link profile did not return a profile token.");

        return payload.Token;
    }

    public Task<ItadSyncResponse> SyncCollectionAsync(
        string accessToken,
        string profileToken,
        IReadOnlyList<SyncGamePayload> games,
        CancellationToken ct = default) =>
        SyncGamesAsync(accessToken, profileToken, "/profiles/sync/collection/v1", games, ct);

    public Task<ItadSyncResponse> SyncWaitlistAsync(
        string accessToken,
        string profileToken,
        IReadOnlyList<SyncGamePayload> games,
        CancellationToken ct = default) =>
        SyncGamesAsync(accessToken, profileToken, "/profiles/sync/waitlist/v1", games, ct);

    public async Task<IReadOnlyList<string>> GetWaitlistGameIdsAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, "/waitlist/games/v1", accessToken);
        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "get waitlist games", ct);

        var games = await response.Content.ReadFromJsonAsync<List<WaitlistGameResponse>>(JsonOptions, ct)
            ?? [];

        return games
            .Select(game => game.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
    }

    public async Task DeleteWaitlistGamesAsync(
        string accessToken,
        IReadOnlyList<string> gameIds,
        CancellationToken ct = default)
    {
        var validIds = gameIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (validIds.Count == 0)
            return;

        try
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Delete, "/waitlist/games/v1", accessToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(validIds),
                System.Text.Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode || (int)response.StatusCode == 400)
                return;

            await EnsureSuccessAsync(response, "delete waitlist games", ct);
        }
        catch
        {
            // Ignore optional cleanup errors
        }
    }

    public async Task<IReadOnlyDictionary<string, int>> GetShopMapAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("/service/shops/map/v1", ct);
        await EnsureSuccessAsync(response, "get shop map", ct);

        var shops = await response.Content.ReadFromJsonAsync<List<ShopMapEntryResponse>>(JsonOptions, ct)
            ?? [];

        return shops.ToDictionary(shop => shop.Title, shop => shop.Id);
    }

    public async Task<IReadOnlyList<string>> LookupGameIdsByShopIdsAsync(
        int shopId,
        IReadOnlyList<string> shopGameIds,
        CancellationToken ct = default)
    {
        var lookup = await LookupShopGameIdsAsync(shopId, shopGameIds, ct);
        return lookup.Values
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, string?>> LookupShopGameIdsAsync(
        int shopId,
        IReadOnlyList<string> shopGameIds,
        CancellationToken ct = default)
    {
        if (shopGameIds.Count == 0)
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var response = await httpClient.PostAsJsonAsync(
            $"/lookup/id/shop/{shopId}/v1",
            shopGameIds,
            JsonOptions,
            ct);
        await EnsureSuccessAsync(response, $"lookup ITAD game IDs for shop {shopId}", ct);

        return await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(JsonOptions, ct)
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ItadUserInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, "/user/info/v2", accessToken);
        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "get user info", ct);

        var payload = await response.Content.ReadFromJsonAsync<UserInfoResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("ITAD API get user info returned an empty response.");

        if (string.IsNullOrWhiteSpace(payload.Username))
            throw new InvalidOperationException("ITAD API get user info did not return a username.");

        return new ItadUserInfo(payload.Username);
    }

    private async Task<ItadSyncResponse> SyncGamesAsync(
        string accessToken,
        string profileToken,
        string path,
        IReadOnlyList<SyncGamePayload> games,
        CancellationToken ct)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Put, path, accessToken);
        if (!request.Headers.TryAddWithoutValidation("ITAD-Profile", profileToken))
            throw new InvalidOperationException("Failed to attach ITAD-Profile header.");

        request.Content = JsonContent.Create(
            games.Select(ToSyncGameJson).ToList(),
            options: JsonOptions);

        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, $"sync {path}", ct);

        var payload = await response.Content.ReadFromJsonAsync<ItadSyncResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException($"ITAD API {path} returned an empty response.");

        return payload;
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static SyncGameJsonPayload ToSyncGameJson(SyncGamePayload game) =>
        new(
            game.Shop,
            game.Id,
            game.Title,
            game.Playtime,
            game.LastPlayed?.ToString("o"));

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"ITAD API {operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}."
            : $"ITAD API {operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}";

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound &&
            operation.Contains("/profiles/", StringComparison.OrdinalIgnoreCase))
        {
            message +=
                " The linked ITAD profile was not found. Disconnect and reconnect your ITAD account in Settings, then sync again. If this persists, ensure your OAuth app is registered at isthereanydeal.com/my/apps/ with the profiles scope enabled.";
        }

        throw new HttpRequestException(message);
    }

    private sealed record LinkProfileRequest(string AccountId, string AccountName);

    private sealed record LinkProfileResponse(string Token);

    private sealed record WaitlistGameResponse(string Id);

    private sealed record ShopMapEntryResponse(int Id, string Title);

    private sealed record UserInfoResponse(string Username);

    private sealed record SyncGameJsonPayload(
        int Shop,
        string Id,
        string Title,
        int? Playtime,
        string? LastPlayed);
}
