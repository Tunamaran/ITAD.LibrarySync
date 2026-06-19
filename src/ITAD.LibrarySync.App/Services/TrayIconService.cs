using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

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
    ISyncOrchestrator orchestrator,
    OAuthFlowService oauthFlow,
    TokenStorage tokenStorage,
    NotificationService notifications) : IDisposable
{
    private static readonly string LogsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ITADLibrarySync",
        "logs");

    private TaskbarIcon? _trayIcon;
    private TraySyncState _state = TraySyncState.Idle;

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = GetIconForState(TraySyncState.Idle),
            ToolTipText = GetToolTipForState(TraySyncState.Idle),
            ContextMenu = CreateContextMenu()
        };
    }

    public void SetState(TraySyncState state)
    {
        _state = state;
        UpdateTrayAppearance();
    }

    public void SetSyncing() => SetState(TraySyncState.Syncing);

    public void RefreshContextMenu()
    {
        if (_trayIcon is not null)
            _trayIcon.ContextMenu = CreateContextMenu();
    }

    private void UpdateTrayAppearance()
    {
        if (_trayIcon is null)
            return;

        _trayIcon.ToolTipText = GetToolTipForState(_state);
        _trayIcon.Icon = GetIconForState(_state);
    }

    private static string GetToolTipForState(TraySyncState state) => state switch
    {
        TraySyncState.Syncing => "ITAD Library Sync — Syncing…",
        TraySyncState.Success => "ITAD Library Sync — Last sync successful",
        TraySyncState.Partial => "ITAD Library Sync — Last sync completed with errors",
        TraySyncState.Error => "ITAD Library Sync — Last sync failed",
        _ => "ITAD Library Sync — Idle"
    };

    private static Icon GetIconForState(TraySyncState state) => state switch
    {
        TraySyncState.Syncing => SystemIcons.Information,
        TraySyncState.Success => SystemIcons.Application,
        TraySyncState.Partial => SystemIcons.Warning,
        TraySyncState.Error => SystemIcons.Error,
        _ => SystemIcons.Application
    };

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        var isConnected = tokenStorage.Load() is not null;

        var syncNow = new MenuItem { Header = "Sync Now" };
        syncNow.Click += async (_, _) => await RunSyncAsync();
        menu.Items.Add(syncNow);

        menu.Items.Add(new Separator());

        menu.Items.Add(CreateLauncherSyncItem("Sync Epic", LauncherId.Epic));
        menu.Items.Add(CreateLauncherSyncItem("Sync Ubisoft", LauncherId.Ubisoft));
        menu.Items.Add(CreateLauncherSyncItem("Sync Battle.net", LauncherId.BattleNet));
        menu.Items.Add(CreateLauncherSyncItem("Sync Microsoft", LauncherId.Xbox));

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
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exit);

        return menu;
    }

    private MenuItem CreateLauncherSyncItem(string header, LauncherId launcher)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await RunSyncAsync([launcher]);
        return item;
    }

    private async Task RunSyncAsync(IReadOnlyList<LauncherId>? launchers = null)
    {
        try
        {
            await orchestrator.SyncAllAsync(launchers);
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
        SetState(TraySyncState.Idle);
        RefreshContextMenu();
        notifications.ShowDisconnected();
    }

    private void OpenSettings()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var settingsType = Type.GetType("ITAD.LibrarySync.App.Views.SettingsWindow, ITAD.LibrarySync.App");
            if (settingsType is null)
            {
                MessageBox.Show(
                    "Settings will be available in a future update.",
                    "ITAD Library Sync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (Activator.CreateInstance(settingsType) is Window window)
            {
                window.Show();
                window.Activate();
            }
        });
    }

    private static void OpenLastSyncLog()
    {
        Directory.CreateDirectory(LogsDirectory);

        var latestLog = Directory
            .EnumerateFiles(LogsDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

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
            FileName = LogsDirectory,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
