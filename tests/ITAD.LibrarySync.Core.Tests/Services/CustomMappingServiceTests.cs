using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;
using Xunit;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class CustomMappingServiceTests : IDisposable
{
    private readonly string _tempFile;

    public CustomMappingServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"custom_mapping_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task SetMappingAndGet_SavesAndRetrievesCustomMapping()
    {
        var service = new CustomMappingService(_tempFile);
        var mapping = new CustomGameMapping(LauncherId.Xbox, "STORE_ID_1", "itad-game-slug", "My Game", DateTime.Now);

        await service.SetMappingAsync(mapping);

        var retrieved = await service.GetMappingAsync(LauncherId.Xbox, "STORE_ID_1");

        Assert.NotNull(retrieved);
        Assert.Equal("itad-game-slug", retrieved.MappedId);
        Assert.Equal("My Game", retrieved.Title);
    }

    [Fact]
    public async Task RemoveMapping_DeletesMapping()
    {
        var service = new CustomMappingService(_tempFile);
        var mapping = new CustomGameMapping(LauncherId.Ea, "EA_GAME_1", "target-slug", "EA Game", DateTime.Now);

        await service.SetMappingAsync(mapping);
        var before = await service.GetMappingAsync(LauncherId.Ea, "EA_GAME_1");
        Assert.NotNull(before);

        await service.RemoveMappingAsync(LauncherId.Ea, "EA_GAME_1");
        var after = await service.GetMappingAsync(LauncherId.Ea, "EA_GAME_1");
        Assert.Null(after);
    }
}
