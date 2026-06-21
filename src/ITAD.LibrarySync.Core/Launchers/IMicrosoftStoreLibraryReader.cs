using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public interface IMicrosoftStoreLibraryReader
{
    Task<MicrosoftStoreLibraryReadResult> ReadOwnedGamesAsync(CancellationToken ct = default);
}
