using System.Text.Json;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Minimal PCGamingWiki MediaWiki API client. Fetches only the "Save game data
/// location" section (not the whole page) and parses the Windows save path.
/// Requests are paced to stay polite to the wiki (default 1.1s between calls).
/// </summary>
public sealed class PcgwApiClient : IPcgwApiClient
{
    private const string ApiUrl = "https://pcgamingwiki.com/w/api.php";
    private const string SaveSectionTitle = "Save game data location";

    private readonly HttpClient _httpClient;
    private readonly FileLogger? _logger;
    private readonly TimeSpan _requestPacing;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    public PcgwApiClient(HttpClient httpClient, FileLogger? logger = null, TimeSpan? requestPacing = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _requestPacing = requestPacing ?? TimeSpan.FromSeconds(1.1);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ITAD-LibrarySync/1.0 (+https://github.com/Tunamaran/ITAD.LibrarySync; cloud-save lookup)");
    }

    public async Task<PcgwSaveInfo?> LookupSavePathAsync(string gameTitle, CancellationToken ct = default)
    {
        try
        {
            var pageTitle = await FindPageAsync(gameTitle, ct);
            if (pageTitle is null)
            {
                _logger?.LogInfo($"PcgwApiClient: no page found for '{gameTitle}'.");
                return null;
            }

            var sectionIndex = await FindSaveSectionAsync(pageTitle, ct);
            if (sectionIndex is null)
            {
                _logger?.LogInfo($"PcgwApiClient: no save-data section on '{pageTitle}'.");
                return null;
            }

            var wikitext = await GetSectionWikitextAsync(pageTitle, sectionIndex.Value, ct);
            if (wikitext is null)
                return null;

            var savePath = PcgwSavePathParser.ParseWindowsSavePath(wikitext);
            if (savePath is null)
            {
                _logger?.LogInfo($"PcgwApiClient: no resolvable Windows save path on '{pageTitle}'.");
                return null;
            }

            return new PcgwSaveInfo(
                pageTitle,
                savePath,
                $"https://www.pcgamingwiki.com/wiki/{Uri.EscapeDataString(pageTitle.Replace(' ', '_'))}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"PcgwApiClient: lookup failed for '{gameTitle}' — {ex.Message}");
            return null;
        }
    }

    private async Task<string?> FindPageAsync(string gameTitle, CancellationToken ct)
    {
        var query = BuildQuery(new Dictionary<string, string>
        {
            ["action"] = "query",
            ["list"] = "search",
            ["srsearch"] = gameTitle,
            ["srlimit"] = "3",
            ["formatversion"] = "2"
        });

        using var document = await GetJsonAsync(query, ct);
        if (document is null)
            return null;

        if (!document.RootElement.TryGetProperty("query", out var queryNode) ||
            !queryNode.TryGetProperty("search", out var search) ||
            search.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in search.EnumerateArray())
        {
            if (item.TryGetProperty("ns", out var ns) && ns.GetInt32() != 0)
                continue;

            if (item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
        }

        return null;
    }

    private async Task<int?> FindSaveSectionAsync(string pageTitle, CancellationToken ct)
    {
        var query = BuildQuery(new Dictionary<string, string>
        {
            ["action"] = "parse",
            ["page"] = pageTitle,
            ["prop"] = "sections",
            ["formatversion"] = "2"
        });

        using var document = await GetJsonAsync(query, ct);
        if (document is null)
            return null;

        if (!document.RootElement.TryGetProperty("parse", out var parseNode) ||
            !parseNode.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var section in sections.EnumerateArray())
        {
            if (!section.TryGetProperty("line", out var line) ||
                !string.Equals(line.GetString(), SaveSectionTitle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (section.TryGetProperty("index", out var index) &&
                int.TryParse(index.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private async Task<string?> GetSectionWikitextAsync(string pageTitle, int sectionIndex, CancellationToken ct)
    {
        var query = BuildQuery(new Dictionary<string, string>
        {
            ["action"] = "parse",
            ["page"] = pageTitle,
            ["section"] = sectionIndex.ToString(),
            ["prop"] = "wikitext",
            ["formatversion"] = "2"
        });

        using var document = await GetJsonAsync(query, ct);
        if (document is null)
            return null;

        if (!document.RootElement.TryGetProperty("parse", out var parseNode) ||
            !parseNode.TryGetProperty("wikitext", out var wikitext) ||
            wikitext.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return wikitext.GetString();
    }

    private string BuildQuery(IReadOnlyDictionary<string, string> parameters)
    {
        var joined = string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"{ApiUrl}?{joined}&format=json";
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken ct)
    {
        await PaceAsync(ct);

        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning($"PcgwApiClient: request failed with {(int)response.StatusCode}: {url}");
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private async Task PaceAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var elapsed = DateTimeOffset.UtcNow - _lastRequestAt;
            if (elapsed < _requestPacing)
                await Task.Delay(_requestPacing - elapsed, ct);

            _lastRequestAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
