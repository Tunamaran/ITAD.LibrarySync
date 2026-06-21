using System.Text.Json.Serialization;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public sealed class CollectionsQueryRequest
{
    [JsonPropertyName("entitlementFilters")]
    public IReadOnlyList<string> EntitlementFilters { get; init; } = ["*:Game"];

    [JsonPropertyName("validityType")]
    public string ValidityType { get; init; } = "Valid";

    [JsonPropertyName("excludeDuplicates")]
    public bool ExcludeDuplicates { get; init; } = true;

    [JsonPropertyName("expandSatisfyingItems")]
    public bool ExpandSatisfyingItems { get; init; } = true;

    [JsonPropertyName("market")]
    public string Market { get; init; } = "neutral";

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; init; }
}

public sealed class CollectionsQueryResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<CollectionsEntitlementItem> Items { get; init; } = [];

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; init; }
}

public sealed class CollectionsEntitlementItem
{
    [JsonPropertyName("productId")]
    public string? ProductId { get; init; }

    [JsonPropertyName("skuId")]
    public string? SkuId { get; init; }

    [JsonPropertyName("productKind")]
    public string? ProductKind { get; init; }

    [JsonPropertyName("acquisitionType")]
    public string? AcquisitionType { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

public sealed class DisplayCatalogResponse
{
    [JsonPropertyName("Products")]
    public IReadOnlyList<DisplayCatalogProduct> Products { get; init; } = [];
}

public sealed class DisplayCatalogProduct
{
    [JsonPropertyName("ProductId")]
    public string? ProductId { get; init; }

    [JsonPropertyName("LocalizedProperties")]
    public IReadOnlyList<DisplayCatalogLocalizedProperties> LocalizedProperties { get; init; } = [];
}

public sealed class DisplayCatalogLocalizedProperties
{
    [JsonPropertyName("ProductTitle")]
    public string? ProductTitle { get; init; }
}
