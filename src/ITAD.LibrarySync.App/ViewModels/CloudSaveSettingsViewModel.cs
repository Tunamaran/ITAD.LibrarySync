using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
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
    private readonly ICloudProviderLocator _locator;
    private readonly IGameSaveDiscoveryService _discovery;
    private readonly ICloudSaveOrchestrator _orchestrator;
    private readonly ICloudSaveMappingStorage _storage;
    private readonly IReadOnlyList<ILauncherReader> _readers;
    private readonly FileLogger _logger;

    public CloudSaveSettingsViewModel(
        ICloudProviderLocator locator,
        IGameSaveDiscoveryService discovery,
        ICloudSaveOrchestrator orchestrator,
        ICloudSaveMappingStorage storage,
        IReadOnlyList<ILauncherReader> readers,
        FileLogger logger)
    {
        _locator = locator;
        _discovery = discovery;
        _orchestrator = orchestrator;
        _storage = storage;
        _readers = readers;
        _logger = logger;

        foreach (var provider in _locator.GetAvailableProviders())
            Providers.Add(new CloudProviderOption(provider, _locator.GetCloudRoot(provider)!));

        SelectedProvider = Providers.FirstOrDefault();
        StatusText = Providers.Count == 0 ? Lang["CloudNoProvider"] : Lang["CloudStatusReady"];

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

    public bool IsProviderAvailable => SelectedProvider is not null;

    public bool CanDetect => !IsBusy;

    public bool CanMigrate => !IsBusy && IsProviderAvailable && Games.Any(game => game.IsSelected && game.HasSaveFolder);

    public bool CanPreview => !IsBusy && IsProviderAvailable && Games.Any(game => game.IsSelected && game.HasSaveFolder);

    partial void OnSelectedProviderChanged(CloudProviderOption? value)
    {
        OnPropertyChanged(nameof(IsProviderAvailable));
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanPreview));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDetect));
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanPreview));
    }

    [RelayCommand]
    private async Task DetectGamesAsync()
    {
        IsBusy = true;
        StatusText = Lang["CloudStatusDetecting"];
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Games.Clear();

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

                foreach (var game in result.Owned)
                {
                    var key = GameMatcher.NormalizeTitle(game.Title);
                    if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                        continue;

                    var saveInfo = await _discovery.FindForTitleAsync(game.Title);
                    var row = new CloudSaveGameViewModel(game, saveInfo, LauncherDisplayNames.Get(reader.Launcher))
                    {
                        StatusText = saveInfo is null
                            ? Lang["CloudStatusNoSaveFolder"]
                            : saveInfo.Exists
                                ? Lang["CloudStatusFound"]
                                : Lang["CloudStatusMissing"],
                        StatusColor = saveInfo is null ? "#D97706" : saveInfo.Exists ? "#16A34A" : "#EA580C"
                    };
                    Games.Add(TrackSelection(row));
                }
            }

            StatusText = string.Format(Lang["CloudStatusGamesFormat"], Games.Count);
            OnPropertyChanged(nameof(CanMigrate));
            OnPropertyChanged(nameof(CanPreview));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
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

    [RelayCommand]
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
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var game in Games)
            game.IsSelected = false;
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
        OnPropertyChanged(nameof(CanMigrate));
        OnPropertyChanged(nameof(CanPreview));
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

    private List<CloudSaveGameViewModel> SelectedRows() =>
        Games.Where(game => game.IsSelected && game.HasSaveFolder).ToList();

    private CloudSaveGameViewModel TrackSelection(CloudSaveGameViewModel item)
    {
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CloudSaveGameViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(CanMigrate));
                OnPropertyChanged(nameof(CanPreview));
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
