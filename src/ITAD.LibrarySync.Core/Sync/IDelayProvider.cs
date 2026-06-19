namespace ITAD.LibrarySync.Core.Sync;

public interface IDelayProvider
{
    Task DelayAsync(TimeSpan delay, CancellationToken ct = default);
}

public sealed class DefaultDelayProvider : IDelayProvider
{
    public Task DelayAsync(TimeSpan delay, CancellationToken ct = default) =>
        Task.Delay(delay, ct);
}
