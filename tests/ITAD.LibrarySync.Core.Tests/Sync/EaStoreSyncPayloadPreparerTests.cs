using FluentAssertions;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class EaStoreSyncPayloadPreparerTests
{
    private readonly Mock<IItadApiClient> _api = new();
    private readonly FileLogger _logger = new(Path.Combine(Path.GetTempPath(), "ITADLibrarySyncTests", Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task PrepareAsync_uses_known_catalog_slug()
    {
        _api
            .Setup(client => client.LookupShopGameIdsAsync(
                52,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["titanfall-2"] = "018d937f-0000-0000-0000-000000000001"
            });

        var preparer = new EaStoreSyncPayloadPreparer(_api.Object, _logger);
        var prepared = await preparer.PrepareAsync([
            new SyncGamePayload(52, "titanfall-2", "Titanfall 2")
        ]);

        prepared.Should().ContainSingle();
        prepared[0].Id.Should().Be("titanfall-2");
    }

    [Fact]
    public async Task PrepareAsync_uses_tracking_id_for_unknown_catalog_games()
    {
        _api
            .Setup(client => client.LookupShopGameIdsAsync(
                52,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());

        var preparer = new EaStoreSyncPayloadPreparer(_api.Object, _logger);
        var prepared = await preparer.PrepareAsync([
            new SyncGamePayload(52, "some-unknown-slug", "Unknown Game")
        ]);

        prepared.Should().ContainSingle();
        prepared[0].Id.Should().Be("itadlibsync/some-unknown-slug");
    }

    [Fact]
    public void FindKnownShopGameId_matches_case_insensitive_lookup_keys()
    {
        var lookup = new Dictionary<string, string?>
        {
            ["apex-legends"] = "018d937f-0000-0000-0000-000000000002"
        };

        EaStoreSyncPayloadPreparer.FindKnownShopGameId("Apex-Legends", lookup)
            .Should()
            .Be("apex-legends");
    }
}
