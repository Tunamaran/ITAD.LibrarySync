using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.Services;

public sealed class TrayAwareSyncOrchestrator(
    SyncOrchestrator inner,
    TrayIconService trayIcon,
    NotificationService notifications,
    FileLogger logger) : ISyncOrchestrator
{
    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default)
    {
        trayIcon.SetSyncing();
        logger.LogInfo(launchers is { Count: > 0 }
            ? $"Sync started for {string.Join(", ", launchers)}"
            : "Sync started for all launchers");

        try
        {
            var results = await inner.SyncAllAsync(launchers, ct);
            var state = DetermineState(results);
            trayIcon.SetState(state);
            logger.LogSyncResults(results);
            notifications.ShowSyncComplete(results);
            return results;
        }
        catch (Exception ex)
        {
            trayIcon.SetState(TraySyncState.Error);
            logger.LogError($"Sync failed: {ex.Message}");
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
