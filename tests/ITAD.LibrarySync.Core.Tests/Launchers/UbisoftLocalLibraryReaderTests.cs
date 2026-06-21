using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Ubisoft;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class UbisoftLocalLibraryReaderTests
{
    [Fact]
    public void ReadOwnedGames_ReadsLocalAppDataCacheWhenPresent()
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
        games.Should().Contain(g => g.Title.Contains("Far Cry", StringComparison.OrdinalIgnoreCase)
            || g.StoreId.Length > 0);
    }
}
