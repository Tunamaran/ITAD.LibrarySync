using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface IUnmatchedTitlesService
{
    Task AddAsync(UnmatchedTitle title, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<UnmatchedTitle> titles, CancellationToken ct = default);
    Task<IReadOnlyList<UnmatchedTitle>> GetAllAsync(CancellationToken ct = default);
    Task RemoveAsync(LauncherId launcher, string storeId, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
