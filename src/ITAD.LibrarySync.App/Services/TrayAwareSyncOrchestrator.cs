using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.Services;

public sealed class TrayAwareSyncOrchestrator(
    SyncOrchestrator inner,
    TrayIconService trayIcon,
    NotificationService notifications) : ISyncOrchestrator
{
    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default)
    {
        trayIcon.SetSyncing();

        try
        {
            var results = await inner.SyncAllAsync(launchers, ct);
            var state = DetermineState(results);
            trayIcon.SetState(state);
            notifications.ShowSyncComplete(results);
            return results;
        }
        catch (Exception ex)
        {
            trayIcon.SetState(TraySyncState.Error);
            notifications.ShowSyncFailed(ex.Message);
            throw;
        }
    }

    private static TraySyncState DetermineState(IReadOnlyList<SyncResult> results)
    {
        if (results.Count == 0)
            return TraySyncState.Success;

        var successes = results.Count(r => r.Success);

        if (successes == results.Count)
            return TraySyncState.Success;

        if (successes == 0)
            return TraySyncState.Error;

        return TraySyncState.Partial;
    }
}
