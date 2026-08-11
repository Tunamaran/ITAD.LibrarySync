using System.Text.Json;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Scheduling;

namespace ITAD.LibrarySync.Core.Tests.Scheduling;

public sealed class AppSettingsStorageTests
{
    [Fact]
    public void AppSettings_RoundTripsCloudSaveState()
    {
        var settings = new AppSettings
        {
            CloudSaveProvider = "GoogleDrive",
            CloudScannedGames =
            {
                new CloudScannedGameEntry
                {
                    Title = "Portal 2",
                    Platform = "Steam",
                    SourcePath = @"C:\Saves\Portal 2",
                    IsSelected = true,
                    StatusText = "Found",
                    StatusColor = "#16A34A"
                },
                new CloudScannedGameEntry
                {
                    Title = "Cyberpunk 2077",
                    Platform = "GOG"
                }
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.Equal("GoogleDrive", loaded.CloudSaveProvider);
        Assert.Equal(2, loaded.CloudScannedGames.Count);

        var first = loaded.CloudScannedGames[0];
        Assert.Equal("Portal 2", first.Title);
        Assert.Equal("Steam", first.Platform);
        Assert.Equal(@"C:\Saves\Portal 2", first.SourcePath);
        Assert.True(first.IsSelected);
        Assert.Equal("Found", first.StatusText);
        Assert.Equal("#16A34A", first.StatusColor);

        // A row without a save folder keeps its entry with an empty path.
        Assert.Equal("", loaded.CloudScannedGames[1].SourcePath);
    }
}
