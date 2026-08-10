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
        ["home"] = "%USERPROFILE%",
        ["public"] = "%PUBLIC%"
    };

    /// <summary>
    /// Returns the first resolvable Windows save path from the wikitext, or
    /// <c>null</c> when no Windows row exists or all rows are unresolvable.
    /// The result keeps <c>%USERPROFILE%</c>-style variables (no trailing slash).
    /// </summary>
    public static string? ParseWindowsSavePath(string? wikitext)
    {
        if (string.IsNullOrWhiteSpace(wikitext))
            return null;

        foreach (var block in EnumerateSaveDataBlocks(wikitext))
        {
            var args = SplitTopLevelArgs(block);
            if (args.Count < 3)
                continue;

            // args: [template name, platform, path, ...]
            if (!string.Equals(args[1].Trim(), "Windows", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var rawPath in args.Skip(2))
            {
                var expanded = ExpandAndClean(rawPath);
                if (string.IsNullOrWhiteSpace(expanded))
                    continue;

                return expanded.TrimEnd('\\', '/');
            }
        }

        return null;
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
            else if (block[i] == '|' && depth == 0)
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
