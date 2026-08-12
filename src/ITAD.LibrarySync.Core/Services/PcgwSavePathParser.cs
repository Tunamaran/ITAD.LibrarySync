using System.Text;
using System.Text.RegularExpressions;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Extracts the Windows save-game folder from PCGamingWiki section wikitext.
/// Save locations live in <c>{{Game data/saves|Windows|path|...}}</c> (or
/// <c>{{Game data/row|...}}</c>) templates whose path arguments use
/// <c>{{p|...}}</c> placeholders, e.g. <c>{{p|userprofile\Documents}}</c>
/// → <c>%USERPROFILE%\Documents</c>.
/// </summary>
public static partial class PcgwSavePathParser
{
    /// <summary>
    /// Placeholders that can be resolved to Windows environment-variable paths.
    /// Paths depending on other placeholders ({{p|steam}}, {{p|uid}}, …) are
    /// considered unresolvable and skipped.
    /// </summary>
    private static readonly Dictionary<string, string> PlaceholderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["userprofile"] = "%USERPROFILE%",
        ["userprofile\\documents"] = "%USERPROFILE%\\Documents",
        ["documents"] = "%USERPROFILE%\\Documents",
        ["appdata"] = "%APPDATA%",
        ["localappdata"] = "%LOCALAPPDATA%",
        ["locallow"] = "%USERPROFILE%\\AppData\\LocalLow",
        ["appdata\\locallow"] = "%USERPROFILE%\\AppData\\LocalLow",
        ["userprofile\\appdata\\locallow"] = "%USERPROFILE%\\AppData\\LocalLow",
        ["home"] = "%USERPROFILE%",
        ["public"] = "%PUBLIC%",
        ["programdata"] = "%PROGRAMDATA%",
        ["saved games"] = "%USERPROFILE%\\Saved Games",
        ["userprofile\\saved games"] = "%USERPROFILE%\\Saved Games",
        ["steam"] = "%ProgramFiles(x86)%\\Steam",

        // Dynamic user ID placeholders mapped to wildcard marker for directory resolution
        ["uid"] = "*",
        ["steamid"] = "*",
        ["steamid3"] = "*",
        ["steamid64"] = "*",
        ["user-id"] = "*",
        ["userid"] = "*",
        ["id"] = "*",
        ["accountid"] = "*",
        ["profileid"] = "*",
        ["username"] = "*"
    };

    /// <summary>
    /// Returns the first resolvable Windows save path from the wikitext, or
    /// <c>null</c> when no Windows-compatible row exists or all rows are unresolvable.
    /// </summary>
    public static string? ParseWindowsSavePath(string? wikitext)
    {
        return ParseWindowsSavePaths(wikitext).FirstOrDefault();
    }

    /// <summary>
    /// Returns all resolvable Windows/Launcher save path candidates from the wikitext.
    /// </summary>
    public static IReadOnlyList<string> ParseWindowsSavePaths(string? wikitext)
    {
        if (string.IsNullOrWhiteSpace(wikitext))
            return [];

        var results = new List<string>();

        foreach (var block in EnumerateSaveDataBlocks(wikitext))
        {
            var args = SplitTopLevelArgs(block);
            if (args.Count < 3)
                continue;

            // args: [template name, platform, path, ...]
            if (!IsWindowsCompatiblePlatform(args[1]))
                continue;

            foreach (var rawPath in args.Skip(2))
            {
                var expanded = ExpandAndClean(rawPath);
                if (string.IsNullOrWhiteSpace(expanded))
                    continue;

                var trimmed = expanded.TrimEnd('\\', '/');
                if (!results.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(trimmed);
                }
            }
        }

        return results;
    }

    private static bool IsWindowsCompatiblePlatform(string rawPlatform)
    {
        if (string.IsNullOrWhiteSpace(rawPlatform))
            return false;

        var platform = rawPlatform.Trim();

        if (platform.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("PC", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (platform.StartsWith("Steam", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Epic", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("GOG", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Ubisoft", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Uplay", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Origin", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("EA", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Battle.net", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("Xbox", StringComparison.OrdinalIgnoreCase))
        {
            if (platform.Contains("Linux", StringComparison.OrdinalIgnoreCase) ||
                platform.Contains("Mac", StringComparison.OrdinalIgnoreCase) ||
                platform.Contains("OS X", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateSaveDataBlocks(string text)
    {
        var index = 0;
        while (true)
        {
            var start = text.IndexOf("{{Game data/", index, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                yield break;

            var nameEnd = text.IndexOfAny(['|', '}'], start + 2);
            if (nameEnd < 0)
                yield break;

            var name = text[(start + 2)..nameEnd].Trim();
            if (name.Equals("Game data/saves", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Game data/row", StringComparison.OrdinalIgnoreCase))
            {
                var block = ReadUntilBlockEnd(text, start);
                yield return block;
                index = start + block.Length;
            }
            else
            {
                index = start + 2;
            }
        }
    }

    private static string ReadUntilBlockEnd(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length - 1; i++)
        {
            if (text[i] == '{' && text[i + 1] == '{')
            {
                depth++;
                i++;
            }
            else if (text[i] == '}' && text[i + 1] == '}')
            {
                depth--;
                i++;
                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        return text[start..];
    }

    private static List<string> SplitTopLevelArgs(string block)
    {
        var args = new List<string>();
        var depth = 0;
        var wikiDepth = 0;
        var current = new StringBuilder();

        for (var i = 2; i < block.Length - 1; i++)
        {
            if (block[i] == '{' && block[i + 1] == '{')
            {
                depth++;
                current.Append("{{");
                i++;
            }
            else if (block[i] == '}' && block[i + 1] == '}')
            {
                depth--;
                if (depth < 0)
                    break; // the block's own closing braces — stop

                current.Append("}}");
                i++;
            }
            else if (block[i] == '[' && block[i + 1] == '[')
            {
                // Wiki links ([[page|label]]) are atomic: their '|' is not an argument separator.
                wikiDepth++;
                current.Append("[[");
                i++;
            }
            else if (block[i] == ']' && block[i + 1] == ']')
            {
                if (wikiDepth > 0)
                    wikiDepth--;

                current.Append("]]");
                i++;
            }
            else if (block[i] == '|' && depth == 0 && wikiDepth == 0)
            {
                args.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(block[i]);
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args;
    }

    private static string? ExpandAndClean(string raw)
    {
        var text = HtmlCommentRegex().Replace(raw, string.Empty);
        text = BrTagRegex().Replace(text, string.Empty);
        text = WikiLinkRegex().Replace(text, match => match.Groups[1].Value);

        var unresolved = false;
        text = PlaceholderRegex().Replace(text, match =>
        {
            if (PlaceholderMap.TryGetValue(match.Groups[1].Value.Trim(), out var value))
                return value;

            unresolved = true;
            return match.Value;
        });

        if (unresolved)
            return null;

        text = RemainingTemplateRegex().Replace(text, string.Empty);
        text = HtmlTagRegex().Replace(text, string.Empty);
        return text.Trim();
    }

    [GeneratedRegex(@"\{\{\s*p\s*\|([^}|]+)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"\[\[(?:[^\]|]*\|)?([^\]]*)\]\]")]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(@"\{\{[^{}]*\}\}")]
    private static partial Regex RemainingTemplateRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
