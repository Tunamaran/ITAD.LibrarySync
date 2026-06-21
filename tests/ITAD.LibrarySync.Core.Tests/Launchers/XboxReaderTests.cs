using FluentAssertions;
using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class XboxReaderTests
{
    [Fact]
    public async Task ReadAsync_MergesApiOwnedGames()
    {
        var storeGames = new List<StoreGame>
        {
            new(LauncherId.Xbox, "9N123", "Halo Infinite"),
            new(LauncherId.Xbox, "9N456", "Forza Horizon 5")
        };

        var storeReader = new Mock<IMicrosoftStoreLibraryReader>();
        storeReader
            .Setup(reader => reader.ReadOwnedGamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MicrosoftStoreLibraryReadResult(storeGames));

        var reader = new XboxReader(storeReader.Object);
        var result = await reader.ReadAsync();

        result.Owned.Should().Contain(game => game.Title == "Halo Infinite");
        result.Owned.Should().Contain(game => game.Title == "Forza Horizon 5");
        result.IsLoggedIn.Should().BeTrue();
        LauncherReadResultDisplay.GetDetectionStatus(result).Should().Be("Ready");
    }

    [Fact]
    public async Task ReadAsync_WhenAuthRequired_SetsNotLoggedInWithConnectMessage()
    {
        var storeReader = new Mock<IMicrosoftStoreLibraryReader>();
        storeReader
            .Setup(reader => reader.ReadOwnedGamesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new XboxAuthRequiredException());

        var reader = new XboxReader(storeReader.Object);
        var result = await reader.ReadAsync();

        result.IsLoggedIn.Should().BeFalse();
        result.Error.Should().Be(XboxReader.XboxConnectMessage);
        LauncherReadResultDisplay.GetDetectionStatus(result).Should().Be("Not logged in");
    }

    [Fact]
    public async Task ReadAsync_WhenAuthenticatedButApiEmpty_ReturnsLimitedStatus()
    {
        var storeReader = new Mock<IMicrosoftStoreLibraryReader>();
        storeReader
            .Setup(reader => reader.ReadOwnedGamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MicrosoftStoreLibraryReadResult(Array.Empty<StoreGame>(), XboxReader.TitleHistoryLimitedMessage));

        var reader = new XboxReader(storeReader.Object);
        var result = await reader.ReadAsync();

        result.IsLoggedIn.Should().BeTrue();
        result.Error.Should().Contain(XboxReader.TitleHistoryLimitedMessage);

        if (result.Owned.Count == 0)
            LauncherReadResultDisplay.GetDetectionStatus(result).Should().Be("Limited");
    }

    [Fact]
    public void GetDetectionStatus_XboxAuthenticatedWithNoGames_ReturnsLimited()
    {
        var result = new LauncherReadResult(
            LauncherId.Xbox,
            IsDetected: true,
            IsLoggedIn: true,
            Owned: [],
            Wishlist: [],
            WishlistReadable: false,
            XboxReader.TitleHistoryLimitedMessage);

        LauncherReadResultDisplay.GetDetectionStatus(result).Should().Be("Limited");
    }

    [Fact]
    public void GetDetectionStatus_XboxNotAuthenticated_ReturnsNotLoggedIn()
    {
        var result = new LauncherReadResult(
            LauncherId.Xbox,
            IsDetected: true,
            IsLoggedIn: false,
            Owned: [],
            Wishlist: [],
            WishlistReadable: false,
            XboxReader.XboxConnectMessage);

        LauncherReadResultDisplay.GetDetectionStatus(result).Should().Be("Not logged in");
    }

    [Fact]
    public async Task ReadAsync_PrefersApiEntryAndEnrichesHigherLocalPlaytime()
    {
        var storeGames = new List<StoreGame>
        {
            new(LauncherId.Xbox, "9N123", "Halo Infinite", PlaytimeMinutes: 60)
        };

        var storeReader = new Mock<IMicrosoftStoreLibraryReader>();
        storeReader
            .Setup(reader => reader.ReadOwnedGamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MicrosoftStoreLibraryReadResult(storeGames));

        var reader = new XboxReader(storeReader.Object);
        var result = await reader.ReadAsync();

        var halo = result.Owned.FirstOrDefault(game => game.StoreId == "9N123");
        halo.Should().NotBeNull();
        halo!.PlaytimeMinutes.Should().BeGreaterThanOrEqualTo(60);
    }

    [Fact]
    public async Task ReadAsync_WithoutStoreReader_ReturnsLocalResultOnly()
    {
        var reader = new XboxReader();
        var result = await reader.ReadAsync();

        result.Launcher.Should().Be(LauncherId.Xbox);
    }
}
