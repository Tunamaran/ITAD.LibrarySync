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
        Func<TGame, StoreGame> mapGame)
        where TGame : class, IGame
    {
        if (!IsClientDetected(clientPath, fileSystem))
            return NotDetected(launcher);

        var (games, errors) = results.SplitResults();
        var owned = games
            .Select(mapGame)
            .Where(g => !string.IsNullOrWhiteSpace(g.StoreId))
            .ToList();

        var isLoggedIn = owned.Count > 0 || errors.Length == 0;
        var error = errors.Length > 0
            ? string.Join("; ", errors.Select(e => e.Message))
            : null;

        if (!isLoggedIn && error is not null)
            error = $"Unable to read library: {error}";

        return new LauncherReadResult(
            launcher,
            IsDetected: true,
            IsLoggedIn: isLoggedIn,
            Owned: owned,
            Wishlist: [],
            Error: error);
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
        new(launcher, false, false, [], [], "Launcher install not found.");

    internal static LauncherReadResult FromException(LauncherId launcher, Exception ex) =>
        new(launcher, false, false, [], [], ex.Message);

    private static int? ToPlaytimeMinutes(TimeSpan? runTime) =>
        runTime is { } value ? (int)Math.Round(value.TotalMinutes) : null;

    private static DateTimeOffset? ToLastPlayed(DateTime? lastRun) =>
        lastRun is { } value ? new DateTimeOffset(value) : null;
}
