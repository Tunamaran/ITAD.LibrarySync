using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public sealed class XboxCollectionsClient(HttpClient httpClient) : IXboxEntitlementsClient
{
    private const string CollectionsEndpoint =
        "https://collections.mp.microsoft.com/v8.0/collections/b2bLicensePreview";

    private const string DisplayCatalogEndpoint =
        "https://displaycatalog.mp.microsoft.com/v7.0/products";

    private const int CatalogBatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public async Task<IReadOnlyList<StoreGame>> GetOwnedGamesAsync(
        XboxAuthorizationData licensingAuth,
        CancellationToken ct)
    {
        var items = await QueryEntitlementsAsync(licensingAuth, ct);
        if (items.Count == 0)
            return [];

        var productIds = items
            .Select(item => item.ProductId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        var titlesByProductId = await ResolveProductTitlesAsync(productIds, ct);

        var games = new List<StoreGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId))
                continue;

            if (!string.Equals(item.ProductKind, "Game", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!seen.Add(item.ProductId))
                continue;

            var title = titlesByProductId.TryGetValue(item.ProductId, out var resolvedTitle)
                && !string.IsNullOrWhiteSpace(resolvedTitle)
                    ? resolvedTitle
                    : item.ProductId;

            games.Add(new StoreGame(LauncherId.Xbox, item.ProductId, title));
        }

        return games;
    }

    private async Task<IReadOnlyList<CollectionsEntitlementItem>> QueryEntitlementsAsync(
        XboxAuthorizationData licensingAuth,
        CancellationToken ct)
    {
        var items = new List<CollectionsEntitlementItem>();
        string? continuationToken = null;

        do
        {
            var body = new CollectionsQueryRequest
            {
                ContinuationToken = continuationToken
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, CollectionsEndpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body, RequestJsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                XboxOAuthService.BuildAuthorizationHeader(licensingAuth));
            request.Headers.TryAddWithoutValidation("Host", "collections.mp.microsoft.com");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Microsoft Store collections query failed ({(int)response.StatusCode}): {errorBody}".Trim());
            }

            var payload = await response.Content.ReadFromJsonAsync<CollectionsQueryResponse>(JsonOptions, ct);
            if (payload?.Items is { Count: > 0 })
                items.AddRange(payload.Items);

            continuationToken = payload?.ContinuationToken;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));

        return items;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveProductTitlesAsync(
        IReadOnlyList<string> productIds,
        CancellationToken ct)
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (productIds.Count == 0)
            return titles;

        foreach (var chunk in productIds.Chunk(CatalogBatchSize))
        {
            var bigIds = string.Join(",", chunk);
            var url =
                $"{DisplayCatalogEndpoint}?bigIds={Uri.EscapeDataString(bigIds)}&market=US&languages=en-US";

            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                continue;

            var payload = await response.Content.ReadFromJsonAsync<DisplayCatalogResponse>(JsonOptions, ct);
            if (payload?.Products is null)
                continue;

            foreach (var product in payload.Products)
            {
                if (string.IsNullOrWhiteSpace(product.ProductId))
                    continue;

                var title = product.LocalizedProperties
                    .Select(localized => localized.ProductTitle)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

                if (!string.IsNullOrWhiteSpace(title))
                    titles[product.ProductId] = title!.Trim();
            }
        }

        return titles;
    }
}
