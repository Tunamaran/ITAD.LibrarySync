using FluentAssertions;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Tests.Api;

public class ShopIdResolverTests
{
    [Fact]
    public void LoadFromShopMap_ResolvesBlizzardShopForBattleNet()
    {
        var resolver = new ShopIdResolver();
        resolver.LoadFromShopMap(new Dictionary<string, int>
        {
            ["Blizzard"] = 4,
            ["Epic Game Store"] = 16,
            ["Microsoft Store"] = 48,
            ["Ubisoft Store"] = 62
        });

        resolver.GetShopId(LauncherId.BattleNet).Should().Be(4);
        resolver.GetShopId(LauncherId.Epic).Should().Be(16);
        resolver.GetShopId(LauncherId.Xbox).Should().Be(48);
        resolver.GetShopId(LauncherId.Ubisoft).Should().Be(62);
    }

    [Fact]
    public void TryGetShopId_ReturnsFalseWhenLauncherMissing()
    {
        var resolver = new ShopIdResolver();
        resolver.LoadFromShopMap(new Dictionary<string, int> { ["Epic Game Store"] = 16 });

        resolver.TryGetShopId(LauncherId.BattleNet, out _).Should().BeFalse();
    }
}
