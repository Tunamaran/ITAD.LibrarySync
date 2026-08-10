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
    private static readonly Settings InstalledGamesSettings = new() { OwnedOnly = true, InstalledOnly = true };

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
                        DetectionSource = $"Multi-Drive Scan ({Path.GetPathRoot(multiPath)})"
                    };
                }
            }

            // Installed-only pass for Cloud Saves (the owned pass may include
            // games that are no longer installed).
            var installedResults = handler.FindAllGames(InstalledGamesSettings);
            var installed = LauncherReadHelper.ReadOwnedGames(
                LauncherId.Epic,
                clientPath,
                FileSystem.Shared,
                installedResults,
                game => LauncherReadHelper.MapGame(
                    LauncherId.Epic,
                    game.GameId,
                    game.GameName,
                    game.RunTime,
                    game.LastRunDate)).Owned;

            return Task.FromResult(result with { Installed = installed });
        }
        catch (Exception ex)
        {
            return Task.FromResult(LauncherReadHelper.FromException(LauncherId.Epic, ex));
        }
    }
}
