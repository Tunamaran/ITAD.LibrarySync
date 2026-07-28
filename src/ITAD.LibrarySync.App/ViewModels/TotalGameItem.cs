using ITAD.LibrarySync.Core.Launchers;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed record TotalGameItem(
    string Title,
    string StoreId,
    LauncherId Launcher,
    string DisplayPlatform);
