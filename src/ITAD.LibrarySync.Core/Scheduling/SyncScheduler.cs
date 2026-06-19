using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Scheduling;

public sealed class SyncScheduler(ISyncOrchestrator orchestrator) : IDisposable
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _loopCts;

    public void Apply(SyncScheduleOptions options)
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
        _timer?.Dispose();
        _timer = null;

        var period = options.Interval switch
        {
            SyncInterval.Every6Hours => TimeSpan.FromHours(6),
            SyncInterval.Every12Hours => TimeSpan.FromHours(12),
            SyncInterval.Every24Hours => TimeSpan.FromHours(24),
            SyncInterval.Weekly => TimeSpan.FromDays(7),
            _ => (TimeSpan?)null
        };

        if (period is not null)
        {
            _loopCts = new CancellationTokenSource();
            _ = RunLoopAsync(period.Value, _loopCts.Token);
        }
    }

    private async Task RunLoopAsync(TimeSpan period, CancellationToken ct)
    {
        _timer = new PeriodicTimer(period);
        try
        {
            while (await _timer.WaitForNextTickAsync(ct))
                await orchestrator.SyncAllAsync(ct: ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _timer?.Dispose();
    }
}
