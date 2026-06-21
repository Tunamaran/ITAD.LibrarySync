using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public static class XboxTitleMapper
{
    public static StoreGame? ToStoreGame(TitleHistoryItem item, int? playtimeMinutes)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            return null;

        var storeId = ResolveStoreId(item);
        if (string.IsNullOrWhiteSpace(storeId))
            return null;

        return new StoreGame(LauncherId.Xbox, storeId, item.Name.Trim())
        {
            PlaytimeMinutes = playtimeMinutes
        };
    }

    /// <summary>
    /// TitleHub often returns a numeric Xbox title ID in modernTitleId, not a Microsoft Store BigId.
    /// Prefer a real Store product ID, then PFN, then a prefixed legacy title ID.
    /// </summary>
    internal static string? ResolveStoreId(TitleHistoryItem item)
    {
        if (MicrosoftStoreId.IsProductId(item.ModernTitleId))
            return item.ModernTitleId;

        if (!string.IsNullOrWhiteSpace(item.Pfn))
            return item.Pfn;

        if (!string.IsNullOrWhiteSpace(item.TitleId))
            return $"xbox:{item.TitleId}";

        return null;
    }
}
