using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Ea;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class EaStoreIdResolverTests
{
    [Fact]
    public void Resolve_PrefersBaseSlugOverOriginId()
    {
        EaStoreIdResolver.Resolve("Titanfall-2", "Origin.SFT.50.0000532")
            .Should()
            .Be("titanfall-2");
    }

    [Fact]
    public void Resolve_FallsBackToOriginIdWhenSlugMissing()
    {
        EaStoreIdResolver.Resolve(null, "Origin.SFT.50.0000532")
            .Should()
            .Be("Origin.SFT.50.0000532");
    }

    [Fact]
    public void Resolve_ReturnsNullWhenBothMissing()
    {
        EaStoreIdResolver.Resolve(" ", "")
            .Should()
            .BeNull();
    }
}
