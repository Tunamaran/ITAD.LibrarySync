namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaRegistryGameFilter
{
    private static readonly string[] LauncherTitles =
    [
        "EA app",
        "EA Desktop",
        "Origin",
        "Origin Client"
    ];

    internal static bool IsLauncherEntry(string title, string storeId)
    {
        if (storeId.Equals("ea-app", StringComparison.OrdinalIgnoreCase) ||
            storeId.Equals("ea-desktop", StringComparison.OrdinalIgnoreCase) ||
            storeId.Equals("origin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return LauncherTitles.Any(name =>
            title.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
