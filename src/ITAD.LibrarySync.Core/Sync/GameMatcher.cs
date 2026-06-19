using System.Text.RegularExpressions;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static partial class GameMatcher
{
    public static bool IsSameGame(StoreGame a, StoreGame b)
    {
        if (a.Launcher != b.Launcher)
            return false;

        if (string.Equals(a.StoreId, b.StoreId, StringComparison.OrdinalIgnoreCase))
            return true;

        return NormalizeTitle(a.Title) == NormalizeTitle(b.Title);
    }

    public static string NormalizeTitle(string title)
    {
        var collapsed = WhitespaceRegex().Replace(title.Trim(), " ");
        return collapsed.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
