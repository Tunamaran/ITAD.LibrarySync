using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class CloudSaveOrchestratorTests : IDisposable
{
    private readonly string _root;
    private readonly string _storageFile;
    private readonly List<(string Link, string Target)> _createdJunctions = [];
    private readonly List<string> _deletedJunctions = [];
    private string? _junctionError;
    private string? _deleteJunctionError;

    public CloudSaveOrchestratorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"itad_cloud_{Guid.NewGuid():N}");
        _storageFile = Path.Combine(Path.GetTempPath(), $"cloud_saves_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        try { if (File.Exists(_storageFile)) File.Delete(_storageFile); } catch { }
    }

    private CloudSaveOrchestrator CreateOrchestrator(string? root = null)
    {
        var locator = new StubLocator(root ?? _root);
        var storage = new CloudSaveMappingStorage(_storageFile);
        return new CloudSaveOrchestrator(
            locator,
            storage,
            logger: null,
            createJunction: (link, target) =>
            {
                _createdJunctions.Add((link, target));
                return _junctionError;
            },
            deleteJunction: path =>
            {
                _deletedJunctions.Add(path);
                return _deleteJunctionError;
            });
    }

    [Fact]
    public void BuildTargetPath_CombinesRootTitleAndFolder()
    {
        var target = CloudSaveOrchestrator.BuildTargetPath(
            @"C:\OneDrive", "The Witcher 3: Wild Hunt", @"C:\Users\Me\Documents\The Witcher 3\gamesaves");

        Assert.Equal(
            @"C:\OneDrive\ITAD_GameSaves\The Witcher 3 Wild Hunt\gamesaves",
            target);
    }

    [Fact]
    public async Task Preview_ReportsSourceMissing_WhenFolderDoesNotExist()
    {
        var orchestrator = CreateOrchestrator();
        var save = new GameSaveInfo("Game", Path.Combine(_root, "missing"), Exists: false);

        var previews = await orchestrator.PreviewAsync(CloudProvider.OneDrive, [save]);

        var preview = Assert.Single(previews);
        Assert.Equal(CloudSaveStatus.SourceMissing, preview.Status);
        Assert.False(string.IsNullOrEmpty(preview.TargetPath));
    }

    [Fact]
    public async Task Preview_ReportsReady_WhenSourceExistsAndTargetFree()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);

        var orchestrator = CreateOrchestrator();
        var save = new GameSaveInfo("Game", source, Exists: true);

        var previews = await orchestrator.PreviewAsync(CloudProvider.OneDrive, [save]);

        var preview = Assert.Single(previews);
        Assert.Equal(CloudSaveStatus.Ready, preview.Status);
        Assert.StartsWith(Path.Combine(_root, "ITAD_GameSaves"), preview.TargetPath);
    }

    [Fact]
    public async Task Preview_ReportsTargetConflict_WhenTargetAlreadyExists()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);
        var target = CloudSaveOrchestrator.BuildTargetPath(_root, "Game", source);
        Directory.CreateDirectory(target);

        var orchestrator = CreateOrchestrator();
        var previews = await orchestrator.PreviewAsync(CloudProvider.OneDrive, [new GameSaveInfo("Game", source, Exists: true)]);

        Assert.Equal(CloudSaveStatus.TargetConflict, Assert.Single(previews).Status);
    }

    [Fact]
    public async Task Preview_ReportsUnavailable_WhenCloudRootMissing()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);

        var orchestrator = CreateOrchestrator(root: null);
        var previews = await orchestrator.PreviewAsync(CloudProvider.OneDrive, [new GameSaveInfo("Game", source, Exists: true)]);

        var preview = Assert.Single(previews);
        Assert.Equal(CloudSaveStatus.Unavailable, preview.Status);
        Assert.Equal(string.Empty, preview.TargetPath);
    }

    [Fact]
    public async Task Migrate_CopiesRenamesAndPersistsMapping()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(Path.Combine(source, "Sub"));
        await File.WriteAllTextAsync(Path.Combine(source, "save.dat"), "data");
        await File.WriteAllTextAsync(Path.Combine(source, "Sub", "nested.dat"), "nested");

        var orchestrator = CreateOrchestrator();
        var results = await orchestrator.MigrateAsync(
            CloudProvider.OneDrive,
            [new GameSaveInfo("Game", source, Exists: true)]);

        var result = Assert.Single(results);
        Assert.True(result.Success, result.Message);

        // Junction created at the original path pointing into the cloud root.
        var junction = Assert.Single(_createdJunctions);
        Assert.Equal(source, junction.Link, ignoreCase: true);
        Assert.StartsWith(Path.Combine(_root, "ITAD_GameSaves"), junction.Target);

        // Original renamed to .backup, files copied to cloud target.
        Assert.False(Directory.Exists(source));
        Assert.True(Directory.Exists(source + ".backup"));
        Assert.True(File.Exists(Path.Combine(junction.Target, "save.dat")));
        Assert.True(File.Exists(Path.Combine(junction.Target, "Sub", "nested.dat")));

        // Mapping persisted.
        var storage = new CloudSaveMappingStorage(_storageFile);
        var mapping = await storage.FindBySourceAsync(source);
        Assert.NotNull(mapping);
        Assert.True(mapping.IsActive);
        Assert.Equal(CloudProvider.OneDrive, mapping.Provider);
        Assert.Equal(source + ".backup", mapping.BackupPath, ignoreCase: true);
    }

    [Fact]
    public async Task Migrate_FailsWhenSourceMissing()
    {
        var orchestrator = CreateOrchestrator();
        var results = await orchestrator.MigrateAsync(
            CloudProvider.OneDrive,
            [new GameSaveInfo("Game", Path.Combine(_root, "Nope"), Exists: false)]);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.Empty(_createdJunctions);
    }

    [Fact]
    public async Task Migrate_FailsWhenTargetAlreadyExists()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(CloudSaveOrchestrator.BuildTargetPath(_root, "Game", source));

        var orchestrator = CreateOrchestrator();
        var results = await orchestrator.MigrateAsync(
            CloudProvider.OneDrive,
            [new GameSaveInfo("Game", source, Exists: true)]);

        Assert.False(Assert.Single(results).Success);
        Assert.Empty(_createdJunctions);
        Assert.True(Directory.Exists(source));
    }

    [Fact]
    public async Task Migrate_RollsBack_WhenJunctionCreationFails()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "save.dat"), "data");
        _junctionError = "access denied";

        var orchestrator = CreateOrchestrator();
        var results = await orchestrator.MigrateAsync(
            CloudProvider.OneDrive,
            [new GameSaveInfo("Game", source, Exists: true)]);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.Contains("access denied", result.Message);

        // Rollback: original folder restored, cloud target cleaned up.
        Assert.True(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(source, "save.dat")));
        Assert.False(Directory.Exists(source + ".backup"));
        Assert.False(Directory.Exists(CloudSaveOrchestrator.BuildTargetPath(_root, "Game", source)));

        var storage = new CloudSaveMappingStorage(_storageFile);
        Assert.Null(await storage.FindBySourceAsync(source));
    }

    [Fact]
    public async Task Restore_MovesBackupBack_AndRemovesMapping()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source + ".backup");
        await File.WriteAllTextAsync(Path.Combine(source + ".backup", "save.dat"), "data");

        var mapping = new CloudSaveMapping(
            "Game", source, Path.Combine(_root, "ITAD_GameSaves", "Game", "Source"),
            CloudProvider.OneDrive, IsActive: true, DateTime.Now, BackupPath: source + ".backup");

        var storage = new CloudSaveMappingStorage(_storageFile);
        await storage.SaveAsync(mapping);

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.RestoreAsync(mapping);

        Assert.True(result.Success, result.Message);
        Assert.True(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(source, "save.dat")));
        Assert.False(Directory.Exists(source + ".backup"));
        Assert.Null(await storage.FindBySourceAsync(source));
    }

    [Fact]
    public async Task Restore_WithoutJunction_LeavesFolderAndRemovesMapping()
    {
        // A non-junction source folder with no backup: restore should not touch
        // the folder (it already is the original) and only remove the mapping.
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);

        var mapping = new CloudSaveMapping(
            "Game", source, Path.Combine(_root, "T"), CloudProvider.OneDrive, true, DateTime.Now);

        var storage = new CloudSaveMappingStorage(_storageFile);
        await storage.SaveAsync(mapping);

        // JunctionHelper.IsJunction returns false for a normal directory, so
        // restore should simply remove the mapping and leave the folder alone.
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.RestoreAsync(mapping);

        Assert.True(result.Success, result.Message);
        Assert.Empty(_deletedJunctions);
        Assert.True(Directory.Exists(source));
        Assert.Null(await storage.FindBySourceAsync(source));
    }

    [Fact]
    public async Task Migrate_RollsBack_WhenMappingPersistenceFails()
    {
        var source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "save.dat"), "data");

        // Make the storage file path un-writable: an existing directory.
        var brokenStorageFile = Path.Combine(_root, "storage-dir");
        Directory.CreateDirectory(brokenStorageFile);

        var locator = new StubLocator(_root);
        var storage = new CloudSaveMappingStorage(brokenStorageFile);
        var orchestrator = new CloudSaveOrchestrator(
            locator,
            storage,
            logger: null,
            createJunction: (link, target) =>
            {
                _createdJunctions.Add((link, target));
                return null;
            },
            deleteJunction: path =>
            {
                _deletedJunctions.Add(path);
                return null;
            });

        var results = await orchestrator.MigrateAsync(
            CloudProvider.OneDrive,
            [new GameSaveInfo("Game", source, Exists: true)]);

        var result = Assert.Single(results);
        Assert.False(result.Success);

        // Rollback: original restored, no cloud target left behind.
        Assert.True(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(source, "save.dat")));
        Assert.False(Directory.Exists(CloudSaveOrchestrator.BuildTargetPath(_root, "Game", source)));
    }

    private sealed class StubLocator(string? root) : ICloudProviderLocator
    {
        public IReadOnlyList<CloudProvider> GetAvailableProviders() =>
            root is null ? [] : [CloudProvider.OneDrive];

        public string? GetCloudRoot(CloudProvider provider) =>
            provider == CloudProvider.OneDrive ? root : null;
    }
}
