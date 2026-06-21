namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public interface IMicrosoftStoreCatalogClient
{
    Task<IReadOnlyDictionary<string, string>> ResolveStoreIdsByPfnAsync(
        IReadOnlyList<string> pfns,
        CancellationToken ct);
}
