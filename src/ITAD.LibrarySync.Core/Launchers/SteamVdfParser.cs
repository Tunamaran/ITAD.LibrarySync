using System.Text.RegularExpressions;

namespace ITAD.LibrarySync.Core.Launchers;

/// <summary>Parsed entry of a Steam <c>appmanifest_&lt;appid&gt;.acf</c> file.</summary>
public sealed record SteamAppManifest(string AppId, string Title, bool IsInstalled);

/// <summary>
/// Minimal VDF parser for Steam's <c>libraryfolders.vdf</c> and
/// <c>appmanifest_*.acf</c> files. Only the fields the Cloud Saves feature
/// needs are extracted; anything else is ignored.
/// </summary>
public static partial class SteamVdfParser
{
    /// <summary>Extracts the library folder paths (the <c>"path"</c> keys) from <c>libraryfolders.vdf</c>.</summary>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdfText)
    {
        if (string.IsNullOrWhiteSpace(vdfText))
            return [];

        return LibraryPathRegex()
            .Matches(vdfText)
            .Select(match => match.Groups[1].Value.Replace(@"\\", @"\"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Parses an appmanifest: appid, display name and whether the game is fully
    /// installed (StateFlags bit 4). Returns <c>null</c> when appid/name are missing.
    /// </summary>
    public static SteamAppManifest? ParseAppManifest(string acfText)
    {
        if (string.IsNullOrWhiteSpace(acfText))
            return null;

        var appId = Match(acfText, AppIdRegex());
        var title = Match(acfText, NameRegex());
        if (appId is null || title is null)
            return null;

        var state = Match(acfText, StateFlagsRegex());
        var isInstalled = int.TryParse(state, out var flags) && (flags & 4) == 4;
        return new SteamAppManifest(appId, title, isInstalled);
    }

    private static string? Match(string text, Regex regex)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"""path""\s+""([^""]+)""")]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex(@"""appid""\s+""(\d+)""")]
    private static partial Regex AppIdRegex();

    [GeneratedRegex(@"""name""\s+""([^""]*)""")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"""StateFlags""\s+""(\d+)""")]
    private static partial Regex StateFlagsRegex();
}
