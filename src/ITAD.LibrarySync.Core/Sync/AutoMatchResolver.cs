using System.Text;

namespace ITAD.LibrarySync.Core.Sync;

public static class AutoMatchResolver
{
    private static readonly Dictionary<string, (string MappedId, string Title)> KnownAutoAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Civilization VI DLCs & Base Game
        ["civilization-vi-gathering-storm"] = ("sid-meiers-civilization-vi-gathering-storm", "Sid Meier's Civilization VI: Gathering Storm"),
        ["civilization-vi-rise-and-fall"] = ("sid-meiers-civilization-vi-rise-and-fall", "Sid Meier's Civilization VI: Rise and Fall"),
        ["civilization-vi"] = ("sid-meiers-civilization-vi", "Sid Meier's Civilization VI"),
        ["sid-meiers-civilization-vi"] = ("sid-meiers-civilization-vi", "Sid Meier's Civilization VI"),

        // Civilization V DLCs & Base Game
        ["civilization-v-brave-new-world"] = ("sid-meiers-civilization-v-brave-new-world", "Sid Meier's Civilization V: Brave New World"),
        ["civilization-v-gods-and-kings"] = ("sid-meiers-civilization-v-gods-and-kings", "Sid Meier's Civilization V: Gods and Kings"),

        // Tom Clancy Franchise
        ["rainbow-six-siege"] = ("tom-clancys-rainbow-six-siege", "Tom Clancy's Rainbow Six Siege"),
        ["ghost-recon-breakpoint"] = ("tom-clancys-ghost-recon-breakpoint", "Tom Clancy's Ghost Recon Breakpoint"),
        ["ghost-recon-wildlands"] = ("tom-clancys-ghost-recon-wildlands", "Tom Clancy's Ghost Recon Wildlands"),
        ["the-division-2"] = ("tom-clancys-the-division-2", "Tom Clancy's The Division 2"),

        // EA / FIFA / FC
        ["ea-sports-fc-24"] = ("ea-sports-fc-24", "EA SPORTS FC 24"),
        ["ea-sports-fc-25"] = ("ea-sports-fc-25", "EA SPORTS FC 25"),
        ["fifa-23"] = ("ea-sports-fifa-23", "EA SPORTS FIFA 23"),
        ["fifa-22"] = ("ea-sports-fifa-22", "EA SPORTS FIFA 22"),

        // Witcher Franchise
        ["the-witcher-3-wild-hunt"] = ("the-witcher-3-wild-hunt", "The Witcher 3: Wild Hunt"),
        ["witcher-3-hearts-of-stone"] = ("the-witcher-3-wild-hunt-hearts-of-stone", "The Witcher 3: Wild Hunt - Hearts of Stone"),
        ["witcher-3-blood-and-wine"] = ("the-witcher-3-wild-hunt-blood-and-wine", "The Witcher 3: Wild Hunt - Blood and Wine"),

        // GTA / Red Dead
        ["gta-v"] = ("grand-theft-auto-v", "Grand Theft Auto V"),
        ["gta-5"] = ("grand-theft-auto-v", "Grand Theft Auto V"),
        ["rdr2"] = ("red-dead-redemption-ii", "Red Dead Redemption 2"),
    };

    public static (string Id, string Title) ResolveAutoMatch(string storeId, string rawTitle)
    {
        var cleanId = storeId.Trim();
        var cleanTitle = rawTitle.Trim();

        // 1. Direct lookup by Store ID
        if (KnownAutoAliases.TryGetValue(cleanId, out var knownByStoreId))
            return (knownByStoreId.MappedId, knownByStoreId.Title);

        // 2. Lookup by title slug
        var slugFromTitle = GenerateSlug(cleanTitle);
        if (KnownAutoAliases.TryGetValue(slugFromTitle, out var knownByTitleSlug))
            return (knownByTitleSlug.MappedId, knownByTitleSlug.Title);

        // 3. Fallback to Smart Normalized Title
        var autoTitle = SmartMatchEngine.AutoNormalizeTitle(cleanTitle);
        var autoId = string.IsNullOrWhiteSpace(cleanId) ? GenerateSlug(autoTitle) : cleanId;

        return (autoId, autoTitle);
    }

    public static string? GetObsoleteIdIfReplaced(string storeId, string rawTitle)
    {
        var cleanId = storeId.Trim();
        if (KnownAutoAliases.TryGetValue(cleanId, out var known) &&
            !string.Equals(cleanId, known.MappedId, StringComparer.OrdinalIgnoreCase))
        {
            return cleanId;
        }

        var slugFromTitle = GenerateSlug(rawTitle);
        if (KnownAutoAliases.TryGetValue(slugFromTitle, out var knownBySlug) &&
            !string.Equals(slugFromTitle, knownBySlug.MappedId, StringComparer.OrdinalIgnoreCase))
        {
            return slugFromTitle;
        }

        return null;
    }

    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lower = text.ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var c in lower)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_')
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                    sb.Append('-');
            }
        }
        return sb.ToString().Trim('-');
    }
}
