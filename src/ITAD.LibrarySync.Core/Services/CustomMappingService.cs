using System.Text.Json;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public sealed class CustomMappingService : ICustomMappingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly FileLogger? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public CustomMappingService(string? customPath = null, FileLogger? logger = null)
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
            _filePath = Path.Combine(dir, "custom_mappings.json");
        }
    }

    public async Task SetMappingAsync(CustomGameMapping mapping, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var mappings = await LoadInternalAsync(ct);
            var dict = mappings.ToDictionary(m => (m.Launcher, m.StoreId.ToLowerInvariant()), m => m);

            dict[(mapping.Launcher, mapping.StoreId.ToLowerInvariant())] = mapping;

            var updated = dict.Values.OrderBy(x => x.Title).ToList();
            var json = JsonSerializer.Serialize(updated, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"CustomMappingService: failed to save mapping — {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveMappingAsync(LauncherId launcher, string storeId, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var mappings = await LoadInternalAsync(ct);
            var key = (launcher, storeId.ToLowerInvariant());
            var updated = mappings.Where(m => (m.Launcher, m.StoreId.ToLowerInvariant()) != key).ToList();

            var json = JsonSerializer.Serialize(updated, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"CustomMappingService: failed to remove mapping — {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<CustomGameMapping?> GetMappingAsync(LauncherId launcher, string storeId, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var mappings = await LoadInternalAsync(ct);
            var key = (launcher, storeId.ToLowerInvariant());
            return mappings.FirstOrDefault(m => (m.Launcher, m.StoreId.ToLowerInvariant()) == key);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<CustomGameMapping>> GetAllAsync(CancellationToken ct = default)
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

    private async Task<List<CustomGameMapping>> LoadInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JsonSerializer.Deserialize<List<CustomGameMapping>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
