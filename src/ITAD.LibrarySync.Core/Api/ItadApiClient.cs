using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public sealed class ItadApiClient(HttpClient httpClient) : IItadApiClient
{
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
        if (gameIds.Count == 0)
            return;

        using var request = CreateAuthorizedRequest(HttpMethod.Delete, "/waitlist/games/v1", accessToken);
        request.Content = JsonContent.Create(gameIds, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "delete waitlist games", ct);
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
        if (shopGameIds.Count == 0)
            return [];

        using var response = await httpClient.PostAsJsonAsync(
            $"/lookup/id/shop/{shopId}/v1",
            shopGameIds,
            JsonOptions,
            ct);
        await EnsureSuccessAsync(response, $"lookup ITAD game IDs for shop {shopId}", ct);

        var lookup = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(JsonOptions, ct)
            ?? [];

        return lookup.Values
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
    }

    private async Task<ItadSyncResponse> SyncGamesAsync(
        string accessToken,
        string profileToken,
        string path,
        IReadOnlyList<SyncGamePayload> games,
        CancellationToken ct)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Put, path, accessToken);
        request.Headers.Add("ITAD-Profile", profileToken);
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

        throw new HttpRequestException(message);
    }

    private sealed record LinkProfileRequest(string AccountId, string AccountName);

    private sealed record LinkProfileResponse(string Token);

    private sealed record WaitlistGameResponse(string Id);

    private sealed record ShopMapEntryResponse(int Id, string Title);

    private sealed record SyncGameJsonPayload(
        int Shop,
        string Id,
        string Title,
        int? Playtime,
        string? LastPlayed);
}
