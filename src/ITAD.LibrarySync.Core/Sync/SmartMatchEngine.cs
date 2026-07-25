using System.Text.RegularExpressions;

namespace ITAD.LibrarySync.Core.Sync;

public static partial class SmartMatchEngine
{
    private static readonly (string Pattern, string Replacement)[] PrefixRules = [
        (@"^(Sid Meier'?s\s+)?Civilization\s+VI\b", "Sid Meier's Civilization VI"),
        (@"^(Sid Meier'?s\s+)?Civilization\s+V\b", "Sid Meier's Civilization V"),
        (@"^(Tom Clancy'?s\s+)?Rainbow Six\b", "Tom Clancy's Rainbow Six"),
        (@"^(Tom Clancy'?s\s+)?Ghost Recon\b", "Tom Clancy's Ghost Recon"),
        (@"^(Tom Clancy'?s\s+)?The Division\b", "Tom Clancy's The Division"),
        (@"^(EA SPORTS™?\s+)?FC\b", "EA SPORTS FC"),
        (@"^(EA SPORTS™?\s+)?FIFA\b", "EA SPORTS FIFA"),
        (@"^(WARHAMMER\s+40,000:?\s*)", "Warhammer 40,000: ")
    ];

    private static readonly string[] RegionSuffixes = [
        "(WW)", "(ROW)", "(EU)", "(US)", "(RU/CIS)", "[Digital Code]", "[Online Game Code]", "(Global)"
    ];

    public static string AutoNormalizeTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return rawTitle;

        var title = SyncTitleSanitizer.Sanitize(rawTitle);

        foreach (var suffix in RegionSuffixes)
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        foreach (var (pattern, replacement) in PrefixRules)
        {
            if (Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase))
            {
                title = Regex.Replace(title, pattern, replacement, RegexOptions.IgnoreCase);
                break;
            }
        }

        if (IsDlcTitle(title) && !title.Contains(':'))
        {
            title = AutoFormatDlcTitle(title);
        }

        return title.Trim();
    }

    public static bool IsDlcTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;

        return title.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Expansion", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Season Pass", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Add-On", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Content Pack", StringComparison.OrdinalIgnoreCase);
    }

    private static string AutoFormatDlcTitle(string title)
    {
        if (title.Contains(" - "))
        {
            var parts = title.Split(" - ", 2);
            return $"{parts[0].Trim()}: {parts[1].Trim()}";
        }
        return title;
    }
}
