using System.Runtime.Versioning;
using GameCollector.StoreHandlers.Xbox;
using GameFinder.Common;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers;

[SupportedOSPlatform("windows")]
public sealed class XboxReader : ILauncherReader
{
    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true };

    public LauncherId Launcher => LauncherId.Xbox;

    public Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var handler = new XboxHandler(FileSystem.Shared);
            var clientPath = handler.FindClient();
            var results = handler.FindAllGames(OwnedGamesSettings);

            return Task.FromResult(LauncherReadHelper.ReadOwnedGames(
                LauncherId.Xbox,
                clientPath,
                FileSystem.Shared,
                results,
                game => LauncherReadHelper.MapGame(
                    LauncherId.Xbox,
                    game.GameId,
                    game.GameName,
                    game.RunTime,
                    game.LastRunDate)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.Xbox, ex));
        }
    }
}
