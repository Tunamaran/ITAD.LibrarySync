using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class CloudSaveMappingStorageTests : IDisposable
{
    private readonly string _tempFile;

    public CloudSaveMappingStorageTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"cloud_saves_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
    }

    [Fact]
    public async Task SaveAndGetAll_RoundTripsMapping()
    {
        var storage = new CloudSaveMappingStorage(_tempFile);
        var mapping = new CloudSaveMapping(
            "Game", @"C:\Saves\Game", @"D:\Cloud\ITAD_GameSaves\Game\Game",
            CloudProvider.OneDrive, IsActive: true, DateTime.Now, BackupPath: @"C:\Saves\Game.backup");

        await storage.SaveAsync(mapping);
        var all = await storage.GetAllAsync();

        var loaded = Assert.Single(all);
        Assert.Equal("Game", loaded.Title);
        Assert.Equal(CloudProvider.OneDrive, loaded.Provider);
        Assert.Equal(@"C:\Saves\Game.backup", loaded.BackupPath);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public async Task SaveAsync_SameSourcePath_ReplacesExisting()
    {
        var storage = new CloudSaveMappingStorage(_tempFile);
        var first = new CloudSaveMapping("Game", @"C:\Saves\Game", "T1", CloudProvider.OneDrive, true, DateTime.Now);
        var second = new CloudSaveMapping("Game", @"C:\Saves\Game", "T2", CloudProvider.OneDrive, true, DateTime.Now);

        await storage.SaveAsync(first);
        await storage.SaveAsync(second);

        var all = await storage.GetAllAsync();
        var loaded = Assert.Single(all);
        Assert.Equal("T2", loaded.TargetPath);
    }

    [Fact]
    public async Task FindBySourceAsync_MatchesCaseInsensitively()
    {
        var storage = new CloudSaveMappingStorage(_tempFile);
        await storage.SaveAsync(new CloudSaveMapping(
            "Game", @"C:\Saves\Game", "T1", CloudProvider.Dropbox, true, DateTime.Now));

        var found = await storage.FindBySourceAsync(@"c:\saves\game");

        Assert.NotNull(found);
        Assert.Equal("T1", found.TargetPath);
    }

    [Fact]
    public async Task RemoveAsync_DeletesOnlyMatchingSource()
    {
        var storage = new CloudSaveMappingStorage(_tempFile);
        await storage.SaveAsync(new CloudSaveMapping("A", @"C:\Saves\A", "T1", CloudProvider.OneDrive, true, DateTime.Now));
        await storage.SaveAsync(new CloudSaveMapping("B", @"C:\Saves\B", "T2", CloudProvider.OneDrive, true, DateTime.Now));

        await storage.RemoveAsync(@"C:\Saves\A");

        var all = await storage.GetAllAsync();
        var remaining = Assert.Single(all);
        Assert.Equal("B", remaining.Title);
    }
}
