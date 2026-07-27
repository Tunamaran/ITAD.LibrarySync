using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Services;
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
    private readonly IUnmatchedTitlesService _unmatchedTitlesService;
    private readonly IUpdateCheckerService _updateCheckerService;
    private readonly ICustomMappingService _customMappingService;
    private readonly ILogReaderService _logReaderService;
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
        IReadOnlyList<ILauncherReader> readers,
        IUnmatchedTitlesService unmatchedTitlesService,
        IUpdateCheckerService updateCheckerService,
        ICustomMappingService customMappingService,
        ILogReaderService logReaderService)
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
        _unmatchedTitlesService = unmatchedTitlesService;
        _updateCheckerService = updateCheckerService;
        _customMappingService = customMappingService;
        _logReaderService = logReaderService;
        _settings = appSettingsStorage.Load();
        LanguageManager.Instance.Initialize(appSettingsStorage);
        _selectedLanguage = LanguageOptions.FirstOrDefault(l => l.Code == _settings.Language) ?? LanguageOptions[0];

        VersionInfo = $"v{UpdateCheckerService.GetCurrentAssemblyVersion()}";
        UpdateStatusText = Lang["VersionUpToDate"];

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
        _itadAccountService.AccountInfoChanged += (_, _) => Application.Current?.Dispatcher?.Invoke(RefreshAccountName);
        _ = LoadUnmatchedTitlesAsync();
        _ = LoadCustomMappingsAsync();
        _ = RefreshLogsAsync();
        RefreshInsights();
    }

    public ObservableCollection<LauncherSettingsItem> LauncherStatuses { get; }

    public ObservableCollection<UnmatchedTitle> UnmatchedTitles { get; } = [];

    public ObservableCollection<UnmatchedTitle> FilteredUnmatchedTitles { get; } = [];

    public ObservableCollection<CustomGameMapping> CustomMappings { get; } = [];

    public ObservableCollection<LogEntry> Logs { get; } = [];

    public ObservableCollection<LogEntry> FilteredLogs { get; } = [];

    public IReadOnlyList<SyncInterval> IntervalOptions { get; } =
        Enum.GetValues<SyncInterval>().Cast<SyncInterval>().ToArray();

    public IReadOnlyList<AppLogLevel> LogLevelOptions { get; } =
        Enum.GetValues<AppLogLevel>().Cast<AppLogLevel>().ToArray();

    public IReadOnlyList<string> LogFilterOptions { get; } = ["ALL", "INFO", "ERROR"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatus))]
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

    public LanguageManager Lang => LanguageManager.Instance;

    public IReadOnlyList<LanguageOption> LanguageOptions => LanguageManager.AvailableLanguages;

    public string ConnectionStatusText => IsConnected ? Lang["StatusConnected"] : Lang["StatusNotConnected"];
    public string DisplayVersionText => $"{Lang["VersionPrefix"]} v{UpdateCheckerService.GetCurrentAssemblyVersion()}";
    public string DisplayCurrentVersionText => $"{Lang["VersionPrefix"]}: v{UpdateCheckerService.GetCurrentAssemblyVersion()}";

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value is null) return;
        _settings.Language = value.Code;
        PersistSettings();
        LanguageManager.Instance.CurrentLanguage = value.Code;
        OnPropertyChanged(nameof(ConnectionStatus));
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(DisplayVersionText));
        OnPropertyChanged(nameof(DisplayCurrentVersionText));
        UpdateStatusText = Lang["VersionUpToDate"];
    }

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

    [ObservableProperty]
    private string _versionInfo = string.Empty;

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateAvailable))]
    private string _downloadUrl = string.Empty;

    public bool HasUpdateAvailable => !string.IsNullOrWhiteSpace(DownloadUrl);

    [ObservableProperty]
    private int _totalSyncedGamesCount;

    [ObservableProperty]
    private double _matchRatePercentage = 100.0;

    [ObservableProperty]
    private int _enabledLaunchersCount;

    [ObservableProperty]
    private string _logSearchText = string.Empty;

    [ObservableProperty]
    private string _selectedLogFilter = "ALL";

    public bool CanCheckUpdates => !IsCheckingUpdates;

    partial void OnIsCheckingUpdatesChanged(bool value) => OnPropertyChanged(nameof(CanCheckUpdates));

    [ObservableProperty]
    private string _unmatchedSearchText = string.Empty;

    public string ConnectionStatus => IsConnected ? Lang["StatusConnected"] : Lang["StatusNotConnected"];

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
                    LanguageManager.Instance["VMXboxConnectPrompt"],
                    LanguageManager.Instance["VMXboxConnectTitle"],
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes);

    private static Task<bool> PromptConnectEaAsync() =>
        Task.FromResult(
            MessageBox.Show(
                    LanguageManager.Instance["VMEaConnectPrompt"],
                    LanguageManager.Instance["VMEaConnectTitle"],
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
        {
            _ = Task.Run(async () =>
            {
                await _itadAccountService.RefreshAsync();
                Application.Current?.Dispatcher?.Invoke(RefreshAccountName);
            });
        }
    }

    private void RefreshAccountName()
    {
        AccountName = IsConnected ? _itadAccountService.GetDisplayName() : "—";
        OnPropertyChanged(nameof(ConnectionStatus));
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

    partial void OnUnmatchedSearchTextChanged(string value) => ApplyUnmatchedFilter();

    [RelayCommand]
    public async Task LoadUnmatchedTitlesAsync()
    {
        var items = await _unmatchedTitlesService.GetAllAsync();
        var mappings = await _customMappingService.GetAllAsync();

        UnmatchedTitles.Clear();
        foreach (var item in items)
        {
            var isMapped = mappings.Any(m =>
                m.Launcher == item.Launcher && (
                    string.Equals(m.StoreId, item.StoreId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.Title, item.Title, StringComparison.OrdinalIgnoreCase)));

            if (!isMapped)
            {
                UnmatchedTitles.Add(item);
            }
        }
        ApplyUnmatchedFilter();
    }

    [RelayCommand]
    public async Task ClearUnmatchedTitlesAsync()
    {
        if (MessageBox.Show(
                Lang["VMClearUnmatchedConfirm"],
                Lang["VMClearUnmatchedTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _unmatchedTitlesService.ClearAsync();
            await LoadUnmatchedTitlesAsync();
        }
    }

    [RelayCommand]
    public async Task FixMatchAsync(UnmatchedTitle? title)
    {
        if (title is null) return;

        var vm = new FixMatchViewModel(_customMappingService, _unmatchedTitlesService, title);
        var window = new FixMatchWindow(vm)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await LoadCustomMappingsAsync();
            await LoadUnmatchedTitlesAsync();
            RefreshInsights();
        }
    }

    [RelayCommand]
    public async Task LoadCustomMappingsAsync()
    {
        var list = await _customMappingService.GetAllAsync();
        CustomMappings.Clear();
        foreach (var item in list)
        {
            CustomMappings.Add(item);
        }
    }

    [RelayCommand]
    public async Task RemoveCustomMappingAsync(CustomGameMapping? mapping)
    {
        if (mapping is null) return;
        await _customMappingService.RemoveMappingAsync(mapping.Launcher, mapping.StoreId);
        await LoadCustomMappingsAsync();
        await LoadUnmatchedTitlesAsync();
        RefreshInsights();
    }

    [RelayCommand]
    public async Task SyncCustomMappingsAsync()
    {
        if (CustomMappings.Count == 0)
        {
            MessageBox.Show(
                Lang["VMSyncCustomMappingsNone"],
                Lang["VMSyncCustomMappingsTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var count = await _syncOrchestrator.SyncCustomMappingsAsync();
            MessageBox.Show(
                string.Format(Lang["VMSyncCustomMappingsSuccess"], count),
                Lang["VMSyncCustomMappingsTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await LoadCustomMappingsAsync();
            await LoadUnmatchedTitlesAsync();
            RefreshInsights();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                Lang["VMSyncCustomMappingsTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatusText = Lang["VMUpdateChecking"];
        DownloadUrl = string.Empty;

        try
        {
            var result = await _updateCheckerService.CheckForUpdatesAsync();
            if (result.HasUpdate)
            {
                DownloadUrl = result.DownloadUrl;
                UpdateStatusText = string.Format(Lang["VMUpdateAvailable"], result.LatestVersion);
                if (MessageBox.Show(
                        string.Format(Lang["VMUpdateAvailablePrompt"], result.LatestVersion),
                        Lang["VMUpdateAvailableTitle"],
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    await DownloadAndApplyUpdateAsync();
                }
            }
            else
            {
                DownloadUrl = string.Empty;
                UpdateStatusText = string.Format(Lang["VMUpToDate"], result.CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            DownloadUrl = string.Empty;
            UpdateStatusText = string.Format(Lang["VMUpdateCheckFailed"], ex.Message);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    public async Task DownloadAndApplyUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(DownloadUrl))
        {
            await CheckForUpdatesAsync();
            if (string.IsNullOrWhiteSpace(DownloadUrl)) return;
        }

        IsDownloadingUpdate = true;
        DownloadProgress = 0;
        UpdateStatusText = Lang["VMUpdateDownloading"];

        try
        {
            var progress = new Progress<double>(p => DownloadProgress = p);
            var downloadedFile = await _updateCheckerService.DownloadUpdateAsync(DownloadUrl, progress);
            UpdateStatusText = Lang["VMUpdateComplete"];
            _updateCheckerService.ApplyUpdateAndRestart(downloadedFile);
        }
        catch (Exception ex)
        {
            UpdateStatusText = string.Format(Lang["VMUpdateDownloadFailed"], ex.Message);
            IsDownloadingUpdate = false;
        }
    }

    [RelayCommand]
    public async Task RefreshLogsAsync()
    {
        var entries = await _logReaderService.GetRecentLogsAsync();
        Logs.Clear();
        foreach (var entry in entries)
        {
            Logs.Add(entry);
        }
        ApplyLogFilter();
    }

    [RelayCommand]
    public void OpenLogFolder()
    {
        var logsDir = FileLogger.LogsDirectory;
        Directory.CreateDirectory(logsDir);
        Process.Start(new ProcessStartInfo { FileName = logsDir, UseShellExecute = true });
    }

    partial void OnLogSearchTextChanged(string value) => ApplyLogFilter();
    partial void OnSelectedLogFilterChanged(string value) => ApplyLogFilter();

    private void ApplyLogFilter()
    {
        FilteredLogs.Clear();
        var query = LogSearchText.Trim();
        var filterLevel = SelectedLogFilter;

        var matches = Logs.Where(l =>
        {
            if (filterLevel != "ALL" && !l.Level.Equals(filterLevel, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(query) &&
                !l.Message.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        });

        foreach (var match in matches)
        {
            FilteredLogs.Add(match);
        }
    }

    public void RefreshInsights()
    {
        EnabledLaunchersCount = LauncherStatuses.Count(l => l.IsEnabled);

        var totalRead = LauncherStatuses
            .Where(l => l.IsEnabled && l.LastReadCache is not null)
            .Sum(l => l.LastReadCache!.Owned.Count);

        TotalSyncedGamesCount = totalRead;

        var unmatchedCount = UnmatchedTitles.Count;
        if (totalRead > 0)
        {
            var matched = Math.Max(0, totalRead - unmatchedCount);
            MatchRatePercentage = Math.Round((double)matched / totalRead * 100.0, 1);
        }
        else
        {
            MatchRatePercentage = 100.0;
        }
    }

    private void ApplyUnmatchedFilter()
    {
        FilteredUnmatchedTitles.Clear();
        var query = UnmatchedSearchText.Trim();
        var matches = string.IsNullOrWhiteSpace(query)
            ? UnmatchedTitles
            : UnmatchedTitles.Where(u =>
                u.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.StoreId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Launcher.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Reason.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var match in matches)
        {
            FilteredUnmatchedTitles.Add(match);
        }
    }
}
