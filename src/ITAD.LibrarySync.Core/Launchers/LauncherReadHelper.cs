using GameFinder.Common;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;
using OneOf;

namespace ITAD.LibrarySync.Core.Launchers;

internal static class LauncherReadHelper
{
    internal static bool IsClientDetected(AbsolutePath clientPath, IFileSystem fileSystem) =>
        clientPath != default && fileSystem.FileExists(clientPath);

    internal static LauncherReadResult ReadOwnedGames<TGame>(
        LauncherId launcher,
        AbsolutePath clientPath,
        IFileSystem fileSystem,
        IEnumerable<OneOf<TGame, ErrorMessage>> results,
        Func<TGame, StoreGame> mapGame,
        bool treatAsInstalled = false)
        where TGame : class, IGame
    {
        var isDetected = IsClientDetected(clientPath, fileSystem) || treatAsInstalled;
        if (!isDetected)
            return NotDetected(launcher);

        var (games, errors) = results.SplitResults();
        var owned = games
            .Select(mapGame)
            .Where(g => !string.IsNullOrWhiteSpace(g.StoreId))
            .GroupBy(g => g.StoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var isLoggedIn = owned.Count > 0 || errors.Length == 0 || treatAsInstalled;
        var errorMessages = errors.Select(error => error.Message).ToList();
        string? error = null;
        IReadOnlyList<string>? warnings = null;

        if (errorMessages.Count > 0)
        {
            if (owned.Count > 0)
                warnings = errorMessages;
            else
                error = $"Unable to read library: {string.Join("; ", errorMessages)}";
        }

        if (!isLoggedIn && error is null && errorMessages.Count > 0)
            error = $"Unable to read library: {string.Join("; ", errorMessages)}";

        var resolvedPathStr = clientPath != default ? clientPath.ToString() : null;
        var detectionSource = treatAsInstalled ? "Kayıt Defteri (Registry)" : "Kayıt Defteri & Yerel Önbellek";

        return new LauncherReadResult(
            launcher,
            IsDetected: true,
            IsLoggedIn: isLoggedIn,
            Owned: owned,
            Wishlist: [],
            WishlistReadable: false,
            Error: error,
            Warnings: warnings,
            ResolvedPath: resolvedPathStr,
            DetectionSource: detectionSource);
    }

    internal static LauncherReadResult MergeOwned(
        LauncherReadResult result,
        IEnumerable<StoreGame> additionalOwned)
    {
        var merged = result.Owned
            .Concat(additionalOwned)
            .Where(g => !string.IsNullOrWhiteSpace(g.StoreId))
            .GroupBy(g => g.StoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return result with
        {
            Owned = merged,
            IsLoggedIn = merged.Count > 0 || result.IsLoggedIn
        };
    }

    internal static StoreGame MapGame(
        LauncherId launcher,
        string storeId,
        string title,
        TimeSpan? runTime = null,
        DateTime? lastRun = null) =>
        new(
            launcher,
            storeId,
            title,
            ToPlaytimeMinutes(runTime),
            ToLastPlayed(lastRun));

    internal static LauncherReadResult NotDetected(LauncherId launcher) =>
        new(launcher, false, false, [], [], WishlistReadable: false, "Launcher install not found.");

    internal static LauncherReadResult FromException(LauncherId launcher, Exception ex) =>
        new(launcher, false, false, [], [], WishlistReadable: false, ex.Message);

    private static int? ToPlaytimeMinutes(TimeSpan? runTime) =>
        runTime is { } value ? (int)Math.Round(value.TotalMinutes) : null;

    private static DateTimeOffset? ToLastPlayed(DateTime? lastRun)
    {
        if (lastRun is not { } value)
            return null;

        try
        {
            return new DateTimeOffset(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
