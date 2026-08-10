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
    /// <summary>
    /// Games physically installed on this PC (vs merely owned on the account).
    /// When <c>null</c>, callers should treat <see cref="Owned"/> as installed.
    /// </summary>
    public IReadOnlyList<StoreGame>? Installed { get; init; }

    public IReadOnlyList<string> WarningMessages { get; } = Warnings ?? [];
}
