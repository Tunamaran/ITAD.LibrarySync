namespace ITAD.LibrarySync.Core.Launchers;

internal static class LauncherMessageSanitizer
{
    internal static IReadOnlyList<string> SplitCombined(string? combined) =>
        string.IsNullOrWhiteSpace(combined)
            ? []
            : combined
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeLine)
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    internal static string SanitizeLine(string message)
    {
        var line = message.Trim();
        var newlineIndex = line.IndexOfAny(['\r', '\n']);
        if (newlineIndex >= 0)
            line = line[..newlineIndex].Trim();

        var atIndex = line.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex > 0 && line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            line = line[..atIndex].Trim();

        if (line.Length > 240)
            line = line[..237] + "...";

        return line;
    }
}
