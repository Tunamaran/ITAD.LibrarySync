using System.Text.Json;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Resolves known game save folders from the embedded (PCGamingWiki-derived)
/// database shipped with the app. No network access is required.
/// </summary>
public sealed class GameSaveDiscoveryService : IGameSaveDiscoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _dataPath;
    private readonly Func<string, bool> _pathExists;
    private readonly FileLogger? _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private IReadOnlyList<GameSavePathEntry>? _entriesCache;

    public GameSaveDiscoveryService(
        string? dataPath = null,
        Func<string, bool>? pathExists = null,
        FileLogger? logger = null)
    {
        _dataPath = dataPath ?? Path.Combine(AppContext.BaseDirectory, "Data", "game-save-paths.json");
        _pathExists = pathExists ?? Directory.Exists;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GameSaveInfo>> DiscoverAsync(CancellationToken ct = default)
    {
        var entries = await GetEntriesAsync(ct);
        var results = new List<GameSaveInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            foreach (var rawPath in entry.SavePaths ?? [])
            {
                var resolved = ResolvePath(rawPath);
                if (string.IsNullOrWhiteSpace(resolved))
                    continue;

                if (!seen.Add(resolved))
                    continue;

                results.Add(new GameSaveInfo(
                    Title: entry.Title,
                    SourcePath: resolved,
                    SourceUrl: entry.SourceUrl,
                    IsInstalled: false,
                    Exists: _pathExists(resolved),
                    IsManual: false));
            }
        }

        return results
            .OrderByDescending(save => save.Exists)
            .ThenBy(save => save.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<GameSaveInfo?> FindForTitleAsync(string title, CancellationToken ct = default)
    {
        var normalizedTitle = GameMatcher.NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
            return null;

        var entries = await GetEntriesAsync(ct);
        foreach (var entry in entries)
        {
            var names = new[] { entry.Title }.Concat(entry.Titles ?? []);
            if (!names.Any(name => GameMatcher.NormalizeTitle(name) == normalizedTitle))
                continue;

            foreach (var rawPath in entry.SavePaths ?? [])
            {
                var resolved = ResolvePath(rawPath);
                if (string.IsNullOrWhiteSpace(resolved))
                    continue;

                return new GameSaveInfo(
                    Title: entry.Title,
                    SourcePath: resolved,
                    SourceUrl: entry.SourceUrl,
                    IsInstalled: true,
                    Exists: _pathExists(resolved));
            }
        }

        return null;
    }

    public GameSaveInfo CreateManual(string title, string sourcePath)
    {
        var resolved = ResolvePath(sourcePath);
        return new GameSaveInfo(
            Title: string.IsNullOrWhiteSpace(title) ? Path.GetFileName(resolved.TrimEnd('\\', '/')) : title.Trim(),
            SourcePath: resolved,
            SourceUrl: null,
            IsInstalled: false,
            Exists: _pathExists(resolved),
            IsManual: true);
    }

    private async Task<IReadOnlyList<GameSavePathEntry>> GetEntriesAsync(CancellationToken ct)
    {
        if (_entriesCache is not null)
            return _entriesCache;

        await _cacheLock.WaitAsync(ct);
        try
        {
            _entriesCache ??= await LoadEntriesAsync(ct);
            return _entriesCache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<IReadOnlyList<GameSavePathEntry>> LoadEntriesAsync(CancellationToken ct)
    {
        if (!File.Exists(_dataPath))
        {
            _logger?.LogWarning($"GameSaveDiscoveryService: database not found at {_dataPath}");
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_dataPath, ct);
            var database = JsonSerializer.Deserialize<GameSaveDatabase>(json, JsonOptions);
            return database?.Entries ?? [];
        }
        catch (Exception ex)
        {
            _logger?.LogError($"GameSaveDiscoveryService: failed to load save-path database — {ex.Message}");
            return [];
        }
    }

    private static string ResolvePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(rawPath.Trim());
        return expanded.TrimEnd('\\', '/');
    }

    private sealed record GameSaveDatabase(int Version, List<GameSavePathEntry>? Entries);
}
