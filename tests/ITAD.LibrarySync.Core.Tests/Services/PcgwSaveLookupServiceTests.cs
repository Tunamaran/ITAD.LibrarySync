using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class PcgwSaveLookupServiceTests : IDisposable
{
    private readonly string _cacheDir;

    public PcgwSaveLookupServiceTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"pcgw_lookup_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task LookupAsync_FirstCallHitsApi_SecondCallServedFromCache()
    {
        var api = new Mock<IPcgwApiClient>();
        api.Setup(x => x.LookupSavePathAsync("Terraria", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PcgwSaveInfo("Terraria", @"%USERPROFILE%\Documents\My Games\Terraria"));

        var service = new PcgwSaveLookupService(api.Object, new PcgwSavePathCache(_cacheDir));

        var first = await service.LookupAsync("Terraria");
        var second = await service.LookupAsync("Terraria");

        Assert.True(first.UsedLiveRequest);
        Assert.NotNull(first.Info);
        Assert.Equal(@"%USERPROFILE%\Documents\My Games\Terraria", first.Info.SourcePath);

        Assert.False(second.UsedLiveRequest); // cache hit does not consume the live budget
        Assert.NotNull(second.Info);
        Assert.Equal(first.Info.SourcePath, second.Info.SourcePath);

        api.Verify(x => x.LookupSavePathAsync("Terraria", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LookupAsync_NegativeResultCached_ApiCalledOnce()
    {
        var api = new Mock<IPcgwApiClient>();
        api.Setup(x => x.LookupSavePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PcgwSaveInfo?)null);

        var service = new PcgwSaveLookupService(api.Object, new PcgwSavePathCache(_cacheDir));

        var first = await service.LookupAsync("Unknown Game");
        var second = await service.LookupAsync("Unknown Game");

        Assert.True(first.UsedLiveRequest);
        Assert.Null(first.Info);
        Assert.False(second.UsedLiveRequest); // negative cache hit
        Assert.Null(second.Info);
        api.Verify(x => x.LookupSavePathAsync("Unknown Game", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LookupAsync_ExpandsEnvironmentVariables()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "PCGWTestGame");
        var api = new Mock<IPcgwApiClient>();
        api.Setup(x => x.LookupSavePathAsync("Test Game", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PcgwSaveInfo("Test Game", @"%USERPROFILE%\PCGWTestGame"));

        var service = new PcgwSaveLookupService(api.Object, new PcgwSavePathCache(_cacheDir));

        var result = await service.LookupAsync("Test Game");

        Assert.True(result.UsedLiveRequest);
        Assert.NotNull(result.Info);
        Assert.Equal(expected, result.Info.SourcePath, ignoreCase: true);
        Assert.Equal("https://www.pcgamingwiki.com/wiki/Test_Game", result.Info.SourceUrl);
    }
}
