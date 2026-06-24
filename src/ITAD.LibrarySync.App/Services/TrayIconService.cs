using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using ITAD.LibrarySync.App.ViewModels;
using ITAD.LibrarySync.App.Views;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace ITAD.LibrarySync.App.Services;

public enum TraySyncState
{
    Idle,
    Syncing,
    Success,
    Partial,
    Error
}

[SupportedOSPlatform("windows")]
public sealed class TrayIconService(
    OAuthFlowService oauthFlow,
    TokenStorage tokenStorage,
    ProfileTokenStorage profileTokenStorage,
    AppSettingsStorage appSettingsStorage,
    SyncConfirmationService syncConfirmation,
    SyncStatusService syncStatusService,
    ItadAccountService itadAccountService,
    NotificationService notifications,
    IServiceProvider serviceProvider) : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private TraySyncState _state = TraySyncState.Idle;

    private ISyncOrchestrator Orchestrator =>
        serviceProvider.GetRequiredService<ISyncOrchestrator>();

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = GetIconForState(TraySyncState.Idle),
            ToolTipText = BuildToolTip(TraySyncState.Idle),
            ContextMenu = CreateContextMenu()
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => OpenSettings();
        syncStatusService.SyncCompleted += (_, _) => UpdateTrayAppearance();
    }

    public void SetState(TraySyncState state)
    {
        _state = state;
        UpdateTrayAppearance();
    }

    public void SetSyncing() => SetState(TraySyncState.Syncing);

    public bool IsSyncing => _state == TraySyncState.Syncing;

    public void Activate() => OpenSettings();

    public void RequestExit()
    {
        if (IsSyncing)
        {
            var result = MessageBox.Show(
                "A sync is in progress. Exit anyway?",
                "Exit ITAD Library Sync",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        Application.Current.Shutdown();
    }

    public void RefreshContextMenu()
    {
        if (_trayIcon is not null)
            _trayIcon.ContextMenu = CreateContextMenu();
    }

    private void UpdateTrayAppearance()
    {
        if (_trayIcon is null)
            return;

        _trayIcon.ToolTipText = BuildToolTip(_state);
        _trayIcon.Icon = GetIconForState(_state);
    }

    private string BuildToolTip(TraySyncState state)
    {
        var baseText = state switch
        {
            TraySyncState.Syncing => "ITAD Library Sync — Syncing…",
            TraySyncState.Success => "ITAD Library Sync — Last sync successful",
            TraySyncState.Partial => "ITAD Library Sync — Last sync completed with errors",
            TraySyncState.Error => "ITAD Library Sync — Last sync failed",
            _ => "ITAD Library Sync — Idle"
        };

        if (state == TraySyncState.Syncing)
            return baseText;

        var summary = syncStatusService.GetTrayTooltipSuffix();
        return summary is null ? baseText : $"{baseText}{summary}";
    }

    private static Icon GetIconForState(TraySyncState state) => TrayIconResources.GetIcon(state);

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        var isConnected = tokenStorage.Load() is not null;

        var syncNow = new MenuItem { Header = "Sync Now" };
        syncNow.Click += async (_, _) => await RunSyncAsync();
        menu.Items.Add(syncNow);

        var enabledLaunchers = appSettingsStorage.Load().GetEnabledLaunchers();
        if (enabledLaunchers.Count > 0)
        {
            menu.Items.Add(new Separator());

            foreach (var launcher in enabledLaunchers.OrderBy(l => l))
                menu.Items.Add(CreateLauncherSyncItem($"Sync {GetLauncherMenuLabel(launcher)}", launcher));
        }

        menu.Items.Add(new Separator());

        var settings = new MenuItem { Header = "Settings…" };
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        var viewLog = new MenuItem { Header = "View Last Sync Log" };
        viewLog.Click += (_, _) => OpenLastSyncLog();
        menu.Items.Add(viewLog);

        menu.Items.Add(new Separator());

        if (isConnected)
        {
            var disconnect = new MenuItem { Header = "Disconnect from ITAD" };
            disconnect.Click += (_, _) => Disconnect();
            menu.Items.Add(disconnect);
        }
        else
        {
            var connect = new MenuItem { Header = "Connect to ITAD" };
            connect.Click += async (_, _) => await ConnectAsync();
            menu.Items.Add(connect);
        }

        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => RequestExit();
        menu.Items.Add(exit);

        return menu;
    }

    private static string GetLauncherMenuLabel(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic",
        LauncherId.Ubisoft => "Ubisoft",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Microsoft",
        LauncherId.Ea => "EA",
        _ => LauncherDisplayNames.Get(launcher)
    };

    private MenuItem CreateLauncherSyncItem(string header, LauncherId launcher)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await RunSyncAsync([launcher]);
        return item;
    }

    private async Task RunSyncAsync(IReadOnlyList<LauncherId>? launchers = null)
    {
        var toSync = launchers ?? appSettingsStorage.Load().GetEnabledLaunchers();

        if (!syncConfirmation.Confirm(toSync))
            return;

        try
        {
            await Orchestrator.SyncAllAsync(toSync);
        }
        catch
        {
            // TrayAwareSyncOrchestrator updates tray state and shows notifications.
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            await oauthFlow.ConnectAsync();
            await itadAccountService.RefreshAsync();
            profileTokenStorage.Clear();
            SetState(TraySyncState.Idle);
            RefreshContextMenu();
            notifications.ShowConnected();
        }
        catch (Exception ex)
        {
            SetState(TraySyncState.Error);
            notifications.ShowConnectionFailed(ex.Message);
        }
    }

    private void Disconnect()
    {
        tokenStorage.Clear();
        profileTokenStorage.Clear();
        itadAccountService.Clear();
        SetState(TraySyncState.Idle);
        RefreshContextMenu();
        notifications.ShowDisconnected();
    }

    private void OpenSettings()
    {
        // Defer until after the tray context menu closes; opening synchronously often fails silently.
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, OpenSettingsCore);
    }

    private void OpenSettingsCore()
    {
        try
        {
            if (_settingsWindow is { IsLoaded: true })
            {
                if (_settingsWindow.WindowState == WindowState.Minimized)
                    _settingsWindow.WindowState = WindowState.Normal;

                _settingsWindow.Show();
                BringToFront(_settingsWindow);
                return;
            }

            var viewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
            _settingsWindow = new SettingsWindow(viewModel)
            {
                ShowInTaskbar = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            BringToFront(_settingsWindow);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Could Not Open Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void BringToFront(Window window)
    {
        window.Activate();
        window.Focus();
        window.Topmost = true;
        window.Topmost = false;
    }

    private static void OpenLastSyncLog()
    {
        Directory.CreateDirectory(FileLogger.LogsDirectory);

        var latestLog = FileLogger.GetLatestLogPath();

        if (latestLog is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = latestLog,
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = FileLogger.LogsDirectory,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
