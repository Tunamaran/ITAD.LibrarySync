using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public sealed class ShopIdResolver
{
    private readonly Dictionary<LauncherId, int> _map = new();
    private static readonly Dictionary<LauncherId, string[]> FallbackNames = new()
    {
        [LauncherId.Epic] = ["Epic Game Store", "Epic Games Store"],
        [LauncherId.Ubisoft] = ["Ubisoft Store", "Ubisoft Connect"],
        [LauncherId.BattleNet] = ["Blizzard", "Battle.net", "Blizzard Shop"],
        [LauncherId.Xbox] = ["Microsoft Store", "Xbox Store"],
        [LauncherId.Ea] = ["EA Store", "Origin"]
    };

    public void LoadFromShopMap(IReadOnlyDictionary<string, int> shopMapByTitle)
    {
        var lookup = shopMapByTitle
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);

        foreach (var (launcher, names) in FallbackNames)
        {
            foreach (var name in names)
            {
                if (lookup.TryGetValue(name, out var id))
                {
                    _map[launcher] = id;
                    break;
                }
            }
        }
    }

    public bool TryGetShopId(LauncherId launcher, out int shopId) =>
        _map.TryGetValue(launcher, out shopId!);

    public int GetShopId(LauncherId launcher) =>
        _map.TryGetValue(launcher, out var id)
            ? id
            : throw new InvalidOperationException($"Shop ID not resolved for {launcher}");
}
