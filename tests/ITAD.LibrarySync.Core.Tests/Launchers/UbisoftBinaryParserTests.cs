using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Ubisoft;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class UbisoftBinaryParserTests
{
    [Fact]
    public void ParseOwnedIds_ReadsLocalOwnershipCacheWhenPresent()
    {
        var ownershipDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ubisoft Game Launcher",
            "cache",
            "ownership");

        if (!Directory.Exists(ownershipDirectory))
            return;

        var ownershipPath = Directory.EnumerateFiles(ownershipDirectory).FirstOrDefault();
        ownershipPath.Should().NotBeNull();

        var ownedIds = UbisoftBinaryParser.ParseOwnedIds(File.ReadAllBytes(ownershipPath!));

        ownedIds.Count.Should().BeInRange(1, 500);
        ownedIds.Count.Should().BeLessThan(300, "ownership should not include the full Ubisoft catalog");
    }

    [Fact]
    public void ReadOwnedGames_ReturnsOnlyOwnedTitlesWhenCachePresent()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ubisoft Game Launcher",
            "cache",
            "configuration",
            "configurations");

        if (!File.Exists(configPath))
            return;

        var games = UbisoftLocalLibraryReader.ReadOwnedGames();

        games.Count.Should().BeInRange(1, 300);
        games.Should().OnlyContain(game => !string.IsNullOrWhiteSpace(game.Title));
        games.Should().OnlyContain(game => !string.IsNullOrWhiteSpace(game.StoreId));
        games.Should().OnlyContain(game => !UbisoftLocalLibraryReader.IsPlaceholderTitle(game.Title));
    }
}
