using System.IO;

namespace ITAD.LibrarySync.Core.Launchers;

public static class MultiDriveScanner
{
    public static readonly string[] EpicCandidatePaths = [
        @"ProgramData\Epic\EpicGamesLauncher\Data\Manifests",
        @"Program Files\Epic Games",
        @"Program Files (x86)\Epic Games",
        @"Epic Games"
    ];

    public static readonly string[] UbisoftCandidatePaths = [
        @"Program Files (x86)\Ubisoft\Ubisoft Game Launcher",
        @"Program Files\Ubisoft\Ubisoft Game Launcher",
        @"Ubisoft\Ubisoft Game Launcher"
    ];

    public static readonly string[] EaCandidatePaths = [
        @"ProgramData\EA Desktop",
        @"Program Files\EA Desktop",
        @"Program Files\EA Games",
        @"Program Files (x86)\EA Games"
    ];

    public static readonly string[] BattleNetCandidatePaths = [
        @"ProgramData\Battle.net\Agent\product.db",
        @"Program Files (x86)\Battle.net",
        @"Program Files\Battle.net"
    ];

    public static IReadOnlyList<DriveInfo> GetFixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string? FindExistingPathOnAnyDrive(string[] relativePaths)
    {
        foreach (var drive in GetFixedDrives())
        {
            var driveLetter = drive.Name; // e.g. "C:\"
            foreach (var relativePath in relativePaths)
            {
                try
                {
                    var candidate = Path.Combine(driveLetter, relativePath);
                    if (File.Exists(candidate) || Directory.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // Ignore drive access errors
                }
            }
        }
        return null;
    }
}
