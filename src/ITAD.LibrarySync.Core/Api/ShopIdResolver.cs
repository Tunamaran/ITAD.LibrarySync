using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public sealed class ShopIdResolver
{
    private readonly Dictionary<LauncherId, int> _map = new();
    private static readonly Dictionary<LauncherId, string[]> FallbackNames = new()
    {
        [LauncherId.Epic] = ["Epic Game Store", "Epic Games Store"],
        [LauncherId.Ubisoft] = ["Ubisoft Store", "Ubisoft Connect"],
        [LauncherId.BattleNet] = ["Battle.net", "Blizzard Shop"],
        [LauncherId.Xbox] = ["Microsoft Store", "Xbox Store"]
    };

    public void LoadFromShopMap(IReadOnlyDictionary<string, int> shopMapByTitle)
    {
        foreach (var (launcher, names) in FallbackNames)
        {
            foreach (var name in names)
            {
                if (shopMapByTitle.TryGetValue(name, out var id))
                {
                    _map[launcher] = id;
                    break;
                }
            }
        }
    }

    public int GetShopId(LauncherId launcher) =>
        _map.TryGetValue(launcher, out var id)
            ? id
            : throw new InvalidOperationException($"Shop ID not resolved for {launcher}");
}
