using System.Runtime.Versioning;
using GameCollector.StoreHandlers.Ubisoft;
using GameFinder.Common;
using GameFinder.RegistryUtils;
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

            return Task.FromResult(LauncherReadHelper.ReadOwnedGames(
                LauncherId.Ubisoft,
                clientPath,
                FileSystem.Shared,
                results,
                game => LauncherReadHelper.MapGame(
                    LauncherId.Ubisoft,
                    game.GameId,
                    game.GameName,
                    game.RunTime,
                    game.LastRunDate)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.Ubisoft, ex));
        }
    }
}
