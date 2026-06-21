using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Xbox;

namespace ITAD.LibrarySync.Core.Tests.Launchers.Xbox;

public class XboxTitleHistoryFilterTests
{
    [Fact]
    public void IsEligibleOwnedCandidate_accepts_pc_game_with_pfn()
    {
        var item = new TitleHistoryItem
        {
            Type = "Game",
            Pfn = "Microsoft.Halo_8wekyb3d8bbwe",
            Devices = ["PC"]
        };

        XboxTitleHistoryFilter.IsEligibleOwnedCandidate(item).Should().BeTrue();
    }

    [Fact]
    public void IsEligibleOwnedCandidate_rejects_console_only_history()
    {
        var item = new TitleHistoryItem
        {
            Type = "Game",
            ModernTitleId = "9NBLGGH4R2Q6",
            Devices = ["XboxOne"]
        };

        XboxTitleHistoryFilter.IsEligibleOwnedCandidate(item).Should().BeFalse();
    }

    [Fact]
    public void IsEligibleOwnedCandidate_rejects_non_game_type()
    {
        var item = new TitleHistoryItem
        {
            Type = "Application",
            Pfn = "Microsoft.XboxApp_8wekyb3d8bbwe",
            Devices = ["PC"]
        };

        XboxTitleHistoryFilter.IsEligibleOwnedCandidate(item).Should().BeFalse();
    }
}
