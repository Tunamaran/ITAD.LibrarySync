using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.Services;

public sealed class TrayAwareSyncOrchestrator(
    SyncOrchestrator inner,
    AppSettingsStorage appSettingsStorage,
    SyncStatusService syncStatusService,
    TrayIconService trayIcon,
    NotificationService notifications,
    SyncProgressService syncProgress,
    FileLogger logger) : ISyncOrchestrator
{
    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default)
    {
        var resolved = launchers ?? appSettingsStorage.Load().GetEnabledLaunchers();

        if (resolved.Count == 0)
        {
            logger.LogInfo("Sync skipped: no enabled launchers.");
            return Array.Empty<SyncResult>();
        }

        trayIcon.SetSyncing();
        syncProgress.BeginSync();
        logger.LogInfo(resolved.Count == Enum.GetValues<LauncherId>().Length
            ? "Sync started for all enabled launchers."
            : $"Sync started for {string.Join(", ", resolved)}.");

        try
        {
            var results = await inner.SyncAllAsync(resolved, ct);
            syncStatusService.RecordResults(results, resolved);
            var state = DetermineState(results);
            trayIcon.SetState(state);
            logger.LogSyncResults(results);
            syncProgress.CompleteSync(results);
            notifications.ShowSyncComplete(results);
            return results;
        }
        catch (Exception ex)
        {
            trayIcon.SetState(TraySyncState.Error);
            logger.LogError($"Sync failed: {ex.Message}");
            syncProgress.FailSync(ex.Message);
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
