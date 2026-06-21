using System.Net;
using System.Text;
using FluentAssertions;
using ITAD.LibrarySync.Core.Auth.Xbox;

namespace ITAD.LibrarySync.Core.Tests.Auth.Xbox;

public class XboxOAuthServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly XboxTokenStorage _storage;
    private readonly XboxOAuthOptions _options;

    public XboxOAuthServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "xbox-oauth-test-" + Guid.NewGuid());
        _storage = new XboxTokenStorage(_tempDir);
        _options = XboxOAuthOptions.CreateDefault();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ExchangeCodeAsync_saves_login_and_xsts()
    {
        var handler = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"msa-access","refresh_token":"msa-refresh","expires_in":3600}""",
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"Token":"user-jwt-token"}""",
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "Token": "xsts-jwt-token",
                      "DisplayClaims": {
                        "xui": [
                          {
                            "xid": "2535412345678901",
                            "uhs": "user-hash",
                            "gtg": "TestGamer"
                          }
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"Token":"user-jwt-token"}""",
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "Token": "licensing-xsts-jwt-token",
                      "DisplayClaims": {
                        "xui": [
                          {
                            "xid": "2535412345678901",
                            "uhs": "user-hash",
                            "gtg": "TestGamer"
                          }
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var service = CreateService(handler);

        await service.ExchangeCodeAsync("auth-code", CancellationToken.None);

        var login = _storage.LoadLogin();
        login.Should().NotBeNull();
        login!.AccessToken.Should().Be("msa-access");
        login.RefreshToken.Should().Be("msa-refresh");
        login.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddSeconds(3600), TimeSpan.FromSeconds(5));

        var xsts = _storage.LoadXsts();
        xsts.Should().NotBeNull();
        xsts!.Token.Should().Be("xsts-jwt-token");
        xsts.DisplayClaims.Xui.Should().HaveCount(1);
        xsts.DisplayClaims.Xui[0].Xid.Should().Be("2535412345678901");
        xsts.DisplayClaims.Xui[0].Uhs.Should().Be("user-hash");
        xsts.DisplayClaims.Xui[0].Gtg.Should().Be("TestGamer");

        handler.Requests.Should().HaveCount(5);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Be("https://login.live.com/oauth20_token.srf");
        handler.Requests[1].RequestUri!.AbsoluteUri.Should().Be("https://user.auth.xboxlive.com/user/authenticate");
        handler.Requests[2].RequestUri!.AbsoluteUri.Should().Be("https://xsts.auth.xboxlive.com/xsts/authorize");
        handler.Requests[3].RequestUri!.AbsoluteUri.Should().Be("https://user.auth.xboxlive.com/user/authenticate");
        handler.Requests[4].RequestUri!.AbsoluteUri.Should().Be("https://xsts.auth.xboxlive.com/xsts/authorize");

        var userAuthBody = handler.Requests[1].Body;
        userAuthBody.Should().Contain("\"RelyingParty\"");
        userAuthBody.Should().Contain("\"RpsTicket\":\"d=msa-access\"");
    }

    [Fact]
    public async Task GetAuthorizationAsync_throws_when_no_login_saved()
    {
        var service = CreateService(new MockHttpMessageHandler());

        var act = () => service.GetAuthorizationAsync(CancellationToken.None);

        await act.Should().ThrowAsync<XboxAuthRequiredException>();
    }

    [Fact]
    public void BuildAuthorizationHeader_formats_correctly()
    {
        var auth = new XboxAuthorizationData
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

        var header = XboxOAuthService.BuildAuthorizationHeader(auth);

        header.Should().Be("XBL3.0 x=user-hash;xsts-jwt-token");
    }

    private XboxOAuthService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new XboxOAuthService(httpClient, _options, _storage);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public MockHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, body));

            if (_responses.Count == 0)
                throw new InvalidOperationException("No mock HTTP response configured.");

            return _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri, string? Body);
}
