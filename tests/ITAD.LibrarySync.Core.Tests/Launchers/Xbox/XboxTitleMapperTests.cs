using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Xbox;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Tests.Launchers.Xbox;

public class XboxTitleMapperTests
{
    [Fact]
    public void ToStoreGame_prefers_store_product_id_over_pfn()
    {
        var item = new TitleHistoryItem
        {
            TitleId = "123",
            Name = "Halo",
            Pfn = "Microsoft.Halo_8wekyb3d8bbwe",
            ModernTitleId = "9NBLGGH4R2Q6"
        };

        var game = XboxTitleMapper.ToStoreGame(item, null);

        game.Should().NotBeNull();
        game!.Launcher.Should().Be(LauncherId.Xbox);
        game.StoreId.Should().Be("9NBLGGH4R2Q6");
        game.Title.Should().Be("Halo");
        game.PlaytimeMinutes.Should().BeNull();
    }

    [Fact]
    public void ToStoreGame_uses_pfn_when_modernTitleId_is_numeric_xbox_title_id()
    {
        var item = new TitleHistoryItem
        {
            TitleId = "1649097896",
            Name = "SnowRunner (Windows 10)",
            Pfn = "FocusHomeInteractiveSA.SnowRunnerWindows10_4hny5m903y3g0",
            ModernTitleId = "1649097896"
        };

        var game = XboxTitleMapper.ToStoreGame(item, null);

        game.Should().NotBeNull();
        game!.StoreId.Should().Be("FocusHomeInteractiveSA.SnowRunnerWindows10_4hny5m903y3g0");
    }

    [Fact]
    public void ToStoreGame_falls_back_to_modernTitleId_when_pfn_missing()
    {
        var item = new TitleHistoryItem
        {
            TitleId = "456",
            Name = "Forza Horizon 5",
            ModernTitleId = "9NBLGGH4R2Q7"
        };

        var game = XboxTitleMapper.ToStoreGame(item, 120);

        game.Should().NotBeNull();
        game!.StoreId.Should().Be("9NBLGGH4R2Q7");
        game.PlaytimeMinutes.Should().Be(120);
    }

    [Fact]
    public void ToStoreGame_falls_back_to_prefixed_titleId()
    {
        var item = new TitleHistoryItem
        {
            TitleId = "789",
            Name = "Legacy Game"
        };

        var game = XboxTitleMapper.ToStoreGame(item, null);

        game.Should().NotBeNull();
        game!.StoreId.Should().Be("xbox:789");
    }

    [Fact]
    public void ToStoreGame_returns_null_for_whitespace_name()
    {
        var item = new TitleHistoryItem
        {
            TitleId = "123",
            Name = "   ",
            Pfn = "Microsoft.Test_8wekyb3d8bbwe"
        };

        var game = XboxTitleMapper.ToStoreGame(item, 30);

        game.Should().BeNull();
    }

    [Fact]
    public void ToStoreGame_passes_through_playtime()
    {
        var item = new TitleHistoryItem
        {
            TitleId = "123",
            Name = "Halo",
            Pfn = "Microsoft.Halo_8wekyb3d8bbwe"
        };

        var game = XboxTitleMapper.ToStoreGame(item, 42);

        game.Should().NotBeNull();
        game!.PlaytimeMinutes.Should().Be(42);
    }
}
