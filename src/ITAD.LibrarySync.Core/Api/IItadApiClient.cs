using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public interface IItadApiClient
{
    Task<string> LinkProfileAsync(string accessToken, string accountId, string accountName, CancellationToken ct = default);
    Task<ItadSyncResponse> SyncCollectionAsync(string accessToken, string profileToken, IReadOnlyList<SyncGamePayload> games, CancellationToken ct = default);
    Task<ItadSyncResponse> SyncWaitlistAsync(string accessToken, string profileToken, IReadOnlyList<SyncGamePayload> games, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetWaitlistGameIdsAsync(string accessToken, CancellationToken ct = default);
    Task DeleteWaitlistGamesAsync(string accessToken, IReadOnlyList<string> gameIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, int>> GetShopMapAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> LookupGameIdsByShopIdsAsync(int shopId, IReadOnlyList<string> shopGameIds, CancellationToken ct = default);
}

public sealed record ItadSyncResponse(int Total, int Added, int Removed);
