using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Xbox;

namespace ITAD.LibrarySync.Core.Tests.Launchers.Xbox;

public class MicrosoftStoreIdTests
{
    [Theory]
    [InlineData("9NBLGGH4R2Q6", true)]
    [InlineData("9N1234567890", true)]
    [InlineData("Microsoft.Halo_8wekyb3d8bbwe", false)]
    [InlineData("xbox:123456789", false)]
    [InlineData("", false)]
    public void IsProductId_detects_big_ids(string value, bool expected) =>
        MicrosoftStoreId.IsProductId(value).Should().Be(expected);

    [Theory]
    [InlineData("Microsoft.Halo_8wekyb3d8bbwe", true)]
    [InlineData("9NBLGGH4R2Q6", false)]
    [InlineData("xbox:123", false)]
    public void IsPackageFamilyName_detects_pfns(string value, bool expected) =>
        MicrosoftStoreId.IsPackageFamilyName(value).Should().Be(expected);

    [Theory]
    [InlineData("xbox:123456789", true)]
    [InlineData("9NBLGGH4R2Q6", false)]
    public void IsLegacyPrefixedTitleId_detects_prefixed_ids(string value, bool expected) =>
        MicrosoftStoreId.IsLegacyPrefixedTitleId(value).Should().Be(expected);
}
