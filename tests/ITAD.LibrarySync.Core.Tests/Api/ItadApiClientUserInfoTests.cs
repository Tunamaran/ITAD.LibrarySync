using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Tests.Api;

public class ItadApiClientUserInfoTests
{
    [Fact]
    public async Task GetUserInfoAsync_ReturnsUsername()
    {
        var handler = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { username = "testplayer" })
            });

        var client = new ItadApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri(ItadOptions.BaseUrl)
        });

        var userInfo = await client.GetUserInfoAsync("access-token");

        userInfo.Username.Should().Be("testplayer");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/user/info/v2");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
