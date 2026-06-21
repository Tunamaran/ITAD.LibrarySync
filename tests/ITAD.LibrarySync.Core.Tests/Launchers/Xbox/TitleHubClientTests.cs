using System.Net;
using System.Text;
using FluentAssertions;
using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Launchers.Xbox;

namespace ITAD.LibrarySync.Core.Tests.Launchers.Xbox;

public class TitleHubClientTests
{
    private static readonly XboxAuthorizationData TestAuth = new()
    {
        Token = "xsts-jwt-token",
        DisplayClaims = new XboxDisplayClaims
        {
            Xui =
            [
                new XboxXuiClaim
                {
                    Xid = "2535412345678901",
                    Uhs = "user-hash",
                    Gtg = "TestGamer"
                }
            ]
        }
    };

    [Fact]
    public async Task GetTitleHistoryAsync_parses_fixture_json()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "xbox", "titlehistory.json");
        var fixtureJson = await File.ReadAllTextAsync(fixturePath);

        var handler = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fixtureJson, Encoding.UTF8, "application/json")
            });

        var client = CreateClient(handler);

        var titles = await client.GetTitleHistoryAsync(TestAuth, CancellationToken.None);

        titles.Should().HaveCount(3);
        titles[0].TitleId.Should().Be("1234567890");
        titles[0].Name.Should().Be("Halo Infinite");
        titles[0].Pfn.Should().Be("Microsoft.Halo_8wekyb3d8bbwe");
        titles[0].ModernTitleId.Should().Be("9NBLGGH4R2Q6");

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Be(
            "https://titlehub.xboxlive.com/users/xuid(2535412345678901)/titles/titlehistory/decoration/detail");
        handler.Requests[0].Headers.GetValues("x-xbl-contract-version").Single().Should().Be("2");
        handler.Requests[0].Headers.GetValues("Authorization").Single()
            .Should().Be("XBL3.0 x=user-hash;xsts-jwt-token");
    }

    [Fact]
    public async Task GetMinutesPlayedAsync_maps_titleId_to_minutes()
    {
        var responseJson = """
            {
              "statlistscollection": [
                {
                  "arrangebyfield": "xuid",
                  "arrangebyfieldid": "2535412345678901",
                  "stats": [
                    { "name": "MinutesPlayed", "titleid": "1234567890", "value": "150" },
                    { "name": "MinutesPlayed", "titleid": "9876543210", "value": "42" }
                  ]
                }
              ]
            }
            """;

        var handler = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var client = CreateClient(handler);

        var minutes = await client.GetMinutesPlayedAsync(
            TestAuth,
            ["1234567890", "9876543210"],
            CancellationToken.None);

        minutes.Should().HaveCount(2);
        minutes["1234567890"].Should().Be(150);
        minutes["9876543210"].Should().Be(42);

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().Be("https://userstats.xboxlive.com/batch");

        var body = handler.RequestBodies[0];
        body.Should().Contain("\"arrangebyfield\":\"xuid\"");
        body.Should().Contain("\"name\":\"MinutesPlayed\"");
        body.Should().Contain("\"titleId\":\"1234567890\"");
        body.Should().Contain("\"titleId\":\"9876543210\"");
        body.Should().Contain("\"xuids\":[\"2535412345678901\"]");
    }

    [Fact]
    public async Task GetMinutesPlayedAsync_returns_empty_for_no_title_ids()
    {
        var handler = new MockHttpMessageHandler();
        var client = CreateClient(handler);

        var minutes = await client.GetMinutesPlayedAsync(TestAuth, [], CancellationToken.None);

        minutes.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    private static TitleHubClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new TitleHubClient(httpClient);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public MockHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken));

            if (_responses.Count == 0)
                throw new InvalidOperationException("No mock HTTP response configured.");

            return _responses.Dequeue();
        }
    }
}
