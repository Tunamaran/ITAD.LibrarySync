using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class PcgwSavePathParserTests
{
    private const string TerrariaSection = """
===Save game data location===
{{Game data|
{{Game data/saves|Windows|{{p|userprofile\Documents}}\My Games\Terraria\}}
{{Game data/saves|Steam|{{p|steam}}\userdata\{{p|uid}}\105600\remote\achievements-steam.dat|{{p|steam}}\userdata\{{p|uid}}\105600\remote\players\*.plr|{{p|steam}}\userdata\{{p|uid}}\105600\remote\worlds\*.wld}}
{{Game data/saves|OS X|{{p|osxhome}}/Library/Application Support/Terraria/}}
{{Game data/saves|Linux|{{p|xdgdatahome}}/Terraria/Players/*.plr|{{p|xdgdatahome}}/Terraria/Worlds/*.wld}}
}}
""";

    private const string RoadCraftSection = """
===Save game data location===
{{Game data|
{{Game data/saves|Epic Games Launcher|{{p|localappdata}}\Saber\RoadCraftGame\storage\EOS\user\{{p|uid}}\Main\save\}}
{{Game data/saves|Steam|{{p|localappdata}}\Saber\RoadCraftGame\storage\steam\user\{{p|uid}}\Main\save\}}
{{Game data/saves|Steam Play (Linux)|<SteamLibrary-folder>/steamapps/compatdata/2104890/pfx/}}
}}
""";

    [Fact]
    public void ParseWindowsSavePath_RealTerrariaSection_ReturnsWindowsPath()
    {
        var path = PcgwSavePathParser.ParseWindowsSavePath(TerrariaSection);

        Assert.Equal(@"%USERPROFILE%\Documents\My Games\Terraria", path);
    }

    [Fact]
    public void ParseWindowsSavePath_RoadCraftSection_ReturnsLauncherCandidatePaths()
    {
        var paths = PcgwSavePathParser.ParseWindowsSavePaths(RoadCraftSection);

        Assert.Equal(2, paths.Count);
        Assert.Equal(@"%LOCALAPPDATA%\Saber\RoadCraftGame\storage\EOS\user\*\Main\save", paths[0]);
        Assert.Equal(@"%LOCALAPPDATA%\Saber\RoadCraftGame\storage\steam\user\*\Main\save", paths[1]);
    }

    [Fact]
    public void ParseWindowsSavePath_SupportsGameDataRowVariant()
    {
        const string wikitext = "===Save game data location===\n{{Game data/row|Windows|{{p|userprofile}}\\Saves\\Game\\}}";

        Assert.Equal(@"%USERPROFILE%\Saves\Game", PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_ExcludesLinuxOnlyRows_ReturnsNull()
    {
        const string wikitext = """
{{Game data|
{{Game data/saves|Linux|{{p|xdgdatahome}}/Game/}}
{{Game data/saves|Steam Play (Linux)|<SteamLibrary-folder>/steamapps/compatdata/123/pfx/}}
}}
""";

        Assert.Null(PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_ResolvesDynamicUserPlaceholders()
    {
        const string wikitext = "{{Game data/saves|Windows|{{p|localappdata}}\\Company\\Game\\{{p|uid}}\\save\\}}";

        Assert.Equal(@"%LOCALAPPDATA%\Company\Game\*\save", PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_CleansWikiMarkup()
    {
        const string wikitext =
            "{{Game data/saves|Windows|{{p|userprofile}}\\Games\\[[link page|Nice Name]]<!-- comment --><br>\\}}";

        Assert.Equal(@"%USERPROFILE%\Games\Nice Name", PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(PcgwSavePathParser.ParseWindowsSavePath(null));
        Assert.Null(PcgwSavePathParser.ParseWindowsSavePath(string.Empty));
    }
}
