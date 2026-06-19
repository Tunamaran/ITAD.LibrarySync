using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public interface ISyncOrchestrator
{
    Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default);
}
