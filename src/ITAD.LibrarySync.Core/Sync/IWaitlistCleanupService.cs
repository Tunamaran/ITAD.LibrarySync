using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public interface IWaitlistCleanupService
{
    Task<int> RemoveOwnedFromGlobalWaitlistAsync(
        IReadOnlyList<StoreGame> allOwned,
        CancellationToken ct = default);
}
