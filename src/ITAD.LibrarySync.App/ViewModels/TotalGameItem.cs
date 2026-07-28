using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed record TotalGameItem(
    string Title,
    string StoreId,
    LauncherId Launcher,
    string DisplayPlatform);
