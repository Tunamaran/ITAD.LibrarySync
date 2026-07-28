using ITAD.LibrarySync.Core.Launchers.Ea;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public static class LauncherReadResultDisplay
{
    public static Func<string, string> StringResolver { get; set; } = GetEnglishDefault;

    private static string GetEnglishDefault(string key) => key switch
    {
        "StatusNotDetected" => "Not detected",
        "StatusNotLoggedIn" => "Not logged in",
        "StatusReady" => "Ready",
        "StatusLimited" => "Limited",
        "StatusError" => "Error",
        "ScanSummaryFormat" => "{0} games ({1} owned, {2} wishlist)",
        "ScanSummaryBnetNotice" => "Battle.net local cache may omit uninstalled owned titles",
        "ScanSummaryEaPartial" => "partial library (local fallback)",
        "ScanSummaryEaOnline" => "online EA library",
        "ScanSummaryNoEntries" => "0 games — launcher detected, no readable library entries",
        "ScanSummaryItemSkipped" => "1 item skipped",
        "ScanSummaryItemsSkipped" => "{0} items skipped",
        _ => key
    };

    public static string GetDetectionStatus(LauncherReadResult result, Func<string, string>? lang = null)
    {
        lang ??= StringResolver;
        return result switch
        {
            { IsDetected: false } => lang("StatusNotDetected"),
            { Launcher: LauncherId.Xbox, IsLoggedIn: false } => lang("StatusNotLoggedIn"),
            { Launcher: LauncherId.Ea, IsLoggedIn: false, Owned.Count: 0 } => lang("StatusNotLoggedIn"),
            { Launcher: LauncherId.Ea, Owned.Count: 0, Error: not null } when
                EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(result.Error) => lang("StatusNotLoggedIn"),
            { Owned.Count: > 0 } => lang("StatusReady"),
            { Launcher: LauncherId.Xbox, IsLoggedIn: true, Error: not null } => lang("StatusLimited"),
            _ => lang("StatusReady")
        };
    }

    public static string FormatScanSummary(LauncherReadResult result, Func<string, string>? lang = null)
    {
        lang ??= StringResolver;
        var total = result.Owned.Count + result.Wishlist.Count;
        var summaryFormat = lang("ScanSummaryFormat");
        var summary = summaryFormat.Contains("{0}")
            ? string.Format(summaryFormat, total, result.Owned.Count, result.Wishlist.Count)
            : $"{total} games ({result.Owned.Count} owned, {result.Wishlist.Count} wishlist)";

        var skipSuffix = FormatSkipSuffix(result, lang);

        if (result.Error is null)
        {
            if (skipSuffix is not null)
                return $"{summary} — {skipSuffix}";

            if (result.Launcher == LauncherId.BattleNet)
                return $"{summary} — {lang("ScanSummaryBnetNotice")}";

            if (result.Launcher == LauncherId.Ea && result.WarningMessages.Count > 0)
                return $"{summary} — {lang("ScanSummaryEaPartial")}";

            if (result.Launcher == LauncherId.Ea && result.IsLoggedIn && result.Owned.Count > 0)
                return $"{summary} — {lang("ScanSummaryEaOnline")}";

            return summary;
        }

        if (result.Owned.Count > 0 || result.Wishlist.Count > 0)
        {
            if (result.Launcher == LauncherId.Xbox && result.Error is not null)
                return $"{summary} — {LauncherMessageSanitizer.SanitizeLine(result.Error)}";

            if (skipSuffix is not null)
                return $"{summary} — {skipSuffix}";

            return $"{summary} — {lang("ScanSummaryItemSkipped")}";
        }

        if (result.IsDetected && result.Launcher == LauncherId.Xbox)
            return $"0 games — {LauncherMessageSanitizer.SanitizeLine(result.Error)}";

        if (result.Launcher == LauncherId.Ea &&
            EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(result.Error))
        {
            return $"0 games — {EaOnlineLibraryReader.ConnectEaMessage}";
        }

        if (result.IsDetected)
            return lang("ScanSummaryNoEntries");

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

    private static string? FormatSkipSuffix(LauncherReadResult result, Func<string, string>? lang = null)
    {
        lang ??= StringResolver;
        if (result.WarningMessages.Count > 0)
        {
            var format = result.WarningMessages.Count == 1 ? lang("ScanSummaryItemSkipped") : lang("ScanSummaryItemsSkipped");
            return format.Contains("{0}") ? string.Format(format, result.WarningMessages.Count) : format;
        }

        return null;
    }
}
