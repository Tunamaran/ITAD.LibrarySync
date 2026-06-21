using System.Runtime.Versioning;
using GameCollector.StoreHandlers.Ubisoft;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Launchers.Ubisoft;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers;

[SupportedOSPlatform("windows")]
public sealed class UbisoftReader : ILauncherReader
{
    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true };

    public LauncherId Launcher => LauncherId.Ubisoft;

    public Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var handler = new UbisoftHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var clientPath = handler.FindClient();
            var results = handler.FindAllGames(OwnedGamesSettings);

            var gameCollectorResult = LauncherReadHelper.ReadOwnedGames(
                LauncherId.Ubisoft,
                clientPath,
                FileSystem.Shared,
                results,
                game => LauncherReadHelper.MapGame(
                    LauncherId.Ubisoft,
                    game.GameId,
                    game.GameName,
                    game.RunTime,
                    game.LastRunDate));

            var localOwned = UbisoftLocalLibraryReader.ReadOwnedGames();
            if (localOwned.Count == 0)
                return Task.FromResult(gameCollectorResult);

            return Task.FromResult(MergeOwnedLibraries(localOwned, gameCollectorResult));
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.Ubisoft, ex));
        }
    }

    private static LauncherReadResult MergeOwnedLibraries(
        IReadOnlyList<StoreGame> localOwned,
        LauncherReadResult gameCollectorResult)
    {
        var merged = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in localOwned)
        {
            if (UbisoftLocalLibraryReader.IsPlaceholderTitle(game.Title))
                continue;

            merged[game.StoreId] = game;
        }

        foreach (var game in gameCollectorResult.Owned)
        {
            if (UbisoftLocalLibraryReader.IsPlaceholderTitle(game.Title))
                continue;

            if (merged.TryGetValue(game.StoreId, out var existing))
            {
                merged[game.StoreId] = existing with
                {
                    Title = UbisoftLocalLibraryReader.IsPlaceholderTitle(existing.Title)
                        ? game.Title
                        : existing.Title,
                    PlaytimeMinutes = game.PlaytimeMinutes ?? existing.PlaytimeMinutes,
                    LastPlayed = game.LastPlayed ?? existing.LastPlayed
                };
                continue;
            }

            merged[game.StoreId] = game;
        }

        var owned = merged.Values.ToList();

        return gameCollectorResult with
        {
            Owned = owned,
            IsLoggedIn = owned.Count > 0 || gameCollectorResult.IsLoggedIn
        };
    }
}
