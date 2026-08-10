using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface ICloudProviderLocator
{
    /// <summary>Providers whose local sync root folder could be resolved.</summary>
    IReadOnlyList<CloudProvider> GetAvailableProviders();

    /// <summary>Resolves the local sync root folder for a provider, or <c>null</c> when unavailable.</summary>
    string? GetCloudRoot(CloudProvider provider);
}
