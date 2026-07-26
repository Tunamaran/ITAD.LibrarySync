using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.ViewModels;

public enum WizardStep
{
    Welcome,
    ItadOAuth,
    LauncherScan,
    FirstSync
}

public sealed partial class FirstRunWizardViewModel : ObservableObject
{
    private readonly OAuthFlowService _oauthFlow;
    private readonly TokenStorage _tokenStorage;
    private readonly AppSettingsStorage _appSettingsStorage;
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly ItadAccountService _itadAccountService;
    private readonly AppSettings _settings;
    private bool _hasScannedLaunchers;

    public FirstRunWizardViewModel(
        OAuthFlowService oauthFlow,
        TokenStorage tokenStorage,
        AppSettingsStorage appSettingsStorage,
        ISyncOrchestrator syncOrchestrator,
        ItadAccountService itadAccountService,
        IReadOnlyList<Core.Launchers.ILauncherReader> readers)
    {
        _oauthFlow = oauthFlow;
        _tokenStorage = tokenStorage;
        _appSettingsStorage = appSettingsStorage;
        _syncOrchestrator = syncOrchestrator;
        _itadAccountService = itadAccountService;
        _settings = appSettingsStorage.Load();

        LauncherStatuses = new ObservableCollection<LauncherSettingsItem>(
            readers
                .OrderBy(r => r.Launcher)
                .Select(r => new LauncherSettingsItem(r, _settings.IsLauncherEnabled(r.Launcher))));

        RefreshConnectionState();
        UpdateNavigationState();
        _itadAccountService.AccountInfoChanged += (_, _) => RefreshAccountName();
    }

    public ObservableCollection<LauncherSettingsItem> LauncherStatuses { get; }

    public bool IsCompleted { get; private set; }

    [ObservableProperty]
    private WizardStep _currentStep = WizardStep.Welcome;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _accountName = "—";

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isScanningLaunchers;

    [ObservableProperty]
    private bool _runSyncOnFinish;

    [ObservableProperty]
    private bool _isFinishing;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private bool _isLastStep;

    public LanguageManager Lang => LanguageManager.Instance;

    public string ConnectionStatus => IsConnected ? Lang["WizardConnected"] : Lang["WizardNotConnected"];

    public int StepNumber => (int)CurrentStep + 1;

    public string StepTitle => CurrentStep switch
    {
        WizardStep.Welcome => Lang["WizardWelcomeTitle"],
        WizardStep.ItadOAuth => Lang["WizardItadTitle"],
        WizardStep.LauncherScan => Lang["WizardPlatformTitle"],
        WizardStep.FirstSync => Lang["WizardReadyTitle"],
        _ => string.Empty
    };

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        try
        {
            await _oauthFlow.ConnectAsync();
            RefreshConnectionState();
            UpdateNavigationState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                Lang["WizardConnectionFailed"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanConnect() => !IsConnected && !IsConnecting;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (CurrentStep > WizardStep.Welcome)
            CurrentStep--;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext()
    {
        if (CurrentStep < WizardStep.FirstSync)
            CurrentStep++;
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task FinishAsync()
    {
        IsFinishing = true;
        try
        {
            _settings.HasCompletedFirstRun = true;
            _settings.EnabledLaunchers = LauncherStatuses.ToDictionary(
                item => item.Launcher,
                item => item.IsEnabled);
            _appSettingsStorage.Save(_settings);

            var initialSyncRan = false;
            if (RunSyncOnFinish)
            {
                await _syncOrchestrator.SyncAllAsync();
                initialSyncRan = true;
            }

            IsCompleted = true;
            WizardCompleted?.Invoke(this, new WizardCompletedEventArgs { InitialSyncRan = initialSyncRan });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                Lang["WizardSetupFailed"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsFinishing = false;
        }
    }

    private bool CanFinish() => IsLastStep && !IsFinishing;

    public event EventHandler<WizardCompletedEventArgs>? WizardCompleted;

    partial void OnCurrentStepChanged(WizardStep value)
    {
        OnPropertyChanged(nameof(StepNumber));
        OnPropertyChanged(nameof(StepTitle));
        UpdateNavigationState();

        if (value == WizardStep.LauncherScan && !_hasScannedLaunchers)
            _ = ScanAllLaunchersAsync();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ConnectionStatus));
        ConnectCommand.NotifyCanExecuteChanged();
        UpdateNavigationState();
    }

    partial void OnIsConnectingChanged(bool value) => ConnectCommand.NotifyCanExecuteChanged();

    partial void OnIsFinishingChanged(bool value)
    {
        FinishCommand.NotifyCanExecuteChanged();
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        CanGoBack = CurrentStep > WizardStep.Welcome && !IsFinishing;
        IsLastStep = CurrentStep == WizardStep.FirstSync;
        CanGoNext = CurrentStep switch
        {
            WizardStep.Welcome => true,
            WizardStep.ItadOAuth => IsConnected,
            WizardStep.LauncherScan => !IsScanningLaunchers,
            _ => false
        };

        GoBackCommand.NotifyCanExecuteChanged();
        GoNextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    private async Task ScanAllLaunchersAsync()
    {
        IsScanningLaunchers = true;
        UpdateNavigationState();

        try
        {
            foreach (var launcher in LauncherStatuses)
                await TestReadLauncherAsync(launcher);

            _hasScannedLaunchers = true;
        }
        finally
        {
            IsScanningLaunchers = false;
            UpdateNavigationState();
        }
    }

    private static async Task TestReadLauncherAsync(LauncherSettingsItem launcher)
    {
        if (launcher.IsTestReadRunning)
            return;

        launcher.IsTestReadRunning = true;
        launcher.LastReadResult = string.Empty;

        try
        {
            var result = await launcher.Reader.ReadAsync();
            launcher.LastReadCache = result;
            launcher.DetectionStatus = LauncherReadResultDisplay.GetDetectionStatus(result);
            launcher.LastReadResult = LauncherReadResultDisplay.FormatScanSummary(result);
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
}
