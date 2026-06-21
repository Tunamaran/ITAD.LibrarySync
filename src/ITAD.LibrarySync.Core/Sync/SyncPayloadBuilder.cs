using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class SyncPayloadBuilder(ShopIdResolver shopIds)
{
    public SyncGamePayload ToPayload(StoreGame game)
    {
        var title = game.Title.Trim();
        var id = game.StoreId.Trim();
        var playtime = game.PlaytimeMinutes is < 0 ? null : game.PlaytimeMinutes;
        var lastPlayed = game.LastPlayed?.Year is > 1970 ? game.LastPlayed : null;

        return new SyncGamePayload(
            Shop: shopIds.GetShopId(game.Launcher),
            Id: id,
            Title: SyncTitleSanitizer.Sanitize(title),
            Playtime: playtime,
            LastPlayed: lastPlayed);
    }

    public static bool IsValid(SyncGamePayload payload) =>
        !string.IsNullOrWhiteSpace(payload.Id) &&
        !string.IsNullOrWhiteSpace(payload.Title);
}
