using FluentAssertions;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;
using Moq;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class SyncOrchestratorTests
{
    private readonly Mock<IItadApiClient> _api = new();
    private readonly ShopIdResolver _shopIds = new();
    private readonly Mock<ICollectionSyncService> _collectionSync = new();
    private readonly Mock<IWaitlistSyncService> _waitlistSync = new();
    private readonly Mock<IWaitlistCleanupService> _waitlistCleanup = new();
    private readonly Mock<IDelayProvider> _delay = new();

    public SyncOrchestratorTests()
    {
        _api.Setup(x => x.GetShopMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        _waitlistCleanup
            .Setup(x => x.RemoveOwnedFromGlobalWaitlistAsync(It.IsAny<IReadOnlyList<StoreGame>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _delay
            .Setup(x => x.DelayAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SyncAllAsync_SkipsCollectionSync_WhenOwnedEmpty()
    {
        var reader = CreateReader(LauncherId.Epic, EmptyRead(LauncherId.Epic));
        var orchestrator = CreateOrchestrator(reader.Object);

        await orchestrator.SyncAllAsync();

        _collectionSync.Verify(
            x => x.SyncAsync(It.IsAny<LauncherReadResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAllAsync_InvokesWaitlistSync_WhenWishlistReadable()
    {
        var read = new LauncherReadResult(
            LauncherId.Epic,
            IsDetected: true,
            IsLoggedIn: true,
            Owned: [new(LauncherId.Epic, "e1", "Hades")],
            Wishlist:
            [
                new(LauncherId.Epic, "w1", "Hades"),
                new(LauncherId.Epic, "w2", "Disco Elysium")
            ],
            WishlistReadable: true);

        var reader = CreateReader(LauncherId.Epic, read);
        _waitlistSync
            .Setup(x => x.SyncAsync(read, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItadSyncResponse(1, 0, 0));

        var orchestrator = CreateOrchestrator(reader.Object);

        await orchestrator.SyncAllAsync();

        _waitlistSync.Verify(
            x => x.SyncAsync(
                It.Is<LauncherReadResult>(r =>
                    r.Wishlist.Count == 2 &&
                    r.Owned.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_RunsGlobalCleanup_AfterAllLaunchers()
    {
        var epicOwned = new StoreGame(LauncherId.Epic, "e1", "Hades");
        var ubisoftOwned = new StoreGame(LauncherId.Ubisoft, "u1", "Far Cry");

        var readers = new[]
        {
            CreateReader(LauncherId.Epic, SuccessfulRead(LauncherId.Epic, [epicOwned])),
            CreateReader(LauncherId.Ubisoft, SuccessfulRead(LauncherId.Ubisoft, [ubisoftOwned]))
        };

        _collectionSync
            .Setup(x => x.SyncAsync(It.IsAny<LauncherReadResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItadSyncResponse(1, 0, 0));

        _waitlistCleanup
            .Setup(x => x.RemoveOwnedFromGlobalWaitlistAsync(It.IsAny<IReadOnlyList<StoreGame>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var orchestrator = CreateOrchestrator(
            readers[0].Object,
            readers[1].Object);

        var results = await orchestrator.SyncAllAsync();

        _waitlistCleanup.Verify(
            x => x.RemoveOwnedFromGlobalWaitlistAsync(
                It.Is<IReadOnlyList<StoreGame>>(owned =>
                    owned.Count == 2 &&
                    owned.Any(g => g.StoreId == "e1") &&
                    owned.Any(g => g.StoreId == "u1")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        results.Should().HaveCount(2);
        results[0].GlobalWaitlistRemoved.Should().Be(3);
        results[1].GlobalWaitlistRemoved.Should().Be(0);
    }

    [Fact]
    public async Task SyncAllAsync_ContinuesOnPartialFailure()
    {
        var failingReader = new Mock<ILauncherReader>();
        failingReader.Setup(x => x.Launcher).Returns(LauncherId.Epic);
        failingReader
            .Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Epic read failed"));

        var succeedingOwned = new StoreGame(LauncherId.Ubisoft, "u1", "Far Cry");
        var succeedingReader = CreateReader(
            LauncherId.Ubisoft,
            SuccessfulRead(LauncherId.Ubisoft, [succeedingOwned]));

        _collectionSync
            .Setup(x => x.SyncAsync(It.IsAny<LauncherReadResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItadSyncResponse(1, 1, 0));

        var orchestrator = CreateOrchestrator(failingReader.Object, succeedingReader.Object);

        var results = await orchestrator.SyncAllAsync();

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Launcher == LauncherId.Epic && !r.Success && r.Error == "Epic read failed");
        results.Should().Contain(r => r.Launcher == LauncherId.Ubisoft && r.Success);

        _waitlistCleanup.Verify(
            x => x.RemoveOwnedFromGlobalWaitlistAsync(
                It.Is<IReadOnlyList<StoreGame>>(owned => owned.Count == 1 && owned[0].StoreId == "u1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_DelaysBetweenLaunchers_NotAfterLast()
    {
        var readers = new[]
        {
            CreateReader(LauncherId.Epic, SuccessfulRead(LauncherId.Epic, [new(LauncherId.Epic, "e1", "Hades")])),
            CreateReader(LauncherId.Ubisoft, SuccessfulRead(LauncherId.Ubisoft, [new(LauncherId.Ubisoft, "u1", "Far Cry")])),
            CreateReader(LauncherId.BattleNet, SuccessfulRead(LauncherId.BattleNet, [new(LauncherId.BattleNet, "b1", "Diablo")]))
        };

        _collectionSync
            .Setup(x => x.SyncAsync(It.IsAny<LauncherReadResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItadSyncResponse(1, 0, 0));

        var orchestrator = CreateOrchestrator(
            readers[0].Object,
            readers[1].Object,
            readers[2].Object);

        await orchestrator.SyncAllAsync();

        _delay.Verify(
            x => x.DelayAsync(TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private SyncOrchestrator CreateOrchestrator(params ILauncherReader[] readers) =>
        new(
            _api.Object,
            _shopIds,
            readers,
            _collectionSync.Object,
            _waitlistSync.Object,
            _waitlistCleanup.Object,
            _delay.Object);

    private static Mock<ILauncherReader> CreateReader(LauncherId launcher, LauncherReadResult result)
    {
        var reader = new Mock<ILauncherReader>();
        reader.Setup(x => x.Launcher).Returns(launcher);
        reader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return reader;
    }

    private static LauncherReadResult EmptyRead(LauncherId launcher) =>
        new(launcher, true, true, [], [], WishlistReadable: true);

    private static LauncherReadResult SuccessfulRead(LauncherId launcher, IReadOnlyList<StoreGame> owned) =>
        new(launcher, true, true, owned, [], WishlistReadable: false);
}
