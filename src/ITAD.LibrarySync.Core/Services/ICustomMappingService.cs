using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface ICustomMappingService
{
    Task SetMappingAsync(CustomGameMapping mapping, CancellationToken ct = default);
    Task RemoveMappingAsync(LauncherId launcher, string storeId, CancellationToken ct = default);
    Task<CustomGameMapping?> GetMappingAsync(LauncherId launcher, string storeId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomGameMapping>> GetAllAsync(CancellationToken ct = default);
}
