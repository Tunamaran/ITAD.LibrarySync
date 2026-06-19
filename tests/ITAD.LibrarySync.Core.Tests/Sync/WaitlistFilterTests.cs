using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class WaitlistFilterTests
{
    [Fact]
    public void RemoveOwnedGames_ExcludesMatches()
    {
        var owned = new List<StoreGame>
        {
            new(LauncherId.Epic, "e1", "Hades"),
            new(LauncherId.Epic, "e2", "Celeste")
        };
        var wishlist = new List<StoreGame>
        {
            new(LauncherId.Epic, "w1", "Hades"),
            new(LauncherId.Epic, "w2", "Disco Elysium")
        };

        var result = WaitlistFilter.RemoveOwnedGames(wishlist, owned);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Disco Elysium");
    }

    [Fact]
    public void ShouldSkipCollectionSync_WhenOwnedEmpty()
    {
        WaitlistFilter.ShouldSkipCollectionSync(Array.Empty<StoreGame>()).Should().BeTrue();
        WaitlistFilter.ShouldSkipCollectionSync(new[] { new StoreGame(LauncherId.Epic, "1", "A") }).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipWaitlistSync_WhenWishlistUnreadable()
    {
        WaitlistFilter.ShouldSkipWaitlistSync(wishlistReadable: false, wishlistCount: 0).Should().BeTrue();
        WaitlistFilter.ShouldSkipWaitlistSync(wishlistReadable: true, wishlistCount: 0).Should().BeTrue();
        WaitlistFilter.ShouldSkipWaitlistSync(wishlistReadable: true, wishlistCount: 3).Should().BeFalse();
    }
}
