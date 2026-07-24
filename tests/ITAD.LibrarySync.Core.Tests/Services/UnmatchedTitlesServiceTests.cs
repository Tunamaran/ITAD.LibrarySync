using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;
using Xunit;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class UnmatchedTitlesServiceTests : IDisposable
{
    private readonly string _tempFile;

    public UnmatchedTitlesServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"unmatched_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task AddAndGetAll_SavesAndRetrievesTitles()
    {
        var service = new UnmatchedTitlesService(_tempFile);

        var title1 = new UnmatchedTitle(LauncherId.Xbox, "PRODUCT_1", "Game One", "Not in ITAD catalog", DateTime.Now);
        var title2 = new UnmatchedTitle(LauncherId.Ea, "EA_2", "Game Two", "Unknown shop ID", DateTime.Now);

        await service.AddAsync(title1);
        await service.AddAsync(title2);

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, x => x.StoreId == "PRODUCT_1");
        Assert.Contains(all, x => x.StoreId == "EA_2");
    }

    [Fact]
    public async Task Clear_RemovesSavedTitles()
    {
        var service = new UnmatchedTitlesService(_tempFile);
        await service.AddAsync(new UnmatchedTitle(LauncherId.Xbox, "P1", "Game", "Reason", DateTime.Now));

        var initial = await service.GetAllAsync();
        Assert.Single(initial);

        await service.ClearAsync();

        var empty = await service.GetAllAsync();
        Assert.Empty(empty);
    }
}
