namespace ITAD.LibrarySync.Core.Models;

public sealed record LauncherReadResult(
    LauncherId Launcher,
    bool IsDetected,
    bool IsLoggedIn,
    IReadOnlyList<StoreGame> Owned,
    IReadOnlyList<StoreGame> Wishlist,
    bool WishlistReadable,
    string? Error = null);
