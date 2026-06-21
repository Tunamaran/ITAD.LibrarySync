using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class WaitlistCleanupService(
    IItadApiClient api,
    ItadOAuthService oauth,
    ShopIdResolver shopIds) : IWaitlistCleanupService
{
    public async Task<int> RemoveOwnedFromGlobalWaitlistAsync(
        IReadOnlyList<StoreGame> allOwned,
        CancellationToken ct = default)
    {
        if (allOwned.Count == 0)
            return 0;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var waitlistIds = await api.GetWaitlistGameIdsAsync(accessToken, ct);
        if (waitlistIds.Count == 0)
            return 0;

        var waitlistSet = waitlistIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in allOwned.GroupBy(g => g.Launcher))
        {
            if (!shopIds.TryGetShopId(group.Key, out var shopId))
                continue;

            var storeIds = group.Select(g => g.StoreId).ToList();
            var itadIds = await api.LookupGameIdsByShopIdsAsync(shopId, storeIds, ct);

            foreach (var id in itadIds)
            {
                if (waitlistSet.Contains(id))
                    toRemove.Add(id);
            }
        }

        if (toRemove.Count == 0)
            return 0;

        await api.DeleteWaitlistGamesAsync(accessToken, toRemove.ToList(), ct);
        return toRemove.Count;
    }
}
