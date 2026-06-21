using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public interface IXboxEntitlementsClient
{
    Task<IReadOnlyList<StoreGame>> GetOwnedGamesAsync(
        XboxAuthorizationData licensingAuth,
        CancellationToken ct);
}
