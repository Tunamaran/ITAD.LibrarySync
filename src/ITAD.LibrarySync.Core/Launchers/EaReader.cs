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
public sealed class EaReader(EaOnlineLibraryReader? onlineLibraryReader = null) : ILauncherReader
{
    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true, BaseOnly = true };

    public LauncherId Launcher => LauncherId.Ea;

    public async Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var local = ReadLocalLibrary();
            if (local.Owned.Count > 0)
            {
                return FormatEaResult(local with
                {
                    IsDetected = true,
                    IsLoggedIn = true,
                    Error = null
                });
            }

            if (onlineLibraryReader is not null)
            {
                var online = await onlineLibraryReader.TryReadAsync(ct);
                if (online is { Owned.Count: > 0 })
                    return online;
            }

            return FormatEaResult(EaReadResultMerger.MergeRegistryFallback(local));
        }
        catch (Exception ex)
        {
            return new LauncherReadResult(
                LauncherId.Ea,
                IsDetected: false,
                IsLoggedIn: false,
                Owned: [],
                Wishlist: [],
                WishlistReadable: false,
                EaReadErrorFormatter.Format(ex));
        }
    }

    private static LauncherReadResult ReadLocalLibrary()
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
            return new LauncherReadResult(
                LauncherId.Ea,
                IsDetected: true,
                IsLoggedIn: true,
                Owned: customOwned,
                Wishlist: [],
                WishlistReadable: false);
        }

        var results = EaGameCollectorReader.FindAllGames(OwnedGamesSettings);
        return LauncherReadHelper.ReadOwnedGames(
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
            treatAsInstalled: isInstalled);
    }

    private static IReadOnlyList<string> CombineWarnings(
        IReadOnlyList<string>? existing,
        string additional)
    {
        if (existing is null || existing.Count == 0)
            return [additional];

        if (existing.Contains(additional, StringComparer.OrdinalIgnoreCase))
            return existing;

        return existing.Append(additional).ToList();
    }

    private static LauncherReadResult FormatEaResult(LauncherReadResult result)
    {
        if (result.Error is null)
            return result;

        var formattedError = EaReadErrorFormatter.FormatFromReadError(result.Error);
        if (result.Owned.Count == 0 && EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(formattedError))
        {
            return result with
            {
                Error = null,
                Warnings = CombineWarnings(result.Warnings, formattedError)
            };
        }

        return result with { Error = formattedError };
    }
}
