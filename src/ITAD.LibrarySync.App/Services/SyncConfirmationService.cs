using System.Text;
using System.Windows;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;

namespace ITAD.LibrarySync.App.Services;

public sealed class SyncConfirmationService(AppSettingsStorage appSettingsStorage)
{
    public bool Confirm(
        IReadOnlyList<LauncherId> launchers,
        IReadOnlyDictionary<LauncherId, LauncherReadResult?>? previews = null)
    {
        if (launchers.Count == 0)
        {
            MessageBox.Show(
                "No launchers are enabled. Enable at least one launcher in Settings before syncing.",
                "Nothing to Sync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (!appSettingsStorage.Load().ConfirmBeforeSync)
            return true;

        var message = new StringBuilder();
        message.AppendLine("The following stores will be synced to ITAD:");
        message.AppendLine();

        foreach (var launcher in launchers)
        {
            var line = $"• {LauncherDisplayNames.Get(launcher)}";

            if (previews?.TryGetValue(launcher, out var preview) == true && preview is not null)
                line += $" — {preview.Owned.Count} owned, {preview.Wishlist.Count} wishlist";

            message.AppendLine(line);
        }

        message.AppendLine();
        message.Append("Continue?");

        return MessageBox.Show(
                message.ToString(),
                "Confirm Sync",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
