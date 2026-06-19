using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class SyncPayloadBuilder(ShopIdResolver shopIds)
{
    public SyncGamePayload ToPayload(StoreGame game) =>
        new(
            Shop: shopIds.GetShopId(game.Launcher),
            Id: game.StoreId,
            Title: game.Title,
            Playtime: game.PlaytimeMinutes,
            LastPlayed: game.LastPlayed);
}
