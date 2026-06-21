using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ITAD.LibrarySync.Core.Auth.Xbox;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public sealed class TitleHubClient : IXboxLibraryClient
{
    private const string TitleHistoryBaseUrl = "https://titlehub.xboxlive.com";
    private const string UserStatsBatchUrl = "https://userstats.xboxlive.com/batch";
    private const int MaxTitleIdsPerBatch = 100;
    private const string MinutesPlayedStat = "MinutesPlayed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Func<XboxAuthorizationData, string> _buildAuthHeader;

    public TitleHubClient(HttpClient httpClient)
        : this(httpClient, XboxOAuthService.BuildAuthorizationHeader)
    {
    }

    public TitleHubClient(HttpClient httpClient, Func<XboxAuthorizationData, string> buildAuthHeader)
    {
        _httpClient = httpClient;
        _buildAuthHeader = buildAuthHeader;
    }

    public async Task<IReadOnlyList<TitleHistoryItem>> GetTitleHistoryAsync(
        XboxAuthorizationData auth,
        CancellationToken ct)
    {
        var xuid = auth.DisplayClaims.Xui[0].Xid;
        var url = $"{TitleHistoryBaseUrl}/users/xuid({xuid})/titles/titlehistory/decoration/detail";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyXboxHeaders(request, auth);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TitleHistoryResponse>(JsonOptions, ct);
        return payload?.Titles ?? [];
    }

    public async Task<IReadOnlyDictionary<string, int>> GetMinutesPlayedAsync(
        XboxAuthorizationData auth,
        IReadOnlyList<string> titleIds,
        CancellationToken ct)
    {
        if (titleIds.Count == 0)
            return new Dictionary<string, int>();

        var xuid = auth.DisplayClaims.Xui[0].Xid;
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var chunk in titleIds.Chunk(MaxTitleIdsPerBatch))
        {
            var batchResult = await RequestMinutesPlayedBatchAsync(auth, xuid, chunk, ct);
            foreach (var (titleId, minutes) in batchResult)
                result[titleId] = minutes;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, int>> RequestMinutesPlayedBatchAsync(
        XboxAuthorizationData auth,
        string xuid,
        IReadOnlyList<string> titleIds,
        CancellationToken ct)
    {
        var body = new UserStatsBatchRequest
        {
            ArrangeByField = "xuid",
            Xuids = [xuid],
            Stats = titleIds
                .Select(titleId => new UserStatsRequestStat
                {
                    Name = MinutesPlayedStat,
                    TitleId = titleId
                })
                .ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, UserStatsBatchUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        ApplyXboxHeaders(request, auth);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UserStatsBatchResponse>(JsonOptions, ct);
        return ParseMinutesPlayed(payload);
    }

    private static IReadOnlyDictionary<string, int> ParseMinutesPlayed(UserStatsBatchResponse? payload)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (payload is null)
            return result;

        foreach (var collection in payload.StatListsCollection)
        {
            foreach (var stat in collection.Stats)
            {
                if (!string.Equals(stat.Name, MinutesPlayedStat, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(stat.TitleId) || string.IsNullOrWhiteSpace(stat.Value))
                    continue;

                if (!int.TryParse(stat.Value, out var minutes))
                    continue;

                result[stat.TitleId] = minutes;
            }
        }

        return result;
    }

    private void ApplyXboxHeaders(HttpRequestMessage request, XboxAuthorizationData auth)
    {
        request.Headers.TryAddWithoutValidation("x-xbl-contract-version", "2");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US");
        request.Headers.TryAddWithoutValidation("Authorization", _buildAuthHeader(auth));
    }
}
