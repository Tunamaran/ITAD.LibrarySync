using System.Text.Json;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public sealed class UnmatchedTitlesService : IUnmatchedTitlesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly FileLogger? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public UnmatchedTitlesService(string? customPath = null, FileLogger? logger = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ITADLibrarySync");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "unmatched_titles.json");
        }
    }

    public async Task AddAsync(UnmatchedTitle title, CancellationToken ct = default) =>
        await AddRangeAsync([title], ct);

    public async Task AddRangeAsync(IEnumerable<UnmatchedTitle> titles, CancellationToken ct = default)
    {
        var newItems = titles.ToList();
        if (newItems.Count == 0) return;

        await _semaphore.WaitAsync(ct);
        try
        {
            var current = await LoadInternalAsync(ct);

            // Deduplicate by Launcher + StoreId + Title, keeping the latest timestamp
            var dict = current.ToDictionary(item => (item.Launcher, item.StoreId, item.Title), item => item);

            foreach (var item in newItems)
            {
                dict[(item.Launcher, item.StoreId, item.Title)] = item;
            }

            var updated = dict.Values
                .OrderByDescending(x => x.DetectedAt)
                .Take(500)
                .ToList();

            var json = JsonSerializer.Serialize(updated, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"UnmatchedTitlesService: failed to save titles — {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<UnmatchedTitle>> GetAllAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await LoadInternalAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"UnmatchedTitlesService: failed to clear titles — {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<UnmatchedTitle>> LoadInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JsonSerializer.Deserialize<List<UnmatchedTitle>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
