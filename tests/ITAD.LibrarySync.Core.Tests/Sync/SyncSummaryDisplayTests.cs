using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class SyncSummaryDisplayTests
{
    [Fact]
    public void BuildTraySummary_FormatsSuccessfulChanges()
    {
        var results = new List<SyncResult>
        {
            new(
                LauncherId.Epic,
                Success: true,
                CollectionTotal: 280,
                CollectionAdded: 2,
                CollectionRemoved: 1,
                WaitlistTotal: 4,
                WaitlistAdded: 0,
                WaitlistRemoved: 0,
                GlobalWaitlistRemoved: 0)
        };

        SyncSummaryDisplay.BuildTraySummary(results, [LauncherId.Epic])
            .Should()
            .Be("Epic: +2/-1");
    }

    [Fact]
    public void BuildTraySummary_MarksFailedLauncher()
    {
        var results = new List<SyncResult>
        {
            new(
                LauncherId.Ubisoft,
                Success: false,
                CollectionTotal: 0,
                CollectionAdded: 0,
                CollectionRemoved: 0,
                WaitlistTotal: 0,
                WaitlistAdded: 0,
                WaitlistRemoved: 0,
                GlobalWaitlistRemoved: 0,
                Error: "Timeout")
        };

        SyncSummaryDisplay.BuildTraySummary(results, [LauncherId.Ubisoft])
            .Should()
            .Be("Ubisoft: failed");
    }
}
