using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Services;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App.ViewModels;

/// <summary>
/// View model for the "Cloud Saves" tab. The user picks which installed games to
/// migrate: each game row shows its (auto-detected or manually set) save folder,
/// then "Migrate Selected" copies it into the cloud root and replaces the original
/// path with an NTFS junction. Restore moves everything back.
/// </summary>
public sealed partial class CloudSaveSettingsViewModel : ObservableObject
{
    /// <summary>Maximum live PCGamingWiki page lookups per detection run.</summary>
    private const int MaxLiveLookupsPerScan = 10;

    private readonly ICloudProviderLocator _locator;
    private readonly IGameSaveDiscoveryService _discovery;
    private readonly ICloudSaveOrchestrator _orchestrator;
    private readonly ICloudSaveMappingStorage _storage;
    private readonly IPcgwSaveLookupService _pcgw;
    private readonly SteamLibraryReader _steam;
    private readonly IReadOnlyList<ILauncherReader> _readers;
    private readonly AppSettingsStorage _settingsStorage;
    private readonly FileLogger _logger;
    private int _remainingLiveLookups;
    private bool _liveLookupLimitReached;
    private bool _lookingUp;

    public CloudSaveSettingsViewModel(
        ICloudProviderLocator locator,
        IGameSaveDiscoveryService discovery,
        ICloudSaveOrchestrator orchestrator,
        ICloudSaveMappingStorage storage,
        IPcgwSaveLookupService pcgw,
        SteamLibraryReader steam,
        IReadOnlyList<ILauncherReader> readers,
        AppSettingsStorage settingsStorage,
        FileLogger logger)
    {
        _locator = locator;
        _discovery = discovery;
        _orchestrator = orchestrator;
        _storage = storage;
        _pcgw = pcgw;
        _steam = steam;
        _readers = readers;
        _settingsStorage = settingsStorage;
        _logger = logger;

        UsePcgwLookup = _settingsStorage.Load().UsePcgwLiveLookup;

        foreach (var provider in _locator.GetAvailableProviders())
            Providers.Add(new CloudProviderOption(provider, _locator.GetCloudRoot(provider)!));

        // Restore the last scan's game list first — it must survive app restarts
        // and is only replaced by the next scan.
        RestoreScannedGames();

        var settings = _settingsStorage.Load();
        SelectedProvider = Providers.FirstOrDefault(candidate =>
                string.Equals(candidate.Provider.ToString(), settings.CloudSaveProvider, StringComparison.OrdinalIgnoreCase))
            ?? Providers.FirstOrDefault();
        StatusText = Providers.Count == 0 ? Lang["CloudNoProvider"] : Lang["CloudStatusReady"];
        Games.CollectionChanged += (_, _) => NotifyButtonStates();

        _ = RefreshMappingsAsync();
    }

    public LanguageManager Lang => LanguageManager.Instance;

    public ObservableCollection<CloudProviderOption> Providers { get; } = [];

    public ObservableCollection<CloudSaveGameViewModel> Games { get; } = [];

    public ObservableCollection<CloudSaveMappingItem> Mappings { get; } = [];

    [ObservableProperty]
    private CloudProviderOption? _selectedProvider;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _usePcgwLookup;

    partial void OnUsePcgwLookupChanged(bool value)
    {
        var settings = _settingsStorage.Load();
        settings.UsePcgwLiveLookup = value;
        _settingsStorage.Save(settings);
    }

    public bool IsProviderAvailable => SelectedProvider is not null;

    public bool CanDetect => !IsBusy;

    public bool CanMigrate => !IsBusy && IsProviderAvailable && Games.Any(game => game.IsSelected && game.HasSaveFolder);

    public bool CanPreview => !IsBusy && IsProviderAvailable && Games.Any(game => game.IsSelected && game.HasSaveFolder);

    public bool CanLookupSelected => !IsBusy && Games.Any(game => game.IsSelected && !game.HasSaveFolder);

