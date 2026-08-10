using System.Text.Json;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Persists cloud save mappings to %APPDATA%\ITADLibrarySync\cloud-saves.json.
/// </summary>
public sealed class CloudSaveMappingStorage : ICloudSaveMappingStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly FileLogger? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public CloudSaveMappingStorage(string? customPath = null, FileLogger? logger = null)
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
            _filePath = Path.Combine(dir, "cloud-saves.json");
        }
    }

    public async Task<IReadOnlyList<CloudSaveMapping>> GetAllAsync(CancellationToken ct = default)
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

    public async Task SaveAsync(CloudSaveMapping mapping, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var mappings = await LoadInternalAsync(ct);
            var dict = mappings.ToDictionary(
                item => item.SourcePath,
                item => item,
                StringComparer.OrdinalIgnoreCase);

            dict[mapping.SourcePath] = mapping;

            await WriteAllAsync(dict.Values.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToList(), ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"CloudSaveMappingStorage: failed to save mapping — {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveAsync(string sourcePath, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var mappings = await LoadInternalAsync(ct);
            var updated = mappings
                .Where(item => !string.Equals(item.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (updated.Count < mappings.Count)
                await WriteAllAsync(updated, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"CloudSaveMappingStorage: failed to remove mapping — {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<CloudSaveMapping?> FindBySourceAsync(string sourcePath, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var mappings = await LoadInternalAsync(ct);
            return mappings.FirstOrDefault(item =>
                string.Equals(item.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<CloudSaveMapping>> LoadInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JsonSerializer.Deserialize<List<CloudSaveMapping>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task WriteAllAsync(IReadOnlyList<CloudSaveMapping> mappings, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(mappings, JsonOptions);

        // Atomic write: a crash mid-write must not corrupt the persisted file.
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
