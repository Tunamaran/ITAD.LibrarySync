namespace ITAD.LibrarySync.Core.Models;

public sealed record LauncherReadResult(
    LauncherId Launcher,
    bool IsDetected,
    bool IsLoggedIn,
    IReadOnlyList<StoreGame> Owned,
    IReadOnlyList<StoreGame> Wishlist,
    bool WishlistReadable,
    string? Error = null,
    IReadOnlyList<string>? Warnings = null,
    string? ResolvedPath = null,
    string? DetectionSource = null)
{
    public IReadOnlyList<string> WarningMessages { get; } = Warnings ?? [];
}
