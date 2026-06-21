using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class LauncherClientDetectionTests
{
    [Fact]
    public void NormalizeClientPath_TrimsQuotesFromRegistryIconPath()
    {
        var fileSystem = FileSystem.Shared;
        var quotedPath = fileSystem.FromUnsanitizedFullPath(
            @"""C:\Program Files (x86)\Battle.net\Battle.net.exe""");

        var normalized = LauncherClientDetection.NormalizeClientPath(quotedPath, fileSystem);

        if (File.Exists(@"C:\Program Files (x86)\Battle.net\Battle.net.exe"))
        {
            normalized.Should().NotBe(default(AbsolutePath));
            fileSystem.FileExists(normalized).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("Ready", true, true, 3, null)]
    [InlineData("Ready", true, true, 0, null)]
    [InlineData("Ready", true, true, 0, "scan failed")]
    [InlineData("Not detected", false, false, 0, null)]
    public void GetDetectionStatus_UsesOwnedGamesBeforeErrors(
        string expected,
        bool isDetected,
        bool isLoggedIn,
        int ownedCount,
        string? error)
    {
        var result = new Models.LauncherReadResult(
            Models.LauncherId.Epic,
            isDetected,
            isLoggedIn,
            Enumerable.Repeat(new Models.StoreGame(Models.LauncherId.Epic, "id", "Game"), ownedCount).ToList(),
            [],
            WishlistReadable: false,
            error);

        LauncherReadResultDisplay.GetDetectionStatus(result).Should().Be(expected);
    }

    [Fact]
    public void FormatScanSummary_ShowsShortSummaryWhenGamesFoundDespiteWarnings()
    {
        var result = new Models.LauncherReadResult(
            Models.LauncherId.Epic,
            true,
            true,
            [new Models.StoreGame(Models.LauncherId.Epic, "abc", "Test Game")],
            [],
            WishlistReadable: false,
            "some warning");

        LauncherReadResultDisplay.FormatScanSummary(result)
            .Should()
            .Be("1 games (1 owned, 0 wishlist) — some items skipped");
    }
}
