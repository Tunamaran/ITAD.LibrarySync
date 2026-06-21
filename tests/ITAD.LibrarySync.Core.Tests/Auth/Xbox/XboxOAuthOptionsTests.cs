using FluentAssertions;
using ITAD.LibrarySync.Core.Auth.Xbox;

namespace ITAD.LibrarySync.Core.Tests.Auth.Xbox;

public class XboxOAuthOptionsTests
{
    [Fact]
    public void DefaultScopes_match_playnite_desktop_flow()
    {
        var options = XboxOAuthOptions.CreateDefault();
        options.Scopes.Should().Be("Xboxlive.signin Xboxlive.offline_access");
    }

    [Fact]
    public void DefaultScopes_include_offline_access()
    {
        var options = XboxOAuthOptions.CreateDefault();
        Assert.Contains("offline_access", options.Scopes, StringComparison.OrdinalIgnoreCase);
    }
}
