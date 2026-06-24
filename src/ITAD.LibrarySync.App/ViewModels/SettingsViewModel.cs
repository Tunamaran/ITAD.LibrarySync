using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITAD.LibrarySync.App.Launchers;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.App.Views;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Auth.Ea;
using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly OAuthFlowService _oauthFlow;
    private readonly XboxOAuthFlowService _xboxOAuthFlow;
    private readonly XboxOAuthService _xboxOAuthService;
    private readonly EaOAuthFlowService _eaOAuthFlow;
    private readonly EaOAuthService _eaOAuthService;
    private readonly TokenStorage _tokenStorage;
    private readonly ProfileTokenStorage _profileTokenStorage;
    private readonly AppSettingsStorage _appSettingsStorage;
    private readonly SyncScheduler _syncScheduler;
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly SyncStatusService _syncStatusService;
    private readonly SyncConfirmationService _syncConfirmation;
    private readonly WindowsStartupService _windowsStartupService;
    private readonly ItadAccountService _itadAccountService;
    private readonly TrayIconService _trayIconService;
    private readonly AppSettings _settings;

    public SettingsViewModel(
        OAuthFlowService oauthFlow,
        XboxOAuthFlowService xboxOAuthFlow,
        XboxOAuthService xboxOAuthService,
        EaOAuthFlowService eaOAuthFlow,
        EaOAuthService eaOAuthService,
        TokenStorage tokenStorage,
        ProfileTokenStorage profileTokenStorage,
        AppSettingsStorage appSettingsStorage,
        SyncScheduler syncScheduler,
        ISyncOrchestrator syncOrchestrator,
        SyncStatusService syncStatusService,
        SyncConfirmationService syncConfirmation,
        WindowsStartupService windowsStartupService,
        ItadAccountService itadAccountService,
        TrayIconService trayIconService,
        IReadOnlyList<ILauncherReader> readers)
    {
        _oauthFlow = oauthFlow;
        _xboxOAuthFlow = xboxOAuthFlow;
        _xboxOAuthService = xboxOAuthService;
        _eaOAuthFlow = eaOAuthFlow;
        _eaOAuthService = eaOAuthService;
        _tokenStorage = tokenStorage;
        _profileTokenStorage = profileTokenStorage;
        _appSettingsStorage = appSettingsStorage;
        _syncScheduler = syncScheduler;
        _syncOrchestrator = syncOrchestrator;
        _syncStatusService = syncStatusService;
        _syncConfirmation = syncConfirmation;
        _windowsStartupService = windowsStartupService;
        _itadAccountService = itadAccountService;
        _trayIconService = trayIconService;
        _settings = appSettingsStorage.Load();

        SelectedInterval = _settings.Interval;
        SyncOnStartup = _settings.SyncOnStartup;
        ConfirmBeforeSync = _settings.ConfirmBeforeSync;
        StartWithWindows = _settings.StartWithWindows;
        ShowNotifications = _settings.ShowNotifications;
        SelectedLogLevel = _settings.LogLevel;

        LauncherStatuses = new ObservableCollection<LauncherSettingsItem>(
            readers
                .OrderBy(r => r.Launcher)
                .Select(r => new LauncherSettingsItem(r, _settings.IsLauncherEnabled(r.Launcher))));

        foreach (var launcher in LauncherStatuses)
            launcher.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(LauncherSettingsItem.IsEnabled))
                    SaveLauncherSettings();
            };

        RefreshConnectionState();
        RefreshXboxConnectionState();
        RefreshEaConnectionState();
        ApplySyncStatsFromService();
        _syncStatusService.SyncCompleted += (_, _) => ApplySyncStatsFromService();
        _itadAccountService.AccountInfoChanged += (_, _) => RefreshAccountName();
    }

    public ObservableCollection<LauncherSettingsItem> LauncherStatuses { get; }

    public IReadOnlyList<SyncInterval> IntervalOptions { get; } =
        Enum.GetValues<SyncInterval>().Cast<SyncInterval>().ToArray();

    public IReadOnlyList<AppLogLevel> LogLevelOptions { get; } =
        Enum.GetValues<AppLogLevel>().Cast<AppLogLevel>().ToArray();

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _accountName = "—";

    [ObservableProperty]
    private SyncInterval _selectedInterval;

    [ObservableProperty]
    private bool _syncOnStartup;

    [ObservableProperty]
    private bool _confirmBeforeSync;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private AppLogLevel _selectedLogLevel;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string _xboxConnectionStatus = "Not connected";

    [ObservableProperty]
    private bool _isXboxConnecting;

    [ObservableProperty]
    private string _eaConnectionStatus = "Not connected";

    [ObservableProperty]
    private bool _isEaConnecting;

    public string ConnectionStatus => IsConnected ? "Connected" : "Not connected";

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        try
        {
            await _oauthFlow.ConnectAsync();
            _profileTokenStorage.Clear();
            await _itadAccountService.RefreshAsync();
            RefreshConnectionState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Connection Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanConnect() => !IsConnected && !IsConnecting;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        _tokenStorage.Clear();
        _profileTokenStorage.Clear();
        _itadAccountService.Clear();
        RefreshConnectionState();
    }

    private bool CanDisconnect() => IsConnected;

    [RelayCommand]
    private async Task ConnectXboxAsync()
    {
        IsXboxConnecting = true;
        try
        {
            await _xboxOAuthFlow.ConnectAsync();
            RefreshXboxConnectionState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Xbox Connection Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsXboxConnecting = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectXboxAsync()
    {
        await _xboxOAuthService.ClearAsync();
        RefreshXboxConnectionState();
    }

    [RelayCommand]
    private async Task ConnectEaAsync()
    {
        IsEaConnecting = true;
        try
        {
            await _eaOAuthFlow.ConnectAsync();
            RefreshEaConnectionState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "EA Connection Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsEaConnecting = false;
        }
    }

    [RelayCommand]
    private void DisconnectEa()
    {
        _eaOAuthService.Disconnect();
        RefreshEaConnectionState();
    }

    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private async Task SyncNowAsync()
    {
        var enabledLaunchers = LauncherStatuses
            .Where(l => l.IsEnabled)
            .Select(l => l.Launcher)
            .ToList();

        var previews = LauncherStatuses.ToDictionary(
            item => item.Launcher,
            item => item.LastReadCache);

        if (!_syncConfirmation.Confirm(enabledLaunchers, previews))
            return;

        IsSyncing = true;
        try
        {
            await _syncOrchestrator.SyncAllAsync(enabledLaunchers);
        }
        catch
        {
            // Errors are shown in the sync progress window and tray notification.
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanSyncNow() => IsConnected && !IsSyncing;

    [RelayCommand]
    private async Task TestReadAsync(LauncherSettingsItem? launcher)
    {
        if (launcher is null || launcher.IsTestReadRunning)
            return;

        launcher.IsTestReadRunning = true;
        launcher.LastReadResult = string.Empty;

        try
        {
            var result = await launcher.Reader.ReadAsync();

            if (launcher.Launcher == LauncherId.Xbox && !result.IsLoggedIn &&
                await PromptConnectXboxAsync())
            {
                await ConnectXboxAsync();
                result = await launcher.Reader.ReadAsync();
            }

            if (launcher.Launcher == LauncherId.Ea &&
                result is { Owned.Count: 0, IsLoggedIn: false } &&
                await PromptConnectEaAsync())
            {
                await ConnectEaAsync();
                result = await launcher.Reader.ReadAsync();
            }

            ApplyReadResult(launcher, result);
        }
        catch (XboxAuthRequiredException)
        {
            if (launcher.Launcher == LauncherId.Xbox && await PromptConnectXboxAsync())
            {
                try
                {
                    await ConnectXboxAsync();
                    var result = await launcher.Reader.ReadAsync();
                    ApplyReadResult(launcher, result);
                    return;
                }
                catch (Exception retryEx)
                {
                    launcher.DetectionStatus = "Error";
                    launcher.LastReadResult = retryEx.Message;
                    return;
                }
            }

            launcher.DetectionStatus = "Not logged in";
            launcher.LastReadResult = "Xbox authentication is required.";
        }
        catch (Exception ex)
        {
            launcher.DetectionStatus = "Error";
            launcher.LastReadResult = ex.Message;
        }
        finally
        {
            launcher.IsTestReadRunning = false;
        }
    }

    private static void ApplyReadResult(LauncherSettingsItem launcher, LauncherReadResult result)
    {
        launcher.LastReadCache = result;
        launcher.DetectionStatus = LauncherReadResultDisplay.GetDetectionStatus(result);
        launcher.LastReadResult = LauncherReadResultDisplay.FormatScanSummary(result);
    }

    [RelayCommand]
    private async Task ViewDetailsAsync(LauncherSettingsItem? launcher)
    {
        if (launcher is null)
            return;

        if (launcher.LastReadCache is null)
            await TestReadAsync(launcher);

        if (launcher.LastReadCache is null)
            return;

        var viewModel = new LibraryPreviewViewModel(launcher.DisplayName, launcher.LastReadCache);
        var window = new LibraryPreviewWindow(viewModel)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    private static Task<bool> PromptConnectXboxAsync() =>
        Task.FromResult(
            MessageBox.Show(
                    "Connect Xbox account now?",
                    "Xbox Not Connected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes);

    private static Task<bool> PromptConnectEaAsync() =>
        Task.FromResult(
            MessageBox.Show(
                    "Connect your EA account now?",
                    "EA Not Connected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes);

    partial void OnSelectedIntervalChanged(SyncInterval value)
    {
        _settings.Interval = value;
        PersistSettings();
        _syncScheduler.Apply(_settings.ToSyncScheduleOptions());
    }

    partial void OnSyncOnStartupChanged(bool value)
    {
        _settings.SyncOnStartup = value;
        PersistSettings();
        _syncScheduler.Apply(_settings.ToSyncScheduleOptions());
    }

    partial void OnConfirmBeforeSyncChanged(bool value)
    {
        _settings.ConfirmBeforeSync = value;
        PersistSettings();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settings.StartWithWindows = value;
        PersistSettings();
        _windowsStartupService.Apply(value);
    }

    partial void OnShowNotificationsChanged(bool value)
    {
        _settings.ShowNotifications = value;
        PersistSettings();
    }

    partial void OnSelectedLogLevelChanged(AppLogLevel value)
    {
        _settings.LogLevel = value;
        PersistSettings();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ConnectionStatus));
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        SyncNowCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsConnectingChanged(bool value) => ConnectCommand.NotifyCanExecuteChanged();

    partial void OnIsSyncingChanged(bool value) => SyncNowCommand.NotifyCanExecuteChanged();

    private void SaveLauncherSettings()
    {
        _settings.EnabledLaunchers = LauncherStatuses.ToDictionary(
            item => item.Launcher,
            item => item.IsEnabled);
        PersistSettings();
        _trayIconService.RefreshContextMenu();
    }

    private void PersistSettings() => _appSettingsStorage.Save(_settings);

    private void RefreshConnectionState()
    {
        IsConnected = _tokenStorage.Load() is not null;
        RefreshAccountName();

        if (IsConnected)
            _ = _itadAccountService.RefreshAsync();
    }

    private void RefreshAccountName()
    {
        AccountName = IsConnected ? _itadAccountService.GetDisplayName() : "—";
    }

    private void RefreshXboxConnectionState()
    {
        XboxConnectionStatus = _xboxOAuthService.IsAuthenticated()
            ? _xboxOAuthService.GetGamertag() ?? "Connected"
            : "Not connected";
    }

    private void RefreshEaConnectionState()
    {
        EaConnectionStatus = _eaOAuthService.IsAuthenticated()
            ? _eaOAuthService.GetStoredSession()?.DisplayName ?? "Connected"
            : "Not connected";
    }

    private void ApplySyncStatsFromService()
    {
        foreach (var launcher in LauncherStatuses)
            launcher.LastSyncStats = _syncStatusService.GetStats(launcher.Launcher);
    }
}
