using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class SyncPayloadBuilder(ShopIdResolver shopIds, ICustomMappingService? customMappings = null)
{
    public async Task<SyncGamePayload> ToPayloadAsync(StoreGame game, CancellationToken ct = default)
    {
        var (autoId, autoTitle) = AutoMatchResolver.ResolveAutoMatch(game.StoreId, game.Title);
        var id = autoId;
        var title = autoTitle;

        if (customMappings != null)
        {
            var custom = await customMappings.GetMappingAsync(game.Launcher, game.StoreId, ct);
            if (custom != null)
            {
                if (!string.IsNullOrWhiteSpace(custom.MappedId))
                    id = custom.MappedId.Trim();
                if (!string.IsNullOrWhiteSpace(custom.Title))
                    title = custom.Title.Trim();
            }
        }

        var playtime = game.PlaytimeMinutes is < 0 ? null : game.PlaytimeMinutes;
        var lastPlayed = game.LastPlayed?.Year is > 1970 ? game.LastPlayed : null;

        return new SyncGamePayload(
            Shop: shopIds.GetShopId(game.Launcher),
            Id: id,
            Title: SyncTitleSanitizer.Sanitize(title),
            Playtime: playtime,
            LastPlayed: lastPlayed);
    }

    public SyncGamePayload ToPayload(StoreGame game)
    {
        var (autoId, autoTitle) = AutoMatchResolver.ResolveAutoMatch(game.StoreId, game.Title);
        var playtime = game.PlaytimeMinutes is < 0 ? null : game.PlaytimeMinutes;
        var lastPlayed = game.LastPlayed?.Year is > 1970 ? game.LastPlayed : null;

        return new SyncGamePayload(
            Shop: shopIds.GetShopId(game.Launcher),
            Id: autoId,
            Title: SyncTitleSanitizer.Sanitize(autoTitle),
            Playtime: playtime,
            LastPlayed: lastPlayed);
    }

    public static bool IsValid(SyncGamePayload payload) =>
        !string.IsNullOrWhiteSpace(payload.Id) &&
        !string.IsNullOrWhiteSpace(payload.Title);
}
