using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.Core.Launchers;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class LauncherPlatformStatItem : ObservableObject
{
    public LauncherId Launcher { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    private int _gameCount;

    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private string _accentColor = "#0284C7";

    public LauncherPlatformStatItem(LauncherId launcher, string displayName, int gameCount, double percentage, string accentColor)
    {
        Launcher = launcher;
        DisplayName = displayName;
        GameCount = gameCount;
        Percentage = percentage;
        AccentColor = accentColor;
    }
}
