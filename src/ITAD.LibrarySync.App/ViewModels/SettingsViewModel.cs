using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly OAuthFlowService _oauthFlow;
    private readonly TokenStorage _tokenStorage;
    private readonly AppSettingsStorage _appSettingsStorage;
    private readonly SyncScheduler _syncScheduler;
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly AppSettings _settings;

    public SettingsViewModel(
        OAuthFlowService oauthFlow,
        TokenStorage tokenStorage,
        AppSettingsStorage appSettingsStorage,
        SyncScheduler syncScheduler,
        ISyncOrchestrator syncOrchestrator,
        IReadOnlyList<ILauncherReader> readers)
    {
        _oauthFlow = oauthFlow;
        _tokenStorage = tokenStorage;
        _appSettingsStorage = appSettingsStorage;
        _syncScheduler = syncScheduler;
        _syncOrchestrator = syncOrchestrator;
        _settings = appSettingsStorage.Load();

        SelectedInterval = _settings.Interval;
        SyncOnStartup = _settings.SyncOnStartup;
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
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private AppLogLevel _selectedLogLevel;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isSyncing;

    public string ConnectionStatus => IsConnected ? "Connected" : "Not connected";

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        try
        {
            await _oauthFlow.ConnectAsync();
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
        RefreshConnectionState();
    }

    private bool CanDisconnect() => IsConnected;

    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private async Task SyncNowAsync()
    {
        IsSyncing = true;
        try
        {
            var enabledLaunchers = LauncherStatuses
                .Where(l => l.IsEnabled)
                .Select(l => l.Launcher)
                .ToList();

            var results = await _syncOrchestrator.SyncAllAsync(enabledLaunchers);
            UpdateLauncherSyncStats(results);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Sync Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
            launcher.DetectionStatus = result switch
            {
                { IsDetected: false } => "Not detected",
                { IsLoggedIn: false } => "Not logged in",
                { Error: not null } => "Error",
                _ => "Ready"
            };

            var total = result.Owned.Count + result.Wishlist.Count;
            launcher.LastReadResult = result.Error is null
                ? $"{total} games ({result.Owned.Count} owned, {result.Wishlist.Count} wishlist)"
                : result.Error;
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

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settings.StartWithWindows = value;
        PersistSettings();
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
    }

    private void PersistSettings() => _appSettingsStorage.Save(_settings);

    private void RefreshConnectionState()
    {
        IsConnected = _tokenStorage.Load() is not null;
        AccountName = IsConnected ? "ITAD Account" : "—";
    }

    private void UpdateLauncherSyncStats(IReadOnlyList<SyncResult> results)
    {
        foreach (var launcher in LauncherStatuses)
        {
            var result = results.FirstOrDefault(r => r.Launcher == launcher.Launcher);
            launcher.LastSyncStats = result switch
            {
                null => "Skipped",
                { Success: false } => $"Failed: {result.Error ?? "Unknown error"}",
                _ => $"Collection {result.CollectionTotal} (+{result.CollectionAdded}/-{result.CollectionRemoved}), " +
                     $"Waitlist {result.WaitlistTotal} (+{result.WaitlistAdded}/-{result.WaitlistRemoved})"
            };
        }
    }
}
