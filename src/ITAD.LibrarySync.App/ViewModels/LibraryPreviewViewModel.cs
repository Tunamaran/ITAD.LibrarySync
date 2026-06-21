using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class LibraryPreviewViewModel : ObservableObject
{
    private readonly IReadOnlyList<LibraryGameRow> _allOwned;
    private readonly IReadOnlyList<LibraryGameRow> _allWishlist;

    public LibraryPreviewViewModel(string launcherName, LauncherReadResult result)
    {
        LauncherName = launcherName;
        Summary = LauncherReadResultDisplay.FormatScanSummary(result);
        Status = LauncherReadResultDisplay.GetDetectionStatus(result);
        PreviewWarning = LauncherReadResultDisplay.FormatPreviewWarning(result);
        HasWarning = !string.IsNullOrWhiteSpace(PreviewWarning);

        DetailLines = new ObservableCollection<string>(LauncherReadResultDisplay.GetPreviewDetailLines(result));
        HasDetails = DetailLines.Count > 0;
        DetailsHeader = DetailLines.Count == 1
            ? "Details (1 item)"
            : $"Details ({DetailLines.Count} items)";

        _allOwned = result.Owned
            .Select(g => new LibraryGameRow(g.Title, g.StoreId))
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _allWishlist = result.Wishlist
            .Select(g => new LibraryGameRow(g.Title, g.StoreId))
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        OwnedGames = new ObservableCollection<LibraryGameRow>();
        WishlistGames = new ObservableCollection<LibraryGameRow>();
        ApplyFilter();
    }

    public string LauncherName { get; }

    public string Summary { get; }

    public string Status { get; }

    public string? PreviewWarning { get; }

    public bool HasWarning { get; }

    public bool HasDetails { get; }

    public string DetailsHeader { get; }

    public ObservableCollection<string> DetailLines { get; }

    public int OwnedCount => _allOwned.Count;

    public int WishlistCount => _allWishlist.Count;

    public ObservableCollection<LibraryGameRow> OwnedGames { get; }

    public ObservableCollection<LibraryGameRow> WishlistGames { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = SearchText.Trim();

        ReplaceCollection(OwnedGames, FilterRows(_allOwned, filter));
        ReplaceCollection(WishlistGames, FilterRows(_allWishlist, filter));
    }

    private static IEnumerable<LibraryGameRow> FilterRows(
        IReadOnlyList<LibraryGameRow> source,
        string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return source;

        return source.Where(row =>
            row.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            row.StoreId.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private static void ReplaceCollection(
        ObservableCollection<LibraryGameRow> target,
        IEnumerable<LibraryGameRow> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
