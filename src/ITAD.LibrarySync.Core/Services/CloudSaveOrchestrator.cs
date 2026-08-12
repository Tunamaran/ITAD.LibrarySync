using System.Text;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Moves game save folders into a cloud-synced directory and replaces the original
/// path with an NTFS directory junction, so games keep reading/writing saves
/// transparently while the cloud client uploads them.
///
/// File operations are real; junction creation is delegated to a <see cref="JunctionHelper"/>
/// (or an injected delegate for tests). Junctions do not require elevation.
/// </summary>
public sealed class CloudSaveOrchestrator : ICloudSaveOrchestrator
{
    public const string CloudRootFolderName = "ITAD_GameSaves";

    private readonly ICloudProviderLocator _locator;
    private readonly ICloudSaveMappingStorage _storage;
    private readonly FileLogger? _logger;
    private readonly Func<string, string, string?> _createJunction;
    private readonly Func<string, string?> _deleteJunction;
    private readonly Func<string, bool> _pathExists;

    public CloudSaveOrchestrator(
        ICloudProviderLocator locator,
        ICloudSaveMappingStorage storage,
        FileLogger? logger = null,
        Func<string, string, string?>? createJunction = null,
        Func<string, string?>? deleteJunction = null,
        Func<string, bool>? pathExists = null)
    {
        _locator = locator;
        _storage = storage;
        _logger = logger;
        _createJunction = createJunction ?? JunctionHelper.TryCreate;
        _deleteJunction = deleteJunction ?? JunctionHelper.TryDelete;
        _pathExists = pathExists ?? Directory.Exists;
    }

    public async Task<IReadOnlyList<CloudSavePreview>> PreviewAsync(
        CloudProvider provider,
        IReadOnlyList<GameSaveInfo> saves,
        CancellationToken ct = default)
    {
        var root = _locator.GetCloudRoot(provider);
        var mappings = await _storage.GetAllAsync(ct);

        var previews = new List<CloudSavePreview>(saves.Count);
        foreach (var save in saves)
        {
            ct.ThrowIfCancellationRequested();

            if (root is null)
            {
                previews.Add(new CloudSavePreview(
                    save.Title, save.SourcePath, string.Empty, CloudSaveStatus.Unavailable));
                continue;
            }

            var target = BuildTargetPath(root, save.Title, save.SourcePath);
            var mapping = mappings.FirstOrDefault(item =>
                string.Equals(item.SourcePath, save.SourcePath, StringComparison.OrdinalIgnoreCase) && item.IsActive);
            var isJunction = save.Exists && JunctionHelper.IsJunction(save.SourcePath);

            var status = !save.Exists
                ? CloudSaveStatus.SourceMissing
                : isJunction && mapping is not null
                    ? CloudSaveStatus.AlreadyMigrated
                    : isJunction
                        ? CloudSaveStatus.OrphanJunction
                        : mapping is not null
                            ? CloudSaveStatus.StaleMapping
                            : _pathExists(target)
                                ? CloudSaveStatus.TargetConflict
                                : CloudSaveStatus.Ready;

            previews.Add(new CloudSavePreview(save.Title, save.SourcePath, target, status));
        }

        return previews;
    }

