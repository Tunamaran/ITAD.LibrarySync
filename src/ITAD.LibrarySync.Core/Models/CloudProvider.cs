namespace ITAD.LibrarySync.Core.Models;

/// <summary>
/// Cloud sync provider whose local sync folder can host game save backups.
/// </summary>
public enum CloudProvider
{
    OneDrive,
    GoogleDrive,
    Dropbox
}
