namespace ITAD.LibrarySync.Core.Scheduling;

public enum SyncInterval
{
    Disabled,
    Every6Hours,
    Every12Hours,
    Every24Hours,
    Weekly
}

public sealed class SyncScheduleOptions
{
    public SyncInterval Interval { get; set; } = SyncInterval.Disabled;
    public bool SyncOnStartup { get; set; }
}
