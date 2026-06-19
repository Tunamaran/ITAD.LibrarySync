using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static class WaitlistFilter
{
    public static IReadOnlyList<StoreGame> RemoveOwnedGames(
        IReadOnlyList<StoreGame> wishlist,
        IReadOnlyList<StoreGame> owned)
    {
        return wishlist
            .Where(w => !owned.Any(o => GameMatcher.IsSameGame(w, o)))
            .ToList();
    }

    public static bool ShouldSkipCollectionSync(IReadOnlyList<StoreGame> owned)
        => owned.Count == 0;

    public static bool ShouldSkipWaitlistSync(bool wishlistReadable, int wishlistCount)
        => !wishlistReadable || wishlistCount == 0;
}
