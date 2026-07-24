namespace ITAD.LibrarySync.Core.Models;

public sealed record LogEntry(
    DateTime Timestamp,
    string Level,
    string Message);
