using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers;
using Xunit.Abstractions;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

[Collection("LauncherIntegration")]
public class LauncherReaderIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public LauncherReaderIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ScanAllLaunchers_ReportsLocalLibraryState()
    {
        foreach (var reader in LauncherReaderFactory.CreateAll())
        {
            var result = await reader.ReadAsync();

            _output.WriteLine(
                $"{reader.Launcher}: detected={result.IsDetected}, loggedIn={result.IsLoggedIn}, " +
                $"owned={result.Owned.Count}, wishlist={result.Wishlist.Count}, error={result.Error ?? "(none)"}");

            if (result.IsDetected)
            {
                result.Owned.Should().NotBeNull();
                foreach (var game in result.Owned.Take(3))
                    _output.WriteLine($"  - {game.Title} ({game.StoreId})");
                if (result.Owned.Count > 3)
                    _output.WriteLine($"  ... +{result.Owned.Count - 3} more");
            }
        }
    }
}
