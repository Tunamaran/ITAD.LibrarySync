using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public interface IWaitlistSyncService
{
    Task<ItadSyncResponse?> SyncAsync(LauncherReadResult read, CancellationToken ct = default);
}
