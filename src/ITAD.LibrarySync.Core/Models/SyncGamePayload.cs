namespace ITAD.LibrarySync.Core.Models;

public sealed record SyncGamePayload(
    int Shop,
    string Id,
    string Title,
    int? Playtime = null,
    DateTimeOffset? LastPlayed = null);
