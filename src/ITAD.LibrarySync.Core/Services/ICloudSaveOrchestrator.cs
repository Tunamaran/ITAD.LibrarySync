using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface ICloudSaveOrchestrator
{
    /// <summary>Computes a dry-run plan for the given save folders.</summary>
    Task<IReadOnlyList<CloudSavePreview>> PreviewAsync(
        CloudProvider provider,
        IReadOnlyList<GameSaveInfo> saves,
        CancellationToken ct = default);

    /// <summary>
    /// Migrates each save folder: copies files into the cloud root, renames the
    /// original to a .backup folder and creates a junction at the original path.
    /// </summary>
    Task<IReadOnlyList<CloudSaveResult>> MigrateAsync(
        CloudProvider provider,
        IReadOnlyList<GameSaveInfo> saves,
        CancellationToken ct = default);

    /// <summary>Removes the junction and moves the backup folder back to the original path.</summary>
    Task<CloudSaveResult> RestoreAsync(CloudSaveMapping mapping, CancellationToken ct = default);
}
