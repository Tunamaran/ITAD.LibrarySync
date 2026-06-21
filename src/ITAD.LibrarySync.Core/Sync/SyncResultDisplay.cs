using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static class SyncResultDisplay
{
    public static string Format(LauncherId launcher, SyncResult? result) =>
        result switch
        {
            null => "Skipped",
            { Success: false } => $"Failed: {result.Error ?? "Unknown error"}",
            _ => $"Collection {result.CollectionTotal} (+{result.CollectionAdded}/-{result.CollectionRemoved}), " +
                 $"Waitlist {result.WaitlistTotal} (+{result.WaitlistAdded}/-{result.WaitlistRemoved})"
        };
}
