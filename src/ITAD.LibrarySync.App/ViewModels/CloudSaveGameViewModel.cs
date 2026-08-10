using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

/// <summary>
/// An installed game row in the Cloud Saves tab. The user decides which games
/// to migrate; each game either has an auto-detected save folder or the user
/// sets one manually.
/// </summary>
public sealed partial class CloudSaveGameViewModel : ObservableObject
{
    private GameSaveInfo? _saveInfo;

    public CloudSaveGameViewModel(StoreGame game, GameSaveInfo? saveInfo, string platform)
    {
        Game = game;
        Title = game.Title;
        Platform = platform;
        SetSaveInfo(saveInfo);
    }

    public StoreGame Game { get; }

    public string Title { get; }

    public string Platform { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _statusColor = "#64748B";

    /// <summary>True when a save folder is known (auto-detected or user-set).</summary>
    public bool HasSaveFolder => _saveInfo is not null;

    /// <summary>True when the user still needs to pick a save folder (none detected, or the detected one does not exist).</summary>
    public bool CanSetFolder => _saveInfo is null || !_saveInfo.Exists;

    public GameSaveInfo? GetSaveInfo() => _saveInfo;

    public void SetSaveInfo(GameSaveInfo? info)
    {
        _saveInfo = info;
        SourcePath = info?.SourcePath ?? string.Empty;
        TargetPath = string.Empty;
        OnPropertyChanged(nameof(HasSaveFolder));
        OnPropertyChanged(nameof(CanSetFolder));
    }
}
