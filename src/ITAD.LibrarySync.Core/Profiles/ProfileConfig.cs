using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Profiles;

public static class ProfileConfig
{
    public static (string AccountId, string AccountName) Get(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => ("epic", "Epic Games Library"),
        LauncherId.Ubisoft => ("ubisoft", "Ubisoft Connect Library"),
        LauncherId.BattleNet => ("battlenet", "Battle.net Library"),
        LauncherId.Xbox => ("xbox", "Microsoft Store Library"),
        LauncherId.Ea => ("ea", "EA App Library"),
        _ => throw new ArgumentOutOfRangeException(nameof(launcher))
    };
}
