using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class LauncherReadResultDisplayPreviewTests
{
    [Fact]
    public void FormatPreviewWarning_WithManyWarnings_ShowsCountSummary()
    {
        var result = new LauncherReadResult(
            LauncherId.Epic,
            IsDetected: true,
            IsLoggedIn: true,
            Owned: [new(LauncherId.Epic, "e1", "Hades")],
            Wishlist: [],
            WishlistReadable: false,
            Warnings: ["skip 1", "skip 2", "skip 3"]);

        LauncherReadResultDisplay.FormatPreviewWarning(result)
            .Should()
            .Be("3 items were skipped or flagged during the library scan.");

        LauncherReadResultDisplay.GetPreviewDetailLines(result)
            .Should()
            .BeEquivalentTo(["skip 1", "skip 2", "skip 3"]);
    }

    [Fact]
    public void FormatPreviewWarning_WithFatalError_ShowsSanitizedMessage()
    {
        var result = new LauncherReadResult(
            LauncherId.BattleNet,
            IsDetected: true,
            IsLoggedIn: false,
            Owned: [],
            Wishlist: [],
            WishlistReadable: false,
            Error: "System.Text.Json.JsonReaderException: 'N' is an invalid start of a value. at System.Text.Json.Utf8JsonReader.Read()");

        LauncherReadResultDisplay.FormatPreviewWarning(result)
            .Should()
            .Be("System.Text.Json.JsonReaderException: 'N' is an invalid start of a value.");
    }

    [Fact]
    public void FormatScanSummary_WithWarnings_IncludesSkipCount()
    {
        var result = new LauncherReadResult(
            LauncherId.Epic,
            IsDetected: true,
            IsLoggedIn: true,
            Owned: [new(LauncherId.Epic, "e1", "Hades"), new(LauncherId.Epic, "e2", "Celeste")],
            Wishlist: [],
            WishlistReadable: false,
            Warnings: ["warn 1", "warn 2"]);

        LauncherReadResultDisplay.FormatScanSummary(result)
            .Should()
            .Be("2 games (2 owned, 0 wishlist) — 2 items skipped");
    }
}
