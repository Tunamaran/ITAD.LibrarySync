using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;
using Xunit;

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
    public void DoesNotMatchDifferentLauncher_ByDefault()
    {
        var owned = new StoreGame(LauncherId.Epic, "same", "Hades");
        var candidate = new StoreGame(LauncherId.Ubisoft, "same", "Hades");
        GameMatcher.IsSameGame(owned, candidate).Should().BeFalse();
    }

    [Fact]
    public void MatchesDifferentLauncher_WhenIgnoreLauncherIsTrue()
    {
        var owned = new StoreGame(LauncherId.Epic, "id1", "Cyberpunk 2077®");
        var candidate = new StoreGame(LauncherId.Xbox, "id2", "Cyberpunk 2077");
        GameMatcher.IsSameGame(owned, candidate, ignoreLauncher: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("Hades", "hades")]
    [InlineData("Cyberpunk 2077®", "cyberpunk 2077")]
    [InlineData("The Witcher 3: Wild Hunt - GOTY Edition", "the witcher 3 wild hunt")]
    [InlineData("Control: Ultimate Edition", "control")]
    public void NormalizeTitle_StripsSymbolsAndEditions(string input, string expected)
    {
        GameMatcher.NormalizeTitle(input).Should().Be(expected);
    }
}
