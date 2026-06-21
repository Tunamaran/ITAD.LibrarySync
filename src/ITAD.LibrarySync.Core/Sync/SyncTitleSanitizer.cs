using System.Text.RegularExpressions;

namespace ITAD.LibrarySync.Core.Sync;

public static partial class SyncTitleSanitizer
{
    public static string Sanitize(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        var sanitized = title
            .Replace('\u2122', ' ')
            .Replace('\u00AE', ' ')
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace("™", string.Empty, StringComparison.Ordinal)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Trim();

        return WhitespaceRegex().Replace(sanitized, " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
