using FluentAssertions;
using GameCollector.StoreHandlers.EADesktop;
using GameCollector.StoreHandlers.EADesktop.Crypto;
using GameFinder.Common;
using ITAD.LibrarySync.Core.Launchers.Ea;
using Moq;
using OneOf;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class EaDecryptFailureDetectorTests
{
    [Fact]
    public void IsDecryptFailure_ReturnsFalseWhenGamesPresent()
    {
        var results = new OneOf<EADesktopGame, ErrorMessage>[]
        {
            default(EADesktopGame),
            new ErrorMessage("Exception while decrypting file")
        };

        EaDecryptFailureDetector.IsDecryptFailure(results).Should().BeFalse();
    }

    [Fact]
    public void IsDecryptFailure_ReturnsTrueForDecryptOnlyErrors()
    {
        var results = new OneOf<EADesktopGame, ErrorMessage>[]
        {
            new ErrorMessage("Exception while decrypting file C:/ProgramData/EA Desktop/IS")
        };

        EaDecryptFailureDetector.IsDecryptFailure(results).Should().BeTrue();
    }
}

public class GpuOverrideHardwareInfoProviderTests
{
    [Fact]
    public void GetVideoControllerDeviceId_ReturnsOverrideValue()
    {
        var inner = new Mock<IHardwareInfoProvider>();
        inner.Setup(provider => provider.GetVolumeSerialNumber()).Returns("ABC");

        var provider = new GpuOverrideHardwareInfoProvider(inner.Object, "GPU-OVERRIDE");

        provider.GetVideoControllerDeviceId().Should().Be("GPU-OVERRIDE");
        provider.GetVolumeSerialNumber().Should().Be("ABC");
    }
}