    public async Task<IReadOnlyList<CloudSaveResult>> MigrateAsync(
        CloudProvider provider,
        IReadOnlyList<GameSaveInfo> saves,
        CancellationToken ct = default)
    {
        var root = _locator.GetCloudRoot(provider);
        var results = new List<CloudSaveResult>(saves.Count);

        foreach (var save in saves)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await MigrateOneAsync(provider, root, save, ct));
        }

        return results;
    }

    public async Task<CloudSaveResult> RestoreAsync(CloudSaveMapping mapping, CancellationToken ct = default)
    {
        var source = mapping.SourcePath;
        if (!mapping.IsActive)
            return new CloudSaveResult(mapping.Title, source, false, "Mapping is not active.");

        var backupPath = mapping.BackupPath ?? source + ".backup";
        var hasBackup = _pathExists(backupPath);

        // Verify the backup exists BEFORE touching the junction, so a missing
        // backup never leaves the game save path broken.
        if (!hasBackup)
        {
            if (JunctionHelper.IsJunction(source))
                return new CloudSaveResult(mapping.Title, source, false, "Backup folder not found; junction left untouched.");

            // Stale mapping: original folder is intact, only the record is removed.
            try
            {
                await _storage.RemoveAsync(source, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"CloudSaveOrchestrator: failed to remove stale mapping for '{source}' — {ex.Message}");
                return new CloudSaveResult(mapping.Title, source, false, $"Failed to remove stale mapping: {ex.Message}");
            }

            return new CloudSaveResult(mapping.Title, source, true, "No backup found; removed stale mapping.");
        }

        if (JunctionHelper.IsJunction(source))
        {
            var deleteError = _deleteJunction(source);
            if (deleteError is not null)
            {
                _logger?.LogError($"CloudSaveOrchestrator: restore failed to remove junction '{source}' — {deleteError}");
                return new CloudSaveResult(mapping.Title, source, false, deleteError);
            }
        }

        if (_pathExists(source))
            return new CloudSaveResult(mapping.Title, source, false, "Original path is already occupied.");

        try
        {
            Directory.Move(backupPath, source);
        }
        catch (Exception ex)
        {
            // Best effort: put the junction back so the game keeps working.
            try
            {
                if (!_pathExists(source))
                {
                    var recreateError = _createJunction(source, mapping.TargetPath);
                    if (recreateError is not null)
                        _logger?.LogError($"CloudSaveOrchestrator: failed to re-create junction for '{source}' — {recreateError}");
                }
            }
            catch (Exception recreateEx)
            {
                _logger?.LogError($"CloudSaveOrchestrator: failed to re-create junction for '{source}' — {recreateEx.Message}");
            }

            _logger?.LogError($"CloudSaveOrchestrator: restore failed for '{source}' — {ex.Message}");
            return new CloudSaveResult(mapping.Title, source, false, ex.Message);
        }

        try
        {
            await _storage.RemoveAsync(source, ct);
        }
        catch (Exception ex)
        {
            // Data is restored; a stale mapping record is a lesser problem.
            _logger?.LogError($"CloudSaveOrchestrator: restore succeeded but mapping removal failed — {ex.Message}");
        }

        _logger?.LogInfo($"CloudSaveOrchestrator: restored '{source}' from cloud backup.");
        return new CloudSaveResult(mapping.Title, source, true, "Restored.");
    }

    /// <summary>
    /// Computes the cloud target folder for a save: &lt;root&gt;\ITAD_GameSaves\&lt;title&gt;\&lt;folder&gt;.
    /// </summary>
    public static string BuildTargetPath(string cloudRoot, string title, string sourcePath)
    {
        var safeTitle = SanitizeSegment(title);
        var folderName = Path.GetFileName(sourcePath.TrimEnd('\\', '/'));
        return Path.Combine(cloudRoot, CloudRootFolderName, safeTitle, folderName);
    }

    private async Task<CloudSaveResult> MigrateOneAsync(
        CloudProvider provider,
        string? root,
        GameSaveInfo save,
        CancellationToken ct)
    {
        var source = save.SourcePath;

        if (root is null)
            return new CloudSaveResult(save.Title, source, false, "Cloud provider root folder is not available.");

        if (!_pathExists(source))
            return new CloudSaveResult(save.Title, source, false, "Source folder not found.");

        if (JunctionHelper.IsJunction(source))
            return new CloudSaveResult(save.Title, source, false, "Source is already a junction.");

        var existing = await _storage.FindBySourceAsync(source, ct);
        if (existing is { IsActive: true })
            return new CloudSaveResult(save.Title, source, false, "Already migrated.");

        var target = BuildTargetPath(root, save.Title, source);
        if (_pathExists(target))
            return new CloudSaveResult(save.Title, source, false, $"Target folder already exists: {target}");

        var backupPath = source + ".backup";
        if (_pathExists(backupPath))
            backupPath = $"{source}.backup-{DateTime.Now:yyyyMMddHHmmss}";

        try
        {
            Directory.CreateDirectory(target);
            CopyDirectory(source, target);

            Directory.Move(source, backupPath);

            var junctionError = _createJunction(source, target);
            if (junctionError is not null)
            {
                Rollback(source, backupPath, target);
                _logger?.LogError($"CloudSaveOrchestrator: junction creation failed for '{source}' — {junctionError}");
                return new CloudSaveResult(save.Title, source, false, junctionError);
            }

            var mapping = new CloudSaveMapping(
                Title: save.Title,
                SourcePath: source,
                TargetPath: target,
                Provider: provider,
                IsActive: true,
                CreatedAt: DateTime.Now,
                LastVerified: DateTime.Now,
                BackupPath: backupPath);

            await _storage.SaveAsync(mapping, ct);
            _logger?.LogInfo($"CloudSaveOrchestrator: migrated '{source}' → '{target}' (junction created).");
            return new CloudSaveResult(save.Title, source, true, "Migrated.");
        }
        catch (Exception ex)
        {
            Rollback(source, backupPath, target);
            _logger?.LogError($"CloudSaveOrchestrator: migration failed for '{source}' — {ex.Message}");
            return new CloudSaveResult(save.Title, source, false, ex.Message);
        }
    }

    private void Rollback(string source, string backupPath, string target)
    {
        // 1. If a junction was created, remove it first so the original path is free.
        try
        {
            if (JunctionHelper.IsJunction(source))
                _deleteJunction(source);
        }
        catch
        {
            // best effort
        }

        // 2. Move the backup back to the original location.
        try
        {
            if (_pathExists(backupPath) && !_pathExists(source))
            {
                try
                {
                    Directory.Move(backupPath, source);
                }
                catch
                {
                    CopyDirectory(backupPath, source);
                    try { Directory.Delete(backupPath, recursive: true); } catch { }
                }
            }
        }
        catch
        {
            // best effort
        }

        // 3. Clean up the partially copied cloud target.
        try
        {
            if (_pathExists(target))
                Directory.Delete(target, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    private void CopyDirectory(string sourceDir, string targetDir) =>
        CopyDirectoryCore(sourceDir, sourceDir, targetDir);

    private void CopyDirectoryCore(string root, string current, string targetRoot)
    {
        foreach (var directory in SafeEnumerateDirectories(current))
        {
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    _logger?.LogWarning($"CloudSaveOrchestrator: skipped junction/symlink inside save folder: {directory}");
                    continue;
                }
            }
            catch
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, directory);
            Directory.CreateDirectory(Path.Combine(targetRoot, relative));
            CopyDirectoryCore(root, directory, targetRoot);
        }

        foreach (var file in SafeEnumerateFiles(current))
        {
            var relative = Path.GetRelativePath(root, file);
            CopyFileWithRetry(file, Path.Combine(targetRoot, relative));
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            // Materialize immediately: Directory.Enumerate* is lazy, so an
            // access-denied subdirectory would otherwise throw at the caller.
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static void CopyFileWithRetry(string sourceFile, string targetFile, int attempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(sourceFile, targetFile, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                Thread.Sleep(300 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < attempts)
            {
                Thread.Sleep(300 * attempt);
            }
        }
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Game";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (Array.IndexOf(invalid, character) < 0 && character is not ':' and not '\\' and not '/')
                builder.Append(character);
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "Game" : result;
    }
}
