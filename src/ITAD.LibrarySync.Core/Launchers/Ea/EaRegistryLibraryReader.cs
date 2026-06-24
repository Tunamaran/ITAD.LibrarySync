using System.Runtime.Versioning;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

[SupportedOSPlatform("windows")]
internal static class EaRegistryLibraryReader
{
    private static readonly (RegistryHive Hive, string SubKey)[] GameRoots =
    [
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Origin Games"),
        (RegistryHive.LocalMachine, @"SOFTWARE\Origin Games"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\EA Games"),
        (RegistryHive.LocalMachine, @"SOFTWARE\EA Games")
    ];

    internal static IReadOnlyList<StoreGame> ReadInstalledGames()
    {
        var games = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in ReadOriginRegistryGames())
            games[game.StoreId] = game;

        foreach (var game in EaUninstallRegistryReader.ReadInstalledGames())
            games[game.StoreId] = game;

        return games.Values
            .Where(game => !EaRegistryGameFilter.IsLauncherEntry(game.Title, game.StoreId))
            .OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<StoreGame> ReadOriginRegistryGames()
    {
        var registry = WindowsRegistry.Shared;
        var games = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, subKey) in GameRoots)
        {
            var root = registry.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
            if (root is null)
                continue;

            foreach (var contentId in root.GetSubKeyNames())
            {
                if (string.IsNullOrWhiteSpace(contentId))
                    continue;

                var gameKey = root.OpenSubKey(contentId);
                if (gameKey is null)
                    continue;

                var title = ReadTitle(gameKey, contentId);
                var storeId = EaStoreIdResolver.Resolve(null, contentId.Trim());
                if (string.IsNullOrWhiteSpace(storeId))
                    continue;

                games[storeId] = new StoreGame(LauncherId.Ea, storeId, title);
            }
        }

        return games.Values.ToList();
    }

    private static string ReadTitle(IRegistryKey gameKey, string contentId)
    {
        foreach (var valueName in new[] { "DisplayName", "Title", "GameName" })
        {
            if (gameKey.TryGetString(valueName, out var title) && !string.IsNullOrWhiteSpace(title))
                return title.Trim();
        }

        return contentId;
    }
}