    public void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(IsProviderAvailable));
        OnPropertyChanged(nameof(CanDetect));
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanPreview));
        OnPropertyChanged(nameof(CanLookupSelected));

        DetectGamesCommand.NotifyCanExecuteChanged();
        LookupSelectedCommand.NotifyCanExecuteChanged();
        MigrateCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProviderChanged(CloudProviderOption? value)
    {
        NotifyButtonStates();

        // Remember the user's choice until they change it again.
        if (value is not null)
            SaveScanState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyButtonStates();
    }

    [RelayCommand(CanExecute = nameof(CanDetect))]
    private async Task DetectGamesAsync()
    {
        IsBusy = true;
        StatusText = Lang["CloudStatusDetecting"];
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _remainingLiveLookups = MaxLiveLookupsPerScan;
            _liveLookupLimitReached = false;
            _lookingUp = false;
            Games.Clear();

            // Only INSTALLED games are scanned — owned-but-not-installed titles
            // have no local save folder and would only add noise.
            foreach (var reader in _readers)
            {
                LauncherReadResult result;
                try
                {
                    result = await reader.ReadAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"CloudSaveSettingsViewModel: launcher scan failed for {reader.Launcher} — {ex.Message}");
                    continue;
                }

                foreach (var game in result.Installed ?? result.Owned)
                    await AddGameRowAsync(game.Title, LauncherDisplayNames.Get(reader.Launcher), seen);
            }

            // Steam — supported only for Cloud Saves, never for the ITAD sync pipeline.
            try
            {
                foreach (var game in await _steam.GetInstalledGamesAsync())
                    await AddGameRowAsync(game.Title, Lang["CloudPlatformSteam"], seen);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"CloudSaveSettingsViewModel: Steam scan failed — {ex.Message}");
            }

            var summary = string.Format(Lang["CloudStatusGamesFormat"], Games.Count);
            if (_liveLookupLimitReached)
                summary += " " + Lang["CloudStatusLookupLimit"];
            StatusText = summary;
            SaveScanState();
            NotifyButtonStates();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddGameRowAsync(string title, string platform, HashSet<string> seen)
    {
        var key = GameMatcher.NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            return;

        var saveInfo = await _discovery.FindForTitleAsync(title);
        var foundViaLive = false;
        if (saveInfo is null && UsePcgwLookup)
        {
            if (_remainingLiveLookups <= 0)
            {
                _liveLookupLimitReached = true;
            }
            else
            {
                var lookup = await _pcgw.LookupAsync(title);
                if (lookup.UsedLiveRequest)
                {
                    if (!_lookingUp)
                    {
                        StatusText = Lang["CloudStatusLookingUp"];
                        _lookingUp = true;
                    }

                    _remainingLiveLookups--;
                }

                // Cache hits are free and do not consume the budget.
                saveInfo = lookup.Info;
                foundViaLive = lookup.UsedLiveRequest && saveInfo is not null;
            }
        }

        var row = new CloudSaveGameViewModel(title, saveInfo, platform)
        {
            StatusText = saveInfo is null
                ? Lang["CloudStatusNoSaveFolder"]
                : foundViaLive
                    ? Lang["CloudStatusPcgwFound"]
                    : saveInfo.Exists
                        ? Lang["CloudStatusFound"]
                        : Lang["CloudStatusMissing"],
            StatusColor = saveInfo is null
                ? "#D97706"
                : foundViaLive
                    ? "#0891B2"
                    : saveInfo.Exists
                        ? "#16A34A"
                        : "#EA580C"
        };
        Games.Add(TrackSelection(row));
    }

    /// <summary>
    /// Runs a PCGamingWiki lookup only for the games the user selected that still
    /// have no save folder — no API calls are made for anything else, so traffic
    /// stays exactly as large as the user asked for.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLookupSelected))]
    private async Task LookupSelectedAsync()
    {
        var selected = Games.Where(game => game.IsSelected && !game.HasSaveFolder).ToList();
        if (selected.Count == 0)
        {
            StatusText = Lang["CloudNoLookupSelection"];
            return;
        }

        IsBusy = true;
        try
        {
            _remainingLiveLookups = MaxLiveLookupsPerScan;
            _liveLookupLimitReached = false;
            _lookingUp = false;

            var found = 0;
            foreach (var row in selected)
            {
                if (_remainingLiveLookups <= 0)
                {
                    _liveLookupLimitReached = true;
                    break;
                }

                var lookup = await _pcgw.LookupAsync(row.Title, forceLive: true);
                if (lookup.UsedLiveRequest)
                {
                    if (!_lookingUp)
                    {
                        StatusText = Lang["CloudStatusLookingUp"];
                        _lookingUp = true;
                    }

                    _remainingLiveLookups--;
                }

                // Cache hits and negative results leave the row untouched.
                if (lookup.Info is null)
                    continue;

                row.SetSaveInfo(lookup.Info);
                row.StatusText = Lang["CloudStatusPcgwFound"];
                row.StatusColor = "#0891B2";
                found++;
            }

            var summary = string.Format(Lang["CloudStatusLookupResultFormat"], found);
            if (_liveLookupLimitReached)
                summary += " " + Lang["CloudStatusLookupLimit"];
            StatusText = summary;
            SaveScanState();
            NotifyButtonStates();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task PreviewAsync()
    {
        if (SelectedProvider is null)
            return;

        var selected = SelectedRows();
        if (selected.Count == 0)
        {
            StatusText = Lang["CloudNoSelection"];
            return;
        }

        IsBusy = true;
        try
        {
            var previews = await _orchestrator.PreviewAsync(
                SelectedProvider.Provider,
                selected.Select(row => row.GetSaveInfo()!).ToList());

            foreach (var preview in previews)
            {
                var row = selected.FirstOrDefault(candidate =>
                    string.Equals(candidate.SourcePath, preview.SourcePath, StringComparison.OrdinalIgnoreCase));
                if (row is null)
                    continue;

                row.TargetPath = preview.TargetPath;
                (row.StatusText, row.StatusColor) = FormatStatus(preview.Status);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMigrate))]
    private async Task MigrateAsync()
    {
        if (SelectedProvider is null)
            return;

        var selected = SelectedRows();
        if (selected.Count == 0)
        {
            StatusText = Lang["CloudNoSelection"];
            return;
        }

        var confirm = MessageBox.Show(
            string.Format(Lang["CloudConfirmMigrateText"], selected.Count, SelectedProvider.DisplayName),
            Lang["CloudConfirmMigrateTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        StatusText = Lang["CloudStatusMigrating"];
        try
        {
            var results = await _orchestrator.MigrateAsync(
                SelectedProvider.Provider,
                selected.Select(row => row.GetSaveInfo()!).ToList());

            ApplyResults(results);
            SaveScanState();
            await RefreshMappingsAsync();
            StatusText = string.Format(
                Lang["CloudStatusMigratedFormat"],
                results.Count(result => result.Success),
                results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CloudSaveSettingsViewModel: migration failed — {ex.Message}");
            StatusText = $"{Lang["CloudStatusFailed"]}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var game in Games)
            game.IsSelected = true;

        NotifyButtonStates();
        SaveScanState();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var game in Games)
            game.IsSelected = false;

        NotifyButtonStates();
        SaveScanState();
    }

    /// <summary>Lets the user pick a save folder for a game that has none.</summary>
    [RelayCommand]
    private void SetSaveFolder(CloudSaveGameViewModel? row)
    {
        if (row is null)
            return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Lang["CloudBrowseTitle"],
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        var info = _discovery.CreateManual(row.Title, dialog.FolderName);
        row.SetSaveInfo(info);
        row.StatusText = info.Exists ? Lang["CloudStatusFound"] : Lang["CloudStatusMissing"];
        row.StatusColor = info.Exists ? "#16A34A" : "#EA580C";
        SaveScanState();
        NotifyButtonStates();
    }

    [RelayCommand]
    private async Task RestoreAsync(CloudSaveMappingItem? item)
    {
        if (item is null)
            return;

        var confirm = MessageBox.Show(
            string.Format(Lang["CloudConfirmRestoreText"], item.Title),
            Lang["CloudConfirmRestoreTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        try
        {
            var result = await _orchestrator.RestoreAsync(item.Mapping);
            StatusText = result.Success
                ? Lang["CloudStatusRestored"]
                : $"{Lang["CloudStatusFailed"]}: {result.Message}";
            await RefreshMappingsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveMappingAsync(CloudSaveMappingItem? item)
    {
        if (item is null)
            return;

        try
        {
            await _storage.RemoveAsync(item.Mapping.SourcePath);
            await RefreshMappingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CloudSaveSettingsViewModel: failed to remove mapping — {ex.Message}");
            StatusText = $"{Lang["CloudStatusFailed"]}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshMappingsAsync()
    {
        var mappings = await _storage.GetAllAsync();
        Mappings.Clear();
        foreach (var mapping in mappings.Where(item => item.IsActive))
            Mappings.Add(new CloudSaveMappingItem(mapping));
    }

    /// <summary>
    /// Re-creates the game rows of the last scan from settings so the list stays
    /// visible until the next scan replaces it.
    /// </summary>
    private void RestoreScannedGames()
    {
        var entries = _settingsStorage.Load().CloudScannedGames;
        foreach (var entry in entries)
        {
            var saveInfo = string.IsNullOrWhiteSpace(entry.SourcePath)
                ? null
                : _discovery.CreateManual(entry.Title, entry.SourcePath);

            var row = new CloudSaveGameViewModel(entry.Title, saveInfo, entry.Platform)
            {
                IsSelected = entry.IsSelected,
                StatusText = entry.StatusText,
                StatusColor = entry.StatusColor
            };
            Games.Add(TrackSelection(row));
        }

        NotifyButtonStates();
    }

    /// <summary>
    /// Persists the current game list and provider selection so both survive
    /// app restarts. The list is only replaced by the next scan.
    /// </summary>
    private void SaveScanState()
    {
        var settings = _settingsStorage.Load();
        settings.CloudScannedGames = Games.Select(game => new CloudScannedGameEntry
        {
            Title = game.Title,
            Platform = game.Platform,
            SourcePath = game.SourcePath,
            IsSelected = game.IsSelected,
            StatusText = game.StatusText,
            StatusColor = game.StatusColor
        }).ToList();
        if (SelectedProvider is not null)
            settings.CloudSaveProvider = SelectedProvider.Provider.ToString();
        _settingsStorage.Save(settings);
    }

    private List<CloudSaveGameViewModel> SelectedRows() =>
        Games.Where(game => game.IsSelected && game.HasSaveFolder).ToList();

    private CloudSaveGameViewModel TrackSelection(CloudSaveGameViewModel item)
    {
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CloudSaveGameViewModel.IsSelected) ||
                args.PropertyName == nameof(CloudSaveGameViewModel.HasSaveFolder) ||
                args.PropertyName == nameof(CloudSaveGameViewModel.SourcePath))
            {
                NotifyButtonStates();
                SaveScanState();
            }
        };
        return item;
    }

    private void ApplyResults(IReadOnlyList<CloudSaveResult> results)
    {
        foreach (var result in results)
        {
            var row = Games.FirstOrDefault(candidate =>
                string.Equals(candidate.SourcePath, result.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (row is null)
                continue;

            row.StatusText = result.Success ? Lang["CloudStatusMigrated"] : $"{Lang["CloudStatusFailed"]}: {result.Message}";
            row.StatusColor = result.Success ? "#16A34A" : "#DC2626";
        }
    }

    private (string Text, string Color) FormatStatus(CloudSaveStatus status) => status switch
    {
        CloudSaveStatus.Ready => (Lang["CloudStatusReady"], "#16A34A"),
        CloudSaveStatus.AlreadyMigrated => (Lang["CloudStatusAlreadyMigrated"], "#0284C7"),
        CloudSaveStatus.SourceMissing => (Lang["CloudStatusMissing"], "#EA580C"),
        CloudSaveStatus.OrphanJunction => (Lang["CloudStatusOrphanJunction"], "#D97706"),
        CloudSaveStatus.StaleMapping => (Lang["CloudStatusStaleMapping"], "#D97706"),
        CloudSaveStatus.TargetConflict => (Lang["CloudStatusConflict"], "#DC2626"),
        CloudSaveStatus.Unavailable => (Lang["CloudStatusUnavailable"], "#DC2626"),
        _ => (status.ToString(), "#64748B")
    };
}
