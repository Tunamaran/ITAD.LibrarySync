namespace ITAD.LibrarySync.Core.Models;

public sealed record CustomGameMapping(
    LauncherId Launcher,
    string StoreId,
    string MappedId,
    string Title,
    DateTime CreatedAt);
