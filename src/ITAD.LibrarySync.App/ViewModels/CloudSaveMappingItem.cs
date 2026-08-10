using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

/// <summary>
/// A persisted cloud backup row in the Cloud Saves tab (localized display values).
/// </summary>
public sealed class CloudSaveMappingItem(CloudSaveMapping mapping)
{
    public CloudSaveMapping Mapping { get; } = mapping;

    public string Title => Mapping.Title;

    public string SourcePath => Mapping.SourcePath;

    public string TargetPath => Mapping.TargetPath;

    public string ProviderDisplay => LanguageManager.Instance[$"CloudProvider{Mapping.Provider}"];

    public string CreatedDisplay => Mapping.CreatedAt.ToString("g");
}
