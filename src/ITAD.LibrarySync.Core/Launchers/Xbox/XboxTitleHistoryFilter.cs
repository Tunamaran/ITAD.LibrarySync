namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public static class XboxTitleHistoryFilter
{
    /// <summary>
    /// TitleHub returns every game ever played on the account. This keeps PC store games
    /// and excludes console-only history entries when device metadata is present.
    /// </summary>
    public static bool IsEligibleOwnedCandidate(TitleHistoryItem item)
    {
        if (!string.Equals(item.Type, "Game", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(item.Pfn) && string.IsNullOrWhiteSpace(item.ModernTitleId))
            return false;

        if (item.Devices is null || item.Devices.Count == 0)
            return true;

        return item.Devices.Any(device =>
            string.Equals(device, "PC", StringComparison.OrdinalIgnoreCase));
    }
}
