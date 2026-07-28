namespace ITAD.LibrarySync.App.ViewModels;

public sealed record DuplicateGameItem(
    string Title,
    string PlatformsList,
    string StoreIdsList,
    int PlatformCount);
