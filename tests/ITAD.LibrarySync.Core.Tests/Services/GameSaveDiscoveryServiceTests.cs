using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class GameSaveDiscoveryServiceTests : IDisposable
{
    private readonly string _tempDb;
    private readonly string _tempSavesRoot;

    public GameSaveDiscoveryServiceTests()
    {
        _tempSavesRoot = Path.Combine(Path.GetTempPath(), $"itad_saves_{Guid.NewGuid():N}");
        _tempDb = Path.Combine(Path.GetTempPath(), $"game_save_paths_{Guid.NewGuid():N}.json");
        Environment.SetEnvironmentVariable("ITAD_TEST_SAVE_ROOT", _tempSavesRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ITAD_TEST_SAVE_ROOT", null);
        try { if (File.Exists(_tempDb)) File.Delete(_tempDb); } catch { }
        try { if (Directory.Exists(_tempSavesRoot)) Directory.Delete(_tempSavesRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task DiscoverAsync_ExpandsVariables_Dedupes_AndReportsExistence()
    {
        var existing = Path.Combine(_tempSavesRoot, "GameB");
        Directory.CreateDirectory(existing);

        await File.WriteAllTextAsync(_tempDb, """
            {
              "version": 1,
              "entries": [
                {
                  "title": "Game A",
                  "titles": ["A"],
                  "savePaths": ["%USERPROFILE%\\Saves\\GameA", "%USERPROFILE%\\Saves\\GameA"],
                  "sourceUrl": "https://www.pcgamingwiki.com/wiki/Game_A"
                },
                {
                  "title": "Game B",
                  "titles": ["B"],
                  "savePaths": ["%ITAD_TEST_SAVE_ROOT%\\GameB"]
                }
              ]
            }
            """);

        var service = new GameSaveDiscoveryService(_tempDb);
        var saves = await service.DiscoverAsync();

        Assert.Equal(2, saves.Count);

        var gameA = saves.Single(s => s.Title == "Game A");
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            gameA.SourcePath);
        Assert.Equal("https://www.pcgamingwiki.com/wiki/Game_A", gameA.SourceUrl);
        Assert.False(gameA.Exists);

        var gameB = saves.Single(s => s.Title == "Game B");
        Assert.Equal(existing, gameB.SourcePath, ignoreCase: true);
        Assert.True(gameB.Exists);
    }

    [Fact]
    public async Task DiscoverAsync_MissingDatabase_ReturnsEmpty()
    {
        var service = new GameSaveDiscoveryService(
            Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json"));

        var saves = await service.DiscoverAsync();

        Assert.Empty(saves);
    }

    [Fact]
    public async Task DiscoverAsync_CorruptDatabase_ReturnsEmpty()
    {
        await File.WriteAllTextAsync(_tempDb, "{ not valid json !!");

        var service = new GameSaveDiscoveryService(_tempDb);

        Assert.Empty(await service.DiscoverAsync());
    }

    [Fact]
    public async Task FindForTitleAsync_MatchesByAliasAndResolvesPath()
    {
        var existing = Path.Combine(_tempSavesRoot, "Witcher3");
        Directory.CreateDirectory(existing);

        await File.WriteAllTextAsync(_tempDb, """
            {
              "version": 1,
              "entries": [
                {
                  "title": "The Witcher 3: Wild Hunt",
                  "titles": ["The Witcher 3", "Witcher 3"],
                  "savePaths": ["%ITAD_TEST_SAVE_ROOT%\\Witcher3"],
                  "sourceUrl": "https://www.pcgamingwiki.com/wiki/The_Witcher_3:_Wild_Hunt"
                }
              ]
            }
            """);

        var service = new GameSaveDiscoveryService(_tempDb);

        var found = await service.FindForTitleAsync("Witcher 3");

        Assert.NotNull(found);
        Assert.Equal("The Witcher 3: Wild Hunt", found.Title);
        Assert.Equal(existing, found.SourcePath, ignoreCase: true);
        Assert.True(found.Exists);
        Assert.Equal("https://www.pcgamingwiki.com/wiki/The_Witcher_3:_Wild_Hunt", found.SourceUrl);
    }

    [Fact]
    public async Task FindForTitleAsync_NormalizesTitleForMatching()
    {
        await File.WriteAllTextAsync(_tempDb, """
            {
              "version": 1,
              "entries": [
                {
                  "title": "Cyberpunk 2077",
                  "titles": [],
                  "savePaths": ["%ITAD_TEST_SAVE_ROOT%\\Cyberpunk"],
                  "sourceUrl": "https://www.pcgamingwiki.com/wiki/Cyberpunk_2077"
                }
              ]
            }
            """);

        var service = new GameSaveDiscoveryService(_tempDb);

        var found = await service.FindForTitleAsync("CYBERPUNK 2077: ");

        Assert.NotNull(found);
        Assert.Equal("Cyberpunk 2077", found.Title);
    }

    [Fact]
    public async Task FindForTitleAsync_UnknownTitle_ReturnsNull()
    {
        await File.WriteAllTextAsync(_tempDb, """
            {
              "version": 1,
              "entries": [
                {
                  "title": "Hollow Knight",
                  "titles": [],
                  "savePaths": ["%ITAD_TEST_SAVE_ROOT%\\HollowKnight"]
                }
              ]
            }
            """);

        var service = new GameSaveDiscoveryService(_tempDb);

        Assert.Null(await service.FindForTitleAsync("Totally Unknown Game"));
    }

    [Fact]
    public void CreateManual_ExpandsVariables_AndChecksExistence()
    {
        var path = Path.Combine(_tempSavesRoot, "ManualGame");
        Directory.CreateDirectory(path);

        var service = new GameSaveDiscoveryService(_tempDb);
        var info = service.CreateManual("Manual Game", $"%ITAD_TEST_SAVE_ROOT%\\ManualGame");

        Assert.Equal("Manual Game", info.Title);
        Assert.Equal(path, info.SourcePath, ignoreCase: true);
        Assert.True(info.Exists);
        Assert.True(info.IsManual);
    }

    [Fact]
    public void CreateManual_EmptyTitle_UsesFolderName()
    {
        var service = new GameSaveDiscoveryService(_tempDb);
        var info = service.CreateManual(string.Empty, $"%ITAD_TEST_SAVE_ROOT%\\NoTitleGame");

        Assert.Equal("NoTitleGame", info.Title);
    }
}
