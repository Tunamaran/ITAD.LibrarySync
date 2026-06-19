using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class GameMatcherTests
{
    [Fact]
    public void MatchesByStoreId_CaseInsensitive()
    {
        var owned = new StoreGame(LauncherId.Epic, "abc123", "Hades");
        var candidate = new StoreGame(LauncherId.Epic, "ABC123", "Different Title");
        GameMatcher.IsSameGame(owned, candidate).Should().BeTrue();
    }

    [Fact]
    public void MatchesByNormalizedTitle_WhenStoreIdDiffers()
    {
        var owned = new StoreGame(LauncherId.Epic, "id1", "Grand Theft Auto V");
        var candidate = new StoreGame(LauncherId.Epic, "id2", "grand theft auto  v ");
        GameMatcher.IsSameGame(owned, candidate).Should().BeTrue();
    }

    [Fact]
    public void DoesNotMatchDifferentLauncher()
    {
        var owned = new StoreGame(LauncherId.Epic, "same", "Hades");
        var candidate = new StoreGame(LauncherId.Ubisoft, "same", "Hades");
        GameMatcher.IsSameGame(owned, candidate).Should().BeFalse();
    }

    [Theory]
    [InlineData("Hades", "hades")]
    [InlineData("  Observer_ ", "observer_")]
    public void NormalizeTitle_TrimsAndLowercases(string input, string expected)
    {
        GameMatcher.NormalizeTitle(input).Should().Be(expected);
    }
}
