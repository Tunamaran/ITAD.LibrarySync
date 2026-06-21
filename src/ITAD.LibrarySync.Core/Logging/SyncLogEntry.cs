namespace ITAD.LibrarySync.Core.Logging;

public sealed record SyncLogEntry(DateTimeOffset Timestamp, string Level, string Message);
