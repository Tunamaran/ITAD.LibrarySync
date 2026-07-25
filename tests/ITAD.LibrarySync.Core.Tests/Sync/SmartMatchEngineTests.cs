using FluentAssertions;
using ITAD.LibrarySync.Core.Sync;
using Xunit;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class SmartMatchEngineTests
{
    [Theory]
    [InlineData("civilization-vi-gathering-storm", "Sid Meier's Civilization VI: Gathering Storm", "sid-meiers-civilization-vi-gathering-storm", "Sid Meier's Civilization VI: Gathering Storm")]
    [InlineData("civilization-vi", "Civilization VI", "sid-meiers-civilization-vi", "Sid Meier's Civilization VI")]
    [InlineData("rainbow-six-siege", "Rainbow Six Siege", "tom-clancys-rainbow-six-siege", "Tom Clancy's Rainbow Six Siege")]
    [InlineData("fifa-23", "FIFA 23", "ea-sports-fifa-23", "EA SPORTS FIFA 23")]
    [InlineData("custom-game-id", "The Witcher 3 - Hearts of Stone DLC", "custom-game-id", "The Witcher 3: Hearts of Stone DLC")]
    [InlineData("game-123", "Cyberpunk 2077 (WW)", "game-123", "Cyberpunk 2077")]
    public void ResolveAutoMatch_returns_expected_mapped_id_and_title(
        string inputStoreId,
        string inputTitle,
        string expectedId,
        string expectedTitle)
    {
        var (id, title) = AutoMatchResolver.ResolveAutoMatch(inputStoreId, inputTitle);
        id.Should().Be(expectedId);
        title.Should().Be(expectedTitle);
    }

    [Theory]
    [InlineData("civilization-vi-gathering-storm", "Sid Meier's Civilization VI: Gathering Storm", "civilization-vi-gathering-storm")]
    public void GetObsoleteIdIfReplaced_identifies_replaced_store_ids(string inputStoreId, string inputTitle, string expectedObsoleteId)
    {
        var obsolete = AutoMatchResolver.GetObsoleteIdIfReplaced(inputStoreId, inputTitle);
        obsolete.Should().Be(expectedObsoleteId);
    }
}
