using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class SyncResultDisplayTests
{
    [Fact]
    public void Format_WhenResultMissing_ReturnsSkipped()
    {
        SyncResultDisplay.Format(LauncherId.Epic, null).Should().Be("Skipped");
    }

    [Fact]
    public void Format_WhenFailed_IncludesError()
    {
        var result = new SyncResult(
            LauncherId.Epic,
            Success: false,
            CollectionTotal: 0,
            CollectionAdded: 0,
            CollectionRemoved: 0,
            WaitlistTotal: 0,
            WaitlistAdded: 0,
            WaitlistRemoved: 0,
            GlobalWaitlistRemoved: 0,
            Error: "Token expired");

        SyncResultDisplay.Format(LauncherId.Epic, result).Should().Be("Failed: Token expired");
    }

    [Fact]
    public void Format_WhenSuccessful_IncludesCollectionAndWaitlistTotals()
    {
        var result = new SyncResult(
            LauncherId.Epic,
            Success: true,
            CollectionTotal: 280,
            CollectionAdded: 2,
            CollectionRemoved: 1,
            WaitlistTotal: 4,
            WaitlistAdded: 0,
            WaitlistRemoved: 0,
            GlobalWaitlistRemoved: 0);

        SyncResultDisplay.Format(LauncherId.Epic, result)
            .Should()
            .Be("Collection 280 (+2/-1), Waitlist 4 (+0/-0)");
    }
}
