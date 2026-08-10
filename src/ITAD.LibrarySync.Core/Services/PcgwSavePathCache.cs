using System.Text.Json;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// File cache for PCGamingWiki lookups. Positive results live 30 days,
/// negative ("not found") results 7 days, so repeat scans never hammer the wiki.
/// </summary>
public sealed class PcgwSavePathCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _cacheDir;
    private readonly TimeSpan _foundTtl;
    private readonly TimeSpan _notFoundTtl;

    public PcgwSavePathCache(
        string? cacheDir = null,
        TimeSpan? foundTtl = null,
        TimeSpan? notFoundTtl = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync",
            "pcgw-cache");
        _foundTtl = foundTtl ?? TimeSpan.FromDays(30);
        _notFoundTtl = notFoundTtl ?? TimeSpan.FromDays(7);
    }

    /// <summary>
    /// Returns <c>(IsHit: false, null)</c> when nothing (valid) is cached;
    /// <c>(IsHit: true, null)</c> for a cached "not found"; otherwise the cached result.
    /// </summary>
    public async Task<(bool IsHit, PcgwSaveInfo? Info)> TryGetAsync(string gameTitle, CancellationToken ct = default)
    {
        var file = GetFilePath(gameTitle);
        if (!File.Exists(file))
            return (false, null);

        try
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var entry = JsonSerializer.Deserialize<CacheEntry>(json, JsonOptions);
            if (entry is null)
            {
                TryDelete(file);
                return (false, null);
            }

            var ttl = string.IsNullOrWhiteSpace(entry.SavePath) ? _notFoundTtl : _foundTtl;
            if (DateTimeOffset.UtcNow - entry.CachedAt > ttl)
            {
                TryDelete(file);
                return (false, null);
            }

            return string.IsNullOrWhiteSpace(entry.SavePath)
                ? (true, null)
                : (true, new PcgwSaveInfo(entry.PageTitle ?? gameTitle, entry.SavePath, entry.SourceUrl));
        }
        catch
        {
            TryDelete(file);
            return (false, null);
        }
    }

    public async Task PutAsync(string gameTitle, PcgwSaveInfo? info, CancellationToken ct = default)
    {
        var entry = new CacheEntry(info?.PageTitle, info?.SavePath, info?.SourceUrl, DateTimeOffset.UtcNow);
        var file = GetFilePath(gameTitle);

        try
        {
            Directory.CreateDirectory(_cacheDir);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            var tempPath = file + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, file, overwrite: true);
        }
        catch
        {
            // Cache failures must never break the lookup flow.
        }
    }

    private string GetFilePath(string gameTitle)
    {
        var slug = AutoMatchResolver.GenerateSlug(gameTitle);
        return Path.Combine(_cacheDir, string.IsNullOrWhiteSpace(slug) ? "unknown" : slug) + ".json";
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch
        {
            // ignore
        }
    }

    private sealed record CacheEntry(
        string? PageTitle,
        string? SavePath,
        string? SourceUrl,
        DateTimeOffset CachedAt);
}
