using System.Net;
using ITAD.LibrarySync.Core.Services;
using Xunit;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class UpdateCheckerServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsNoUpdate_WhenVersionsMatchOrResponseFails()
    {
        var handler = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new HttpClient(handler);

        var service = new UpdateCheckerService(client);
        var result = await service.CheckForUpdatesAsync();

        Assert.False(result.HasUpdate);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_DetectsNewerVersion_WhenGitHubHasNewerRelease()
    {
        var jsonResponse = """
        {
          "tag_name": "v99.0.0",
          "html_url": "https://github.com/Tunamaran/ITAD.LibrarySync/releases/tag/v99.0.0",
          "assets": [
            {
              "browser_download_url": "https://github.com/Tunamaran/ITAD.LibrarySync/releases/download/v99.0.0/ITAD.LibrarySync.exe"
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        var client = new HttpClient(handler);

        var service = new UpdateCheckerService(client);
        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.HasUpdate);
        Assert.Equal("v99.0.0", result.LatestVersion);
        Assert.Equal("https://github.com/Tunamaran/ITAD.LibrarySync/releases/tag/v99.0.0", result.ReleaseNotesUrl);
    }

    private sealed class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
