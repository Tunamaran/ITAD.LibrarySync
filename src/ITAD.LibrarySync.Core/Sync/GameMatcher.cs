using System.Text.RegularExpressions;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static partial class GameMatcher
{
    public static bool IsSameGame(StoreGame a, StoreGame b, bool ignoreLauncher = false)
    {
        if (!ignoreLauncher && a.Launcher != b.Launcher)
            return false;

        if (string.Equals(a.StoreId, b.StoreId, StringComparison.OrdinalIgnoreCase))
            return true;

        var normA = NormalizeTitle(a.Title);
        var normB = NormalizeTitle(b.Title);

        return !string.IsNullOrWhiteSpace(normA) && normA == normB;
    }

    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // 1. Remove trademark / copyright symbols
        var text = SymbolRegex().Replace(title, string.Empty);

        // 2. Remove common edition suffixes for cross-store matching
        text = EditionRegex().Replace(text, string.Empty);

        // 3. Remove punctuation
        text = PunctuationRegex().Replace(text, " ");

        // 4. Collapse whitespace & lowercase
        var collapsed = WhitespaceRegex().Replace(text.Trim(), " ");
        return collapsed.ToLowerInvariant();
    }

    [GeneratedRegex(@"[®™©]")]
    private static partial Regex SymbolRegex();

    [GeneratedRegex(@"(?i)\b(game of the year|goty|deluxe|ultimate|standard|enhanced|digital|definitive)\s+edition\b")]
    private static partial Regex EditionRegex();

    [GeneratedRegex(@"[:\-_,.'""!\?\(\)\[\]]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
