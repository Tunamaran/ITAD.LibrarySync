using System.Runtime.Versioning;
using GameCollector.StoreHandlers.EGS;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers;

[SupportedOSPlatform("windows")]
public sealed class EpicReader : ILauncherReader
{
    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true };

    public LauncherId Launcher => LauncherId.Epic;

    public Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var handler = new EGSHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var clientPath = handler.FindClient();
            var results = handler.FindAllGames(OwnedGamesSettings);

            var result = LauncherReadHelper.ReadOwnedGames(
                LauncherId.Epic,
                clientPath,
                FileSystem.Shared,
                results,
                game => LauncherReadHelper.MapGame(
                    LauncherId.Epic,
                    game.GameId,
                    game.GameName,
                    game.RunTime,
                    game.LastRunDate));

            if (!result.IsDetected || string.IsNullOrEmpty(result.ResolvedPath))
            {
                var multiPath = MultiDriveScanner.FindExistingPathOnAnyDrive(MultiDriveScanner.EpicCandidatePaths);
                if (!string.IsNullOrEmpty(multiPath))
                {
                    result = result with
                    {
                        IsDetected = true,
                        ResolvedPath = multiPath,
                        DetectionSource = $"Çoklu Sürücü Taraması ({Path.GetPathRoot(multiPath)})"
                    };
                }
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.Epic, ex));
        }
    }
}
