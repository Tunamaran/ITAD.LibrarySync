using ITAD.LibrarySync.Core.Launchers;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public sealed class SteamLibraryReaderTests : IDisposable
{
    private readonly string _root;
    private readonly string _secondLibrary;

    public SteamLibraryReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"steam_test_{Guid.NewGuid():N}");
        _secondLibrary = Path.Combine(Path.GetTempPath(), $"steam_lib2_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        try { if (Directory.Exists(_secondLibrary)) Directory.Delete(_secondLibrary, recursive: true); } catch { }
    }

    [Fact]
    public async Task GetInstalledGamesAsync_ReadsAllLibraries_Dedupes_AndFiltersInstalled()
    {
        // Steam root library: one installed game, one not-installed.
        var rootApps = Path.Combine(_root, "steamapps");
        Directory.CreateDirectory(rootApps);
        await File.WriteAllTextAsync(Path.Combine(rootApps, "appmanifest_105600.acf"), """
            "AppState"
            {
                "appid"		"105600"
                "name"		"Terraria"
                "StateFlags"		"4"
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(rootApps, "appmanifest_367520.acf"), """
            "AppState"
            {
                "appid"		"367520"
                "name"		"Hollow Knight"
                "StateFlags"		"0"
            }
            """);

        // libraryfolders.vdf pointing at a second library.
        var libraryFoldersVdf =
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" +
            _secondLibrary.Replace("\\", "\\\\") +
            "\"\n\t}\n}\n";
        await File.WriteAllTextAsync(Path.Combine(rootApps, "libraryfolders.vdf"), libraryFoldersVdf);

        // Second library: one installed game + a duplicate appid of Terraria.
        var secondApps = Path.Combine(_secondLibrary, "steamapps");
        Directory.CreateDirectory(secondApps);
        await File.WriteAllTextAsync(Path.Combine(secondApps, "appmanifest_427520.acf"), """
            "AppState"
            {
                "appid"		"427520"
                "name"		"Factorio"
                "StateFlags"		"4"
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(secondApps, "appmanifest_105600.acf"), """
            "AppState"
            {
                "appid"		"105600"
                "name"		"Terraria"
                "StateFlags"		"4"
            }
            """);

        var reader = new SteamLibraryReader(_root);
        var games = await reader.GetInstalledGamesAsync();

        Assert.Equal(2, games.Count);
        Assert.Contains(games, game => game.AppId == "105600" && game.Title == "Terraria");
        Assert.Contains(games, game => game.AppId == "427520" && game.Title == "Factorio");
        Assert.DoesNotContain(games, game => game.AppId == "367520"); // not installed
    }

    [Fact]
    public async Task GetInstalledGamesAsync_MissingRoot_ReturnsEmpty()
    {
        var reader = new SteamLibraryReader(Path.Combine(_root, "does-not-exist"));
        Assert.Empty(await reader.GetInstalledGamesAsync());
    }
}
