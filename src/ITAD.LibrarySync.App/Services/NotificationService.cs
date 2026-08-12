using System.Runtime.Versioning;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ITAD.LibrarySync.App.Services;

[SupportedOSPlatform("windows")]
public sealed class NotificationService(AppSettingsStorage appSettingsStorage)
{
    private static LanguageManager Lang => LanguageManager.Instance;

    public void ShowSyncComplete(IReadOnlyList<SyncResult> results)
    {
        var successes = results.Count(r => r.Success);
        var total = results.Count;

        if (total == 0)
        {
            Show(Lang["NotifSyncComplete"], Lang["NotifNoLaunchers"]);
            return;
        }

        if (successes == total)
        {
            var summary = BuildSuccessSummary(results);
            Show(Lang["NotifSyncComplete"], summary);
            return;
        }

        if (successes == 0)
        {
            Show(Lang["NotifSyncFailed"], BuildFailureSummary(results));
            return;
        }

        Show(
            Lang["NotifSyncPartial"],
            $"{string.Format(Lang["NotifPartialFormat"], successes, total)}\n{BuildFailureSummary(results)}");
    }

    public void ShowSyncFailed(string message)
    {
        Show(Lang["NotifSyncFailed"], message);
    }

    public void ShowConnected()
    {
        Show(Lang["NotifConnectedTitle"], Lang["NotifConnectedBody"]);
    }

    public void ShowConnectionFailed(string message)
    {
        Show(Lang["NotifConnectionFailed"], message);
    }

    public void ShowDisconnected()
    {
        Show(Lang["NotifDisconnectedTitle"], Lang["NotifDisconnectedBody"]);
    }

    public void ShowTokenExpired()
    {
        Show(Lang["NotifTokenExpiredTitle"], Lang["NotifTokenExpiredBody"]);
    }

    public void ShowInfo(string title, string body)
    {
        Show(title, body);
    }

    private void Show(string title, string body)
    {
        if (!appSettingsStorage.Load().ShowNotifications)
            return;

        new ToastContentBuilder()
            .AddText(title)
            .AddText(body)
            .Show(toast =>
            {
#pragma warning disable CA1416
                toast.Tag = "ITADLibrarySync";
                toast.Group = "Sync";
#pragma warning restore CA1416
            });
    }

    private static string BuildSuccessSummary(IReadOnlyList<SyncResult> results)
    {
        var lines = results
            .Where(r => r.Success)
            .Select(r =>
                $"{FormatLauncher(r.Launcher)}: +{r.CollectionAdded}/-{r.CollectionRemoved} {Lang["NotifCollectionLabel"]}, " +
                $"+{r.WaitlistAdded}/-{r.WaitlistRemoved} {Lang["NotifWaitlistLabel"]}");

        return string.Join("\n", lines);
    }

    private static string BuildFailureSummary(IReadOnlyList<SyncResult> results)
    {
        var lines = results
            .Where(r => !r.Success)
            .Select(r => $"{FormatLauncher(r.Launcher)}: {FormatLauncherError(r)}");

        return string.Join("\n", lines);
    }

    private static string FormatLauncherError(SyncResult result) =>
        result.Launcher == LauncherId.Xbox
        && string.Equals(result.Error, XboxReader.XboxConnectMessage, StringComparison.Ordinal)
            ? Lang["NotifReconnectXbox"]
            : result.Error ?? Lang["NotifUnknownError"];

    private static string FormatLauncher(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic",
        LauncherId.Ubisoft => "Ubisoft",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Microsoft",
        LauncherId.Ea => "EA App",
        _ => launcher.ToString()
    };
}
