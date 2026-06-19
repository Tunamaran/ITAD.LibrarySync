using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.Services;

public sealed class TrayIconService(ISyncOrchestrator orchestrator) : IDisposable
{
    private TaskbarIcon? _trayIcon;

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "ITAD Library Sync",
            Icon = SystemIcons.Application,
            ContextMenu = CreateContextMenu()
        };
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var syncNow = new MenuItem { Header = "Sync Now" };
        syncNow.Click += async (_, _) => await SyncNowAsync();
        menu.Items.Add(syncNow);

        var settings = new MenuItem { Header = "Settings" };
        settings.Click += (_, _) => { /* Task 15 */ };
        menu.Items.Add(settings);

        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exit);

        return menu;
    }

    private async Task SyncNowAsync()
    {
        try
        {
            await orchestrator.SyncAllAsync();
        }
        catch (Exception ex)
        {
            _trayIcon?.ShowBalloonTip(
                "Sync Failed",
                ex.Message,
                BalloonIcon.Error);
        }
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
