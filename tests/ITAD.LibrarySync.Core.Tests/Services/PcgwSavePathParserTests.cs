namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class PcgwSavePathParserTests
{
    // Real wikitext of the "Save game data location" section of Terraria (fetched live).
    private const string TerrariaSection = """
===Save game data location===
{{Game data|
{{Game data/saves|Windows|{{p|userprofile\Documents}}\My Games\Terraria\}}
{{Game data/saves|Steam|{{p|steam}}\userdata\{{p|uid}}\105600\remote\achievements-steam.dat|{{p|steam}}\userdata\{{p|uid}}\105600\remote\players\*.plr|{{p|steam}}\userdata\{{p|uid}}\105600\remote\worlds\*.wld}}
{{Game data/saves|OS X|{{p|osxhome}}/Library/Application Support/Terraria/}}
{{Game data/saves|Linux|{{p|xdgdatahome}}/Terraria/Players/*.plr|{{p|xdgdatahome}}/Terraria/Worlds/*.wld}}
}}
""";

    [Fact]
    public void ParseWindowsSavePath_RealTerrariaSection_ReturnsWindowsPath()
    {
        var path = PcgwSavePathParser.ParseWindowsSavePath(TerrariaSection);

        Assert.Equal(@"%USERPROFILE%\Documents\My Games\Terraria", path);
    }

    [Fact]
    public void ParseWindowsSavePath_SupportsGameDataRowVariant()
    {
        const string wikitext = "===Save game data location===\n{{Game data/row|Windows|{{p|userprofile}}\\Saves\\Game\\}}";

        Assert.Equal(@"%USERPROFILE%\Saves\Game", PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_NoWindowsRow_ReturnsNull()
    {
        const string wikitext = """
{{Game data|
{{Game data/saves|Steam|{{p|steam}}\userdata\}}
{{Game data/saves|Linux|{{p|xdgdatahome}}/Game/}}
}}
""";

        Assert.Null(PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_UnresolvablePlaceholder_ReturnsNull()
    {
        const string wikitext = "{{Game data/saves|Windows|{{p|steam}}\\userdata\\{{p|uid}}\\1234\\}}";

        Assert.Null(PcgwSavePathParser.ParseWindowsSavePath(wikitext));
    }

    [Fact]
    public void ParseWindowsSavePath_SkipsUnresolvableRowAndUsesNext()
    {
        const string wikitext =
            "{{Game data/saves|Windows|{{p|steam}}\\userdata\\}}\n" +
            "{{Game data/saves|Windows|{{p|appdata}}\\Game\\}}";

        Assert.Equal(@"%APPDATA%\Game", PcgwSavePathParser.ParseWindowsSavePath(wikitext));
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
