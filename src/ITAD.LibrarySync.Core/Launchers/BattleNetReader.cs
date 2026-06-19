using System.Runtime.Versioning;
using GameCollector.StoreHandlers.BattleNet;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers;

[SupportedOSPlatform("windows")]
public sealed class BattleNetReader : ILauncherReader
{
    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true };

    public LauncherId Launcher => LauncherId.BattleNet;

    public Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var handler = new BattleNetHandler(FileSystem.Shared, WindowsRegistry.Shared);
            var clientPath = handler.FindClient();
            var results = handler.FindAllGames(OwnedGamesSettings);

            return Task.FromResult(LauncherReadHelper.ReadOwnedGames(
                LauncherId.BattleNet,
                clientPath,
                FileSystem.Shared,
                results,
                game => LauncherReadHelper.MapGame(
                    LauncherId.BattleNet,
                    game.GameId,
                    game.GameName,
                    game.RunTime,
                    game.LastRunDate ?? game.LastPlayed)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.BattleNet, ex));
        }
    }
}
