using System.Runtime.Versioning;
using GameCollector.StoreHandlers.EADesktop;
using GameCollector.StoreHandlers.EADesktop.Crypto.Windows;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Launchers.Ea;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers;

[SupportedOSPlatform("windows")]
public sealed class EaReader : ILauncherReader
{
    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true, BaseOnly = true };

    public LauncherId Launcher => LauncherId.Ea;

    public Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var handler = new EADesktopHandler(
                FileSystem.Shared,
                WindowsRegistry.Shared,
                new HardwareInfoProvider());
            var clientPath = EaClientDetection.ResolveClientPath(
                handler.FindClient(),
                FileSystem.Shared,
                WindowsRegistry.Shared);
            var isInstalled = EaClientDetection.IsEaInstalled(FileSystem.Shared, clientPath);

            var customOwned = EaInstallInfoLibraryReader.TryReadOwnedGames(FileSystem.Shared);
            if (customOwned.Count > 0)
            {
                return Task.FromResult(new LauncherReadResult(
                    LauncherId.Ea,
                    IsDetected: true,
                    IsLoggedIn: true,
                    Owned: customOwned,
                    Wishlist: [],
                    WishlistReadable: false));
            }

            var results = EaGameCollectorReader.FindAllGames(OwnedGamesSettings);

            return Task.FromResult(FormatEaResult(EaReadResultMerger.MergeRegistryFallback(
                LauncherReadHelper.ReadOwnedGames(
                    LauncherId.Ea,
                    clientPath,
                    FileSystem.Shared,
                    results,
                    game =>
                    {
                        var storeId = EaStoreIdResolver.Resolve(game.BaseSlug, game.EADesktopGameId.Value);
                        return LauncherReadHelper.MapGame(
                            LauncherId.Ea,
                            storeId ?? string.Empty,
                            game.Name);
                    },
                    treatAsInstalled: isInstalled))));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new LauncherReadResult(
                LauncherId.Ea,
                IsDetected: false,
                IsLoggedIn: false,
                Owned: [],
                Wishlist: [],
                WishlistReadable: false,
                EaReadErrorFormatter.Format(ex)));
        }
    }

    private static LauncherReadResult FormatEaResult(LauncherReadResult result)
    {
        if (result.Error is null)
            return result;

        return result with { Error = EaReadErrorFormatter.FormatFromReadError(result.Error) };
    }
}
