using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Xbox;
using ITAD.LibrarySync.Core.Models;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Launchers.Xbox;

public class XboxStoreIdNormalizerTests
{
    private readonly Mock<IMicrosoftStoreCatalogClient> _catalog = new();

    [Fact]
    public async Task NormalizeAsync_keeps_product_ids()
    {
        var normalizer = new XboxStoreIdNormalizer(_catalog.Object);
        var games = new[]
        {
            new StoreGame(LauncherId.Xbox, "9NBLGGH4R2Q6", "Halo Infinite"),
            new StoreGame(LauncherId.Xbox, "9NBLGGH4R2Q7", "Forza Horizon 5")
        };

        var normalized = await normalizer.NormalizeAsync(games);

        normalized.Should().HaveCount(2);
        normalized.Select(game => game.StoreId).Should().BeEquivalentTo("9NBLGGH4R2Q6", "9NBLGGH4R2Q7");
        _catalog.Verify(
            client => client.ResolveStoreIdsByPfnAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NormalizeAsync_resolves_pfns_to_product_ids()
    {
        _catalog
            .Setup(client => client.ResolveStoreIdsByPfnAsync(
                It.Is<IReadOnlyList<string>>(pfns => pfns.Single() == "Microsoft.Halo_8wekyb3d8bbwe"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Microsoft.Halo_8wekyb3d8bbwe"] = "9NBLGGH4R2Q6"
            });

        var normalizer = new XboxStoreIdNormalizer(_catalog.Object);
        var games = new[]
        {
            new StoreGame(LauncherId.Xbox, "Microsoft.Halo_8wekyb3d8bbwe", "Halo Infinite")
        };

        var normalized = await normalizer.NormalizeAsync(games);

        normalized.Should().ContainSingle();
        normalized[0].StoreId.Should().Be("9NBLGGH4R2Q6");
        normalized[0].Title.Should().Be("Halo Infinite");
    }

    [Fact]
    public async Task NormalizeAsync_drops_legacy_prefixed_ids()
    {
        var normalizer = new XboxStoreIdNormalizer(_catalog.Object);
        var games = new[]
        {
            new StoreGame(LauncherId.Xbox, "xbox:123456789", "Legacy Game"),
            new StoreGame(LauncherId.Xbox, "9NBLGGH4R2Q6", "Halo Infinite")
        };

        var normalized = await normalizer.NormalizeAsync(games);

        normalized.Should().ContainSingle();
        normalized[0].StoreId.Should().Be("9NBLGGH4R2Q6");
    }

    [Fact]
    public async Task NormalizeAsync_deduplicates_after_pfn_resolution()
    {
        _catalog
            .Setup(client => client.ResolveStoreIdsByPfnAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Microsoft.Halo_8wekyb3d8bbwe"] = "9NBLGGH4R2Q6"
            });

        var normalizer = new XboxStoreIdNormalizer(_catalog.Object);
        var games = new[]
        {
            new StoreGame(LauncherId.Xbox, "9NBLGGH4R2Q6", "Halo Infinite"),
            new StoreGame(LauncherId.Xbox, "Microsoft.Halo_8wekyb3d8bbwe", "Halo Infinite")
        };

        var normalized = await normalizer.NormalizeAsync(games);

        normalized.Should().ContainSingle();
        normalized[0].StoreId.Should().Be("9NBLGGH4R2Q6");
    }
}
