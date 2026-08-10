using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class PcgwSavePathCacheTests : IDisposable
{
    private readonly string _cacheDir;

    public PcgwSavePathCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"pcgw_cache_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task PutAndGet_RoundTripsFoundResult()
    {
        var cache = new PcgwSavePathCache(_cacheDir);
        await cache.PutAsync("Terraria", new PcgwSaveInfo("Terraria", @"%USERPROFILE%\Documents\My Games\Terraria", "https://www.pcgamingwiki.com/wiki/Terraria"));

        var (isHit, info) = await cache.TryGetAsync("Terraria");

        Assert.True(isHit);
        Assert.NotNull(info);
        Assert.Equal(@"%USERPROFILE%\Documents\My Games\Terraria", info.SavePath);
        Assert.Equal("Terraria", info.PageTitle);
    }

    [Fact]
    public async Task PutAndGet_NegativeResultIsCached()
    {
        var cache = new PcgwSavePathCache(_cacheDir);
        await cache.PutAsync("Unknown Game", null);

        var (isHit, info) = await cache.TryGetAsync("Unknown Game");

        Assert.True(isHit);
        Assert.Null(info);
    }

    [Fact]
    public async Task TryGet_MissingEntry_ReturnsMiss()
    {
        var cache = new PcgwSavePathCache(_cacheDir);

        var (isHit, info) = await cache.TryGetAsync("Never Cached");

        Assert.False(isHit);
        Assert.Null(info);
    }

    [Fact]
    public async Task TryGet_ExpiredFoundEntry_ReturnsMiss()
    {
        var cache = new PcgwSavePathCache(_cacheDir, foundTtl: TimeSpan.FromMilliseconds(1));
        await cache.PutAsync("Terraria", new PcgwSaveInfo("Terraria", @"%USERPROFILE%\Saves\Terraria"));
        Thread.Sleep(20);

        var (isHit, info) = await cache.TryGetAsync("Terraria");

        Assert.False(isHit);
        Assert.Null(info);
    }

    [Fact]
    public async Task TryGet_ExpiredNegativeEntry_ReturnsMiss()
    {
        var cache = new PcgwSavePathCache(_cacheDir, notFoundTtl: TimeSpan.FromMilliseconds(1));
        await cache.PutAsync("Unknown Game", null);
        Thread.Sleep(20);

        var (isHit, info) = await cache.TryGetAsync("Unknown Game");

        Assert.False(isHit);
        Assert.Null(info);
    }
}
