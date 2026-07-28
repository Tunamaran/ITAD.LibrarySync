namespace ITAD.LibrarySync.Core.Models;

public sealed record SyncResult(
    LauncherId Launcher,
    bool Success,
    int CollectionTotal,
    int CollectionAdded,
    int CollectionRemoved,
    int WaitlistTotal,
    int WaitlistAdded,
    int WaitlistRemoved,
    int GlobalWaitlistRemoved,
    string? Error = null,
    LauncherReadResult? ReadResult = null);

