using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface ICloudSaveMappingStorage
{
    Task<IReadOnlyList<CloudSaveMapping>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Inserts or replaces the mapping identified by its source path.</summary>
    Task SaveAsync(CloudSaveMapping mapping, CancellationToken ct = default);

    Task RemoveAsync(string sourcePath, CancellationToken ct = default);

    Task<CloudSaveMapping?> FindBySourceAsync(string sourcePath, CancellationToken ct = default);
}
