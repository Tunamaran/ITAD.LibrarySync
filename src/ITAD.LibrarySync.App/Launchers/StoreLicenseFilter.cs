using System.Windows;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Launchers.Xbox;
using ITAD.LibrarySync.Core.Models;
using Windows.Services.Store;
using WinRT.Interop;

namespace ITAD.LibrarySync.App.Launchers;

public sealed class StoreLicenseFilter(
    IWindowHandleProvider windowHandleProvider,
    DisplayCatalogClient displayCatalog)
{
    private static readonly string[] ProductKinds = ["Game", "Application"];
    private const int StoreIdBatchSize = 50;

    public Task<IReadOnlyList<StoreGame>> FilterToCurrentlyOwnedAsync(
        IReadOnlyList<StoreGame> candidates,
        CancellationToken ct = default)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("WPF application is not running.");

        if (dispatcher.CheckAccess())
            return FilterCoreAsync(candidates, ct);

        return dispatcher.InvokeAsync(() => FilterCoreAsync(candidates, ct)).Task.Unwrap();
    }

    private async Task<IReadOnlyList<StoreGame>> FilterCoreAsync(
        IReadOnlyList<StoreGame> candidates,
        CancellationToken ct)
    {
        if (candidates.Count == 0)
            return candidates;

        var pfnCandidates = candidates
            .Where(game => LooksLikePackageFamilyName(game.StoreId))
            .ToList();

        if (pfnCandidates.Count == 0)
            return [];

        var pfnToStoreId = await displayCatalog.ResolveStoreIdsByPfnAsync(
            pfnCandidates.Select(game => game.StoreId).ToList(),
            ct);

        if (pfnToStoreId.Count == 0)
            return [];

        windowHandleProvider.EnsureInitialized();

        var context = StoreContext.GetDefault();
        InitializeWithWindow.Initialize(context, windowHandleProvider.Handle);

        var ownedStoreIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in pfnToStoreId.Values.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(StoreIdBatchSize))
        {
            var queryResult = await context.GetStoreProductsAsync(ProductKinds, chunk)
                .AsTask()
                .WaitAsync(ct);

            if (queryResult.ExtendedError is not null)
                return [];

            foreach (var product in queryResult.Products.Values)
            {
                if (!product.IsInUserCollection || string.IsNullOrWhiteSpace(product.StoreId))
                    continue;

                ownedStoreIds.Add(product.StoreId);
            }
        }

        if (ownedStoreIds.Count == 0)
            return [];

        var verified = new List<StoreGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in pfnCandidates)
        {
            if (!pfnToStoreId.TryGetValue(candidate.StoreId, out var storeId))
                continue;

            if (!ownedStoreIds.Contains(storeId) || !seen.Add(storeId))
                continue;

            verified.Add(candidate with { StoreId = storeId });
        }

        return verified;
    }

    private static bool LooksLikePackageFamilyName(string value) =>
        value.Contains('_', StringComparison.Ordinal) &&
        !value.StartsWith("9", StringComparison.OrdinalIgnoreCase);
}
