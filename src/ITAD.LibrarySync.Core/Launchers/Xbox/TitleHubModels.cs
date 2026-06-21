using System.Text.Json.Serialization;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public sealed class TitleHistoryResponse
{
    [JsonPropertyName("xuid")]
    public string? Xuid { get; init; }

    [JsonPropertyName("titles")]
    public IReadOnlyList<TitleHistoryItem> Titles { get; init; } = [];
}

public sealed class TitleHistoryItem
{
    [JsonPropertyName("titleId")]
    public string? TitleId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("pfn")]
    public string? Pfn { get; init; }

    [JsonPropertyName("modernTitleId")]
    public string? ModernTitleId { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("devices")]
    public IReadOnlyList<string>? Devices { get; init; }
}

public sealed class UserStatsBatchRequest
{
    [JsonPropertyName("arrangebyfield")]
    public string ArrangeByField { get; init; } = "xuid";

    [JsonPropertyName("stats")]
    public required IReadOnlyList<UserStatsRequestStat> Stats { get; init; }

    [JsonPropertyName("xuids")]
    public required IReadOnlyList<string> Xuids { get; init; }
}

public sealed class UserStatsRequestStat
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("titleId")]
    public required string TitleId { get; init; }
}

public sealed class UserStatsBatchResponse
{
    [JsonPropertyName("statlistscollection")]
    public IReadOnlyList<UserStatsStatListCollection> StatListsCollection { get; init; } = [];
}

public sealed class UserStatsStatListCollection
{
    [JsonPropertyName("arrangebyfield")]
    public string? ArrangeByField { get; init; }

    [JsonPropertyName("arrangebyfieldid")]
    public string? ArrangeByFieldId { get; init; }

    [JsonPropertyName("stats")]
    public IReadOnlyList<UserStatsStat> Stats { get; init; } = [];
}

public sealed class UserStatsStat
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("titleid")]
    public string? TitleId { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}
