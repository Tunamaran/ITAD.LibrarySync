using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class LauncherSettingsItem : ObservableObject
{
    public LauncherSettingsItem(ILauncherReader reader, bool isEnabled)
    {
        Reader = reader;
        Launcher = reader.Launcher;
        DisplayName = Launcher switch
        {
            LauncherId.Epic => "Epic Games Store",
            LauncherId.Ubisoft => "Ubisoft Connect",
            LauncherId.BattleNet => "Battle.net",
            LauncherId.Xbox => "Xbox / Microsoft Store",
            _ => reader.Launcher.ToString()
        };
        _isEnabled = isEnabled;
    }

    public ILauncherReader Reader { get; }

    public LauncherId Launcher { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _detectionStatus = "Not checked";

    [ObservableProperty]
    private string _lastSyncStats = "—";

    [ObservableProperty]
    private string _lastReadResult = string.Empty;

    [ObservableProperty]
    private bool _isTestReadRunning;
}
