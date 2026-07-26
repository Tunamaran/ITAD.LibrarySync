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
using ITAD.LibrarySync.Core.Services;
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
        LanguageManager.Instance.PropertyChanged += (_, _) => RefreshContextMenu();
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
                LanguageManager.Instance["ExitConfirmText"],
                LanguageManager.Instance["ExitConfirmTitle"],
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
        var lang = LanguageManager.Instance;
        var baseText = state switch
        {
            TraySyncState.Syncing => lang["TrayTooltipSyncing"],
            TraySyncState.Success => lang["TrayTooltipSuccess"],
            TraySyncState.Partial => lang["TrayTooltipPartial"],
            TraySyncState.Error => lang["TrayTooltipError"],
            _ => lang["TrayTooltipIdle"]
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
        var lang = LanguageManager.Instance;
        var isConnected = tokenStorage.Load() is not null;

        var syncNow = new MenuItem { Header = lang["TraySyncNow"] };
        syncNow.Click += async (_, _) => await RunSyncAsync();
        menu.Items.Add(syncNow);

        var enabledLaunchers = appSettingsStorage.Load().GetEnabledLaunchers();
        if (enabledLaunchers.Count > 0)
        {
            menu.Items.Add(new Separator());

            foreach (var launcher in enabledLaunchers.OrderBy(l => l))
                menu.Items.Add(CreateLauncherSyncItem(string.Format(lang["TraySyncStore"], GetLauncherMenuLabel(launcher)), launcher));
        }

        menu.Items.Add(new Separator());

        var settings = new MenuItem { Header = lang["TraySettings"] };
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        var viewLog = new MenuItem { Header = lang["TrayViewLog"] };
        viewLog.Click += (_, _) => OpenLastSyncLog();
        menu.Items.Add(viewLog);

        var checkUpdates = new MenuItem { Header = lang["TrayCheckUpdates"] };
        checkUpdates.Click += async (_, _) => await CheckForUpdatesFromTrayAsync();
        menu.Items.Add(checkUpdates);

        menu.Items.Add(new Separator());

        if (isConnected)
        {
            var disconnect = new MenuItem { Header = lang["TrayDisconnect"] };
            disconnect.Click += (_, _) => Disconnect();
            menu.Items.Add(disconnect);
        }
        else
        {
            var connect = new MenuItem { Header = lang["TrayConnect"] };
            connect.Click += async (_, _) => await ConnectAsync();
            menu.Items.Add(connect);
        }

        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = lang["TrayExit"] };
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
                LanguageManager.Instance["CouldNotOpenSettings"],
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

    private async Task CheckForUpdatesFromTrayAsync()
    {
        var lang = LanguageManager.Instance;
        try
        {
            var updateChecker = serviceProvider.GetRequiredService<IUpdateCheckerService>();
            var result = await updateChecker.CheckForUpdatesAsync();

            if (result.HasUpdate)
            {
                var prompt = MessageBox.Show(
                    string.Format(lang["TrayUpdateAvailableText"], result.LatestVersion),
                    lang["TrayUpdateAvailableTitle"],
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (prompt == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = result.ReleaseNotesUrl,
                        UseShellExecute = true
                    });
                }
            }
            else
            {
                MessageBox.Show(
                    string.Format(lang["TrayUpToDateText"], result.CurrentVersion),
                    lang["TrayUpdateCheckTitle"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(lang["TrayUpdateCheckFailedText"], ex.Message),
                lang["ErrorTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
