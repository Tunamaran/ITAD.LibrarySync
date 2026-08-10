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
    private static readonly Settings InstalledGamesSettings = new() { OwnedOnly = true, InstalledOnly = true };

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

            var result = LauncherReadHelper.ReadOwnedGames(
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
                treatAsInstalled: isInstalled);

            // Installed-only pass for Cloud Saves (product.db may keep entries
            // for products that are no longer installed).
            var installedResults = handler.FindAllGames(InstalledGamesSettings);
            var installed = LauncherReadHelper.ReadOwnedGames(
                LauncherId.BattleNet,
                clientPath,
                FileSystem.Shared,
                installedResults,
                game => LauncherReadHelper.MapGame(
                    LauncherId.BattleNet,
                    game.ProductId.Value,
                    game.DirName,
                    null,
                    game.LastPlayed),
                treatAsInstalled: isInstalled).Owned;

            return Task.FromResult(result with { Installed = installed });
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.BattleNet, ex));
        }
    }
}
