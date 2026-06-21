using FluentAssertions;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class CollectionSyncFaultIsolatorTests
{
    private readonly Mock<IItadApiClient> _api = new();
    private readonly FileLogger _logger = new(Path.Combine(Path.GetTempPath(), "ITADLibrarySyncTests", Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task SyncCollectionAsync_skips_rejected_entries_and_syncs_the_rest()
    {
        var good = new SyncGamePayload(48, "9NBLGGH2JHXJ", "Minecraft for Windows");
        var bad = new SyncGamePayload(48, "9P44R51Z21WW", "Digital Monster: Ultimate Evolve");

        _api
            .SetupSequence(client => client.SyncCollectionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<SyncGamePayload>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItadSyncResponse(1, 1, 0))
            .ThrowsAsync(new HttpRequestException("ITAD API sync failed with 500 Internal Server Error"))
            .ReturnsAsync(new ItadSyncResponse(1, 0, 0));

        var isolator = new CollectionSyncFaultIsolator(_api.Object, _logger);
        var response = await isolator.SyncCollectionAsync(
            "token",
            "profile",
            [good, bad],
            "Microsoft",
            CancellationToken.None);

        response.Total.Should().Be(1);
        _api.Verify(
            client => client.SyncCollectionAsync(
                "token",
                "profile",
                It.Is<IReadOnlyList<SyncGamePayload>>(payloads => payloads.Count == 1 && payloads[0].Id == good.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _api.Verify(
            client => client.SyncCollectionAsync(
                "token",
                "profile",
                It.Is<IReadOnlyList<SyncGamePayload>>(payloads => payloads.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
