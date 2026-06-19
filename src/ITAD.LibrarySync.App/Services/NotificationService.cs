using System.Runtime.Versioning;
using ITAD.LibrarySync.Core.Models;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ITAD.LibrarySync.App.Services;

[SupportedOSPlatform("windows")]
public sealed class NotificationService
{
    public void ShowSyncComplete(IReadOnlyList<SyncResult> results)
    {
        var successes = results.Count(r => r.Success);
        var total = results.Count;

        if (total == 0)
        {
            Show("Sync complete", "No launchers were synced.");
            return;
        }

        if (successes == total)
        {
            var summary = BuildSuccessSummary(results);
            Show("Sync complete", summary);
            return;
        }

        if (successes == 0)
        {
            Show("Sync failed", BuildFailureSummary(results));
            return;
        }

        Show(
            "Sync completed with errors",
            $"{successes} of {total} launchers synced successfully.\n{BuildFailureSummary(results)}");
    }

    public void ShowSyncFailed(string message)
    {
        Show("Sync failed", message);
    }

    public void ShowConnected()
    {
        Show("Connected to ITAD", "Successfully connected to IsThereAnyDeal.");
    }

    public void ShowConnectionFailed(string message)
    {
        Show("Connection failed", message);
    }

    public void ShowDisconnected()
    {
        Show("Disconnected from ITAD", "Your ITAD account has been disconnected.");
    }

    public void ShowTokenExpired()
    {
        Show(
            "ITAD session expired",
            "Your IsThereAnyDeal session has expired. Connect again from the tray menu.");
    }

    private static void Show(string title, string body)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(body)
            .Show(toast =>
            {
                toast.Tag = "ITADLibrarySync";
                toast.Group = "Sync";
            });
    }

    private static string BuildSuccessSummary(IReadOnlyList<SyncResult> results)
    {
        var lines = results
            .Where(r => r.Success)
            .Select(r =>
                $"{FormatLauncher(r.Launcher)}: +{r.CollectionAdded}/-{r.CollectionRemoved} collection, " +
                $"+{r.WaitlistAdded}/-{r.WaitlistRemoved} waitlist");

        return string.Join("\n", lines);
    }

    private static string BuildFailureSummary(IReadOnlyList<SyncResult> results)
    {
        var lines = results
            .Where(r => !r.Success)
            .Select(r => $"{FormatLauncher(r.Launcher)}: {r.Error ?? "Unknown error"}");

        return string.Join("\n", lines);
    }

    private static string FormatLauncher(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic",
        LauncherId.Ubisoft => "Ubisoft",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Microsoft",
        _ => launcher.ToString()
    };
}
