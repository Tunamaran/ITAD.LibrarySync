using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

/// <summary>
/// A cloud provider entry shown in the provider ComboBox.
/// </summary>
public sealed class CloudProviderOption(CloudProvider provider, string root)
{
    public CloudProvider Provider { get; } = provider;

    public string Root { get; } = root;

    public string DisplayName => LanguageManager.Instance[$"CloudProvider{Provider}"];

    public override string ToString() => DisplayName;
}
