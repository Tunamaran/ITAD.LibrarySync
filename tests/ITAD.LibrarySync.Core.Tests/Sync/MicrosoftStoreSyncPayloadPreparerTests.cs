using FluentAssertions;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class MicrosoftStoreSyncPayloadPreparerTests
{
    private readonly Mock<IItadApiClient> _api = new();
    private readonly FileLogger _logger = new(Path.Combine(Path.GetTempPath(), "ITADLibrarySyncTests", Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task PrepareAsync_uses_known_catalog_id_casing()
    {
        _api
            .Setup(client => client.LookupShopGameIdsAsync(
                48,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["9NKV34XDW014"] = "018d937f-50c1-7086-807c-e020c98c72b2"
            });

        var preparer = new MicrosoftStoreSyncPayloadPreparer(_api.Object, _logger);
        var prepared = await preparer.PrepareAsync([
            new SyncGamePayload(48, "9nkv34xdw014", "Palworld (Game Preview)")
        ]);

        prepared.Should().ContainSingle();
        prepared[0].Id.Should().Be("9NKV34XDW014");
    }

    [Fact]
    public async Task PrepareAsync_uses_tracking_id_for_unknown_catalog_games()
    {
        _api
            .Setup(client => client.LookupShopGameIdsAsync(
                48,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());

        var preparer = new MicrosoftStoreSyncPayloadPreparer(_api.Object, _logger);
        var prepared = await preparer.PrepareAsync([
            new SyncGamePayload(48, "9P44R51Z21WW", "Digital Monster: Ultimate Evolve")
        ]);

        prepared.Should().ContainSingle();
        prepared[0].Id.Should().Be("itadlibsync/9P44R51Z21WW");
    }

    [Fact]
    public void FindKnownShopGameId_matches_lowercase_lookup_keys()
    {
        var lookup = new Dictionary<string, string?>
        {
            ["9np6wl1xqdbw"] = "018d937f-3e66-70a2-bb81-04e55ec0da54"
        };

        MicrosoftStoreSyncPayloadPreparer.FindKnownShopGameId("9NP6WL1XQDBW", lookup)
            .Should()
            .Be("9np6wl1xqdbw");
    }
}
