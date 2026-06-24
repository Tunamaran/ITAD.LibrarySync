using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Ea;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class EaReadErrorFormatterTests
{
    [Fact]
    public void Format_ReturnsFriendlyMessageForDecryptFailures()
    {
        EaReadErrorFormatter.Format(new InvalidOperationException("Failed to decrypt EA library file"))
            .Should()
            .Contain("EA App library sync is unavailable on this PC");
    }

    [Fact]
    public void FormatFromReadError_ReturnsFriendlyMessageForWrappedDecryptFailures()
    {
        EaReadErrorFormatter.FormatFromReadError(
                "Unable to read library: Exception while decrypting file C:/ProgramData/EA Desktop/IS")
            .Should()
            .Contain("EA App library sync is unavailable on this PC");
    }

    [Fact]
    public void IsDecryptOrHardwareFailureMessage_DetectsDecryptKeywords()
    {
        EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage("Failed to decrypt local library file")
            .Should()
            .BeTrue();
    }
}
