using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public sealed record MicrosoftStoreLibraryReadResult(
    IReadOnlyList<StoreGame> Games,
    string? Warning = null);
