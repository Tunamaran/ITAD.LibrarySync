using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public static class LauncherDisplayNames
{
    public static string Get(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic Games Store",
        LauncherId.Ubisoft => "Ubisoft Connect",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Xbox / Microsoft Store",
        LauncherId.Ea => "EA App",
        _ => launcher.ToString()
    };
}
