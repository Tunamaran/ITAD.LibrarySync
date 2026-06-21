using ITAD.LibrarySync.Core.Auth.Xbox;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public interface IXboxLibraryClient
{
    Task<IReadOnlyList<TitleHistoryItem>> GetTitleHistoryAsync(
        XboxAuthorizationData auth,
        CancellationToken ct);

    Task<IReadOnlyDictionary<string, int>> GetMinutesPlayedAsync(
        XboxAuthorizationData auth,
        IReadOnlyList<string> titleIds,
        CancellationToken ct);
}
