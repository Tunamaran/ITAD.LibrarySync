using ITAD.LibrarySync.Core.Launchers;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public sealed class SteamVdfParserTests
{
    [Fact]
    public void ParseLibraryFolders_ExtractsAndUnescapesPaths()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"D:\\SteamLibrary"
                    "label"		""
                }
                "1"
                {
                    "path"		"E:\\Games\\Steam"
                    "label"		""
                }
            }
            """;

        var paths = SteamVdfParser.ParseLibraryFolders(vdf);

        Assert.Equal(2, paths.Count);
        Assert.Contains(@"D:\SteamLibrary", paths);
        Assert.Contains(@"E:\Games\Steam", paths);
    }

    [Fact]
    public void ParseLibraryFolders_DedupesAndIgnoresOtherKeys()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0" { "path" "C:\\Steam1" "label" "x" }
                "1" { "path" "c:\\steam1" "label" "y" }
                "2" { "label" "no path here" }
            }
            """;

        var paths = SteamVdfParser.ParseLibraryFolders(vdf);

        Assert.Single(paths);
        Assert.Equal(@"C:\Steam1", paths[0]);
    }

    [Fact]
    public void ParseLibraryFolders_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(SteamVdfParser.ParseLibraryFolders(string.Empty));
        Assert.Empty(SteamVdfParser.ParseLibraryFolders("   "));
    }

    [Fact]
    public void ParseAppManifest_InstalledGame_ReturnsManifest()
    {
        const string acf = """
            "AppState"
            {
                "appid"		"105600"
                "name"		"Terraria"
                "StateFlags"		"4"
                "installdir"		"Terraria"
            }
            """;

        var manifest = SteamVdfParser.ParseAppManifest(acf);

        Assert.NotNull(manifest);
        Assert.Equal("105600", manifest.AppId);
        Assert.Equal("Terraria", manifest.Title);
        Assert.True(manifest.IsInstalled);
    }

    [Fact]
    public void ParseAppManifest_NotInstalledGame_ReportsNotInstalled()
    {
        const string acf = """
            "AppState"
            {
                "appid"		"12345"
                "name"		"Hollow Knight"
                "StateFlags"		"0"
            }
            """;

        var manifest = SteamVdfParser.ParseAppManifest(acf);

        Assert.NotNull(manifest);
        Assert.False(manifest.IsInstalled);
    }

    [Fact]
    public void ParseAppManifest_MissingFields_ReturnsNull()
    {
        Assert.Null(SteamVdfParser.ParseAppManifest(string.Empty));
        Assert.Null(SteamVdfParser.ParseAppManifest("""
            "AppState"
            {
                "appid"		"42"
                "StateFlags"		"4"
            }
            """));
    }
}
