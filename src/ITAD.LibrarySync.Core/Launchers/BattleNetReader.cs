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
            var clientPath = LauncherClientDetection.NormalizeClientPath(
                handler.FindClient(),
                FileSystem.Shared);
            var isInstalled = LauncherClientDetection.IsBattleNetInstalled(
                handler,
                FileSystem.Shared,
                clientPath);
            var results = handler.FindAllGames(OwnedGamesSettings);

            return Task.FromResult(LauncherReadHelper.ReadOwnedGames(
                LauncherId.BattleNet,
                clientPath,
                FileSystem.Shared,
                results,
                game => LauncherReadHelper.MapGame(
                    LauncherId.BattleNet,
                    game.ProductId.Value,
                    game.DirName,
                    null,
                    game.LastPlayed),
                treatAsInstalled: isInstalled));
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.BattleNet, ex));
        }
    }
}
