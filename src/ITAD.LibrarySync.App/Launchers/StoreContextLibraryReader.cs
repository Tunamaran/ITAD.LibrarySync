using System.Windows;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using Windows.Services.Store;
using WinRT.Interop;

namespace ITAD.LibrarySync.App.Launchers;

public sealed class StoreContextLibraryReader(IWindowHandleProvider windowHandleProvider)
    : IMicrosoftStoreLibraryReader
{
    private static readonly string[] ProductKinds = ["Game", "Application"];

    internal const uint ErrorNoSuchUserHResult = 0x80070525;

    public Task<MicrosoftStoreLibraryReadResult> ReadOwnedGamesAsync(CancellationToken ct = default)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("WPF application is not running.");

        if (dispatcher.CheckAccess())
            return ReadOwnedGamesCoreAsync(ct);

        return dispatcher.InvokeAsync(() => ReadOwnedGamesCoreAsync(ct)).Task.Unwrap();
    }

    private async Task<MicrosoftStoreLibraryReadResult> ReadOwnedGamesCoreAsync(CancellationToken ct)
    {
        windowHandleProvider.EnsureInitialized();

        var context = StoreContext.GetDefault();
        InitializeWithWindow.Initialize(context, windowHandleProvider.Handle);

        var queryResult = await context.GetUserCollectionAsync(ProductKinds)
            .AsTask()
            .WaitAsync(ct);

        if (queryResult.ExtendedError is { } extendedError)
        {
            if (extendedError.HResult == ErrorNoSuchUserHResult)
                throw new InvalidOperationException("No Microsoft Store user is signed in on this PC.");

            throw new InvalidOperationException(extendedError.Message);
        }

        var games = new List<StoreGame>();
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in queryResult.Products.Values)
        {
            if (string.IsNullOrWhiteSpace(product.Title))
                continue;

            var storeId = ResolveStoreId(product);
            if (string.IsNullOrWhiteSpace(storeId) || !knownIds.Add(storeId))
                continue;

            games.Add(new StoreGame(LauncherId.Xbox, storeId, product.Title.Trim()));
        }

        return new MicrosoftStoreLibraryReadResult(games);
    }

    private static string ResolveStoreId(StoreProduct product)
    {
        if (!string.IsNullOrWhiteSpace(product.StoreId))
            return product.StoreId;

        if (!string.IsNullOrWhiteSpace(product.InAppOfferToken))
            return product.InAppOfferToken;

        return string.Empty;
    }
}
