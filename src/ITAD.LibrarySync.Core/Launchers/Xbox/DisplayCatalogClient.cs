using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public sealed class DisplayCatalogClient(HttpClient httpClient) : IMicrosoftStoreCatalogClient
{
    private const string LookupEndpoint =
        "https://displaycatalog.mp.microsoft.com/v7.0/products/lookup";

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyDictionary<string, string>> ResolveStoreIdsByPfnAsync(
        IReadOnlyList<string> pfns,
        CancellationToken ct)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pfn in pfns.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var storeId = await LookupStoreIdByPfnAsync(pfn, ct);
            if (!string.IsNullOrWhiteSpace(storeId))
                results[pfn] = storeId!;
        }

        return results;
    }

    private async Task<string?> LookupStoreIdByPfnAsync(string pfn, CancellationToken ct)
    {
        var url =
            $"{LookupEndpoint}?alternateId=PackageFamilyName&value={Uri.EscapeDataString(pfn)}&market=US&languages=en-US";

        using var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<DisplayCatalogLookupResponse>(JsonOptions, ct);
        return payload?.Products?.FirstOrDefault()?.ProductId;
    }

    private sealed class DisplayCatalogLookupResponse
    {
        [JsonPropertyName("Products")]
        public IReadOnlyList<DisplayCatalogLookupProduct> Products { get; init; } = [];
    }

    private sealed class DisplayCatalogLookupProduct
    {
        [JsonPropertyName("ProductId")]
        public string? ProductId { get; init; }
    }
}
