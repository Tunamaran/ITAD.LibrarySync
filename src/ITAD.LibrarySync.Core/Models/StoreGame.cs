namespace ITAD.LibrarySync.Core.Models;

public sealed record StoreGame(
    LauncherId Launcher,
    string StoreId,
    string Title,
    int? PlaytimeMinutes = null,
    DateTimeOffset? LastPlayed = null);
