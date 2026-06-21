using GameCollector.StoreHandlers.BattleNet;
using GameFinder.Common;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers;

internal static class LauncherClientDetection
{
    internal static AbsolutePath NormalizeClientPath(AbsolutePath clientPath, IFileSystem fileSystem)
    {
        if (clientPath == default)
            return default;

        if (fileSystem.FileExists(clientPath))
            return clientPath;

        var raw = clientPath.GetFullPath().Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(raw))
            return default;

        var normalized = fileSystem.FromUnsanitizedFullPath(raw);
        return fileSystem.FileExists(normalized) ? normalized : default;
    }

    internal static bool IsBattleNetInstalled(BattleNetHandler handler, IFileSystem fileSystem, AbsolutePath clientPath)
    {
        if (NormalizeClientPath(clientPath, fileSystem) != default)
            return true;

        return handler.GetBattleNetPath()
            .Combine("Agent")
            .Combine("product.db")
            .FileExists;
    }

    internal static bool IsXboxInstalled(IFileSystem fileSystem)
    {
        foreach (var root in fileSystem.EnumerateRootDirectories())
        {
            if (!fileSystem.DirectoryExists(root))
                continue;

            if (fileSystem.FileExists(root.Combine(".GamingRoot")))
                return true;

            var modifiableApps = root.Combine("Program Files").Combine("ModifiableWindowsApps");
            if (fileSystem.DirectoryExists(modifiableApps))
                return true;

            var xboxGames = root.Combine("XboxGames");
            if (!fileSystem.DirectoryExists(xboxGames))
                continue;

            foreach (var directory in fileSystem.EnumerateDirectories(xboxGames, recursive: false))
            {
                if (fileSystem.FileExists(directory.Combine("appxmanifest.xml")))
                    return true;

                if (fileSystem.FileExists(directory.Combine("Content").Combine("appxmanifest.xml")))
                    return true;
            }
        }

        return false;
    }
}
