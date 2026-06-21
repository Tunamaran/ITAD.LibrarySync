using FluentAssertions;
using ITAD.LibrarySync.Core.Auth.Xbox;

namespace ITAD.LibrarySync.Core.Tests.Auth.Xbox;

public class XboxTokenStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly XboxTokenStorage _storage;

    public XboxTokenStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "xbox-token-test-" + Guid.NewGuid());
        _storage = new XboxTokenStorage(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void SaveLogin_LoadLogin_round_trip()
    {
        var tokens = new XboxOAuthTokens(
            "access-token",
            "refresh-token",
            new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            "user-123");

        _storage.SaveLogin(tokens);
        var loaded = _storage.LoadLogin();

        loaded.Should().NotBeNull();
        loaded.Should().Be(tokens);
    }

    [Fact]
    public void SaveXsts_LoadXsts_round_trip()
    {
        var data = new XboxAuthorizationData
        {
            Token = "xsts-jwt-token",
            DisplayClaims = new XboxDisplayClaims
            {
                Xui = new List<XboxXuiClaim>
                {
                    new()
                    {
                        Xid = "2535412345678901",
                        Uhs = "user-hash",
                        Gtg = "TestGamer"
                    }
                }
            }
        };

        _storage.SaveXsts(data);
        var loaded = _storage.LoadXsts();

        loaded.Should().NotBeNull();
        loaded!.Token.Should().Be(data.Token);
        loaded.DisplayClaims.Xui.Should().HaveCount(1);
        loaded.DisplayClaims.Xui[0].Xid.Should().Be("2535412345678901");
        loaded.DisplayClaims.Xui[0].Uhs.Should().Be("user-hash");
        loaded.DisplayClaims.Xui[0].Gtg.Should().Be("TestGamer");
    }

    [Fact]
    public void ClearAll_removes_both_files()
    {
        _storage.SaveLogin(new XboxOAuthTokens("a", "r", DateTimeOffset.UtcNow, null));
        _storage.SaveXsts(new XboxAuthorizationData
        {
            Token = "t",
            DisplayClaims = new XboxDisplayClaims
            {
                Xui = new List<XboxXuiClaim> { new() { Xid = "1", Uhs = "u" } }
            }
        });

        _storage.ClearAll();

        _storage.LoadLogin().Should().BeNull();
        _storage.LoadXsts().Should().BeNull();
        File.Exists(Path.Combine(_tempDir, "xbox-login.dat")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "xbox-xsts.dat")).Should().BeFalse();
    }
}
