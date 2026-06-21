using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.Services;

public sealed class SyncStatusService(AppSettingsStorage appSettingsStorage)
{
    private readonly Dictionary<LauncherId, string> _statsByLauncher = new();
    private DateTimeOffset? _lastSyncAt;
    private string? _lastSyncSummary;

    public event EventHandler<IReadOnlyList<SyncResult>>? SyncCompleted;

    public SyncStatusService LoadFromSettings()
    {
        var settings = appSettingsStorage.Load();
        _statsByLauncher.Clear();

        foreach (var (launcher, stats) in settings.LastSyncStatsByLauncher)
            _statsByLauncher[launcher] = stats;

        _lastSyncAt = settings.LastSyncAt;
        _lastSyncSummary = settings.LastSyncSummary;
        return this;
    }

    public string GetStats(LauncherId launcher) =>
        _statsByLauncher.TryGetValue(launcher, out var stats) ? stats : "—";

    public string? GetTrayTooltipSuffix()
    {
        if (_lastSyncAt is null || string.IsNullOrWhiteSpace(_lastSyncSummary))
            return null;

        return $"\nLast sync: {_lastSyncAt.Value.LocalDateTime:g}\n{_lastSyncSummary}";
    }

    public void RecordResults(IReadOnlyList<SyncResult> results, IReadOnlyList<LauncherId> attemptedLaunchers)
    {
        foreach (var launcher in attemptedLaunchers)
        {
            var result = results.FirstOrDefault(r => r.Launcher == launcher);
            _statsByLauncher[launcher] = SyncResultDisplay.Format(launcher, result);
        }

        _lastSyncAt = DateTimeOffset.Now;
        _lastSyncSummary = SyncSummaryDisplay.BuildTraySummary(results, attemptedLaunchers);
        Persist();
        SyncCompleted?.Invoke(this, results);
    }

    private void Persist()
    {
        var settings = appSettingsStorage.Load();
        settings.LastSyncStatsByLauncher = new Dictionary<LauncherId, string>(_statsByLauncher);
        settings.LastSyncAt = _lastSyncAt;
        settings.LastSyncSummary = _lastSyncSummary;
        appSettingsStorage.Save(settings);
    }
}
