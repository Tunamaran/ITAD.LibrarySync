using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static class SyncSummaryDisplay
{
    public static string BuildTraySummary(
        IReadOnlyList<SyncResult> results,
        IReadOnlyList<LauncherId> attemptedLaunchers)
    {
        if (attemptedLaunchers.Count == 0)
            return string.Empty;

        var parts = attemptedLaunchers
            .Select(launcher =>
            {
                var result = results.FirstOrDefault(r => r.Launcher == launcher);
                var label = GetShortLabel(launcher);

                if (result is null || !result.Success)
                    return $"{label}: failed";

                if (result.CollectionAdded > 0 || result.CollectionRemoved > 0)
                    return $"{label}: +{result.CollectionAdded}/-{result.CollectionRemoved}";

                return $"{label}: ok";
            })
            .ToList();

        return string.Join(" | ", parts);
    }

    private static string GetShortLabel(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic",
        LauncherId.Ubisoft => "Ubisoft",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Microsoft",
        LauncherId.Ea => "EA",
        _ => LauncherDisplayNames.Get(launcher)
    };
}
