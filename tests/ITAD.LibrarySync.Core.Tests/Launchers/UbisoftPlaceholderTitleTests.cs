using FluentAssertions;
using ITAD.LibrarySync.Core.Launchers.Ubisoft;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class UbisoftPlaceholderTitleTests
{
    [Theory]
    [InlineData("l1")]
    [InlineData("L1")]
    [InlineData("GAMENAME")]
    [InlineData("name")]
    [InlineData("l9")]
    public void IsPlaceholderTitle_DetectsBrokenUbisoftCacheTitles(string title)
    {
        UbisoftLocalLibraryReader.IsPlaceholderTitle(title).Should().BeTrue();
    }

    [Theory]
    [InlineData("Far Cry 6")]
    [InlineData("Rayman Origins")]
    [InlineData("ACOD")]
    public void IsPlaceholderTitle_AllowsRealTitles(string title)
    {
        UbisoftLocalLibraryReader.IsPlaceholderTitle(title).Should().BeFalse();
    }
}
