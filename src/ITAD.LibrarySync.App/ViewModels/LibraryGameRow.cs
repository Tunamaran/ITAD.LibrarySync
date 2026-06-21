namespace ITAD.LibrarySync.App.ViewModels;

public sealed class LibraryGameRow
{
    public LibraryGameRow(string title, string storeId)
    {
        Title = title;
        StoreId = storeId;
    }

    public string Title { get; }

    public string StoreId { get; }
}
