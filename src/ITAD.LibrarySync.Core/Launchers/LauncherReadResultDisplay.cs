using ITAD.LibrarySync.Core.Launchers.Ea;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public static class LauncherReadResultDisplay
{
    public static string GetDetectionStatus(LauncherReadResult result) =>
        result switch
        {
            { IsDetected: false } => "Not detected",
            { Launcher: LauncherId.Xbox, IsLoggedIn: false } => "Not logged in",
            { Launcher: LauncherId.Ea, Owned.Count: 0, Error: not null } when
                EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(result.Error) => "Error",
            { Owned.Count: > 0 } => "Ready",
            { Launcher: LauncherId.Xbox, IsLoggedIn: true, Error: not null } => "Limited",
            _ => "Ready"
        };

    public static string FormatScanSummary(LauncherReadResult result)
    {
        var total = result.Owned.Count + result.Wishlist.Count;
        var summary = $"{total} games ({result.Owned.Count} owned, {result.Wishlist.Count} wishlist)";
        var skipSuffix = FormatSkipSuffix(result);

        if (result.Error is null)
        {
            if (skipSuffix is not null)
                return $"{summary} — {skipSuffix}";

            if (result.Launcher == LauncherId.BattleNet)
                return summary + " — Battle.net local cache may omit uninstalled owned titles";

            if (result.Launcher == LauncherId.Ea && result.WarningMessages.Count > 0)
                return summary + " — installed games only (local EA cache unavailable)";

            return summary;
        }

        if (result.Owned.Count > 0 || result.Wishlist.Count > 0)
        {
            if (result.Launcher == LauncherId.Xbox && result.Error is not null)
                return $"{summary} — {LauncherMessageSanitizer.SanitizeLine(result.Error)}";

            if (skipSuffix is not null)
                return $"{summary} — {skipSuffix}";

            return $"{summary} — some items skipped";
        }

        if (result.IsDetected && result.Launcher == LauncherId.Xbox)
            return $"0 games — {LauncherMessageSanitizer.SanitizeLine(result.Error)}";

        if (result.Launcher == LauncherId.Ea &&
            EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(result.Error))
        {
            return $"0 games — {LauncherMessageSanitizer.SanitizeLine(result.Error)}";
        }

        if (result.IsDetected)
            return "0 games — launcher detected, no readable library entries";

        return LauncherMessageSanitizer.SanitizeLine(result.Error);
    }

    public static string? FormatPreviewWarning(LauncherReadResult result)
    {
        var details = GetPreviewDetailLines(result);
        if (details.Count == 0)
        {
            return string.IsNullOrWhiteSpace(result.Error)
                ? null
                : LauncherMessageSanitizer.SanitizeLine(result.Error);
        }

        if (result.Owned.Count > 0 || result.Wishlist.Count > 0)
        {
            return details.Count == 1
                ? "1 item was skipped or flagged during the library scan."
                : $"{details.Count} items were skipped or flagged during the library scan.";
        }

        return LauncherMessageSanitizer.SanitizeLine(result.Error ?? details[0]);
    }

    public static IReadOnlyList<string> GetPreviewDetailLines(LauncherReadResult result)
    {
        if (result.WarningMessages.Count > 0)
        {
            return result.WarningMessages
                .Select(LauncherMessageSanitizer.SanitizeLine)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return LauncherMessageSanitizer.SplitCombined(result.Error);
    }

    private static string? FormatSkipSuffix(LauncherReadResult result)
    {
        if (result.WarningMessages.Count > 0)
            return result.WarningMessages.Count == 1
                ? "1 item skipped"
                : $"{result.WarningMessages.Count} items skipped";

        return null;
    }
}
