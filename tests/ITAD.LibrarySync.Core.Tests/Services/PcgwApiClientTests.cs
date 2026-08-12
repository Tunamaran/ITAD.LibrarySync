using System.Net;
using System.Text;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class PcgwApiClientTests
{
    private const string SearchJson = """
        {"batchcomplete":true,"query":{"search":[{"ns":0,"title":"Terraria","pageid":149}]}}
        """;

    private const string SectionsJson = """
        {"parse":{"title":"Terraria","pageid":149,"sections":[{"toclevel":2,"level":"3","line":"Save game data location","number":"3.2","index":"6","fromtitle":"Terraria","byteoffset":4713,"anchor":"Save_game_data_location"}]}}
        """;

    private const string WikitextJson = """
        {"parse":{"title":"Terraria","pageid":149,"wikitext":"===Save game data location===\n{{Game data|\n{{Game data/saves|Windows|{{p|userprofile\\Documents}}\\My Games\\Terraria\\}}\n}}"}}
        """;

    [Fact]
    public async Task LookupSavePathAsync_CompleteFlow_ReturnsParsedResult()
    {
        var handler = new FakeHttpMessageHandler(url => url.Contains("list=search")
            ? SearchJson
            : url.Contains("prop=sections")
                ? SectionsJson
                : WikitextJson);

        using var client = new HttpClient(handler);
        var api = new PcgwApiClient(client, requestPacing: TimeSpan.Zero);

        var result = await api.LookupSavePathAsync("Terraria");

        Assert.NotNull(result);
        Assert.Equal("Terraria", result.PageTitle);
        Assert.Equal(@"%USERPROFILE%\Documents\My Games\Terraria", result.SavePath);
        Assert.Equal("https://www.pcgamingwiki.com/wiki/Terraria", result.SourceUrl);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task LookupSavePathAsync_NoSearchResults_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(_ => """{"batchcomplete":true,"query":{"search":[]}}""");
        using var client = new HttpClient(handler);
        var api = new PcgwApiClient(client, requestPacing: TimeSpan.Zero);

        var result = await api.LookupSavePathAsync("Totally Unknown Game");

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task LookupSavePathAsync_MissingSaveSection_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(url => url.Contains("list=search")
            ? SearchJson
            : """{"parse":{"title":"Terraria","pageid":149,"sections":[]}}""");
        using var client = new HttpClient(handler);
        var api = new PcgwApiClient(client, requestPacing: TimeSpan.Zero);

        var result = await api.LookupSavePathAsync("Terraria");

        Assert.Null(result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task LookupSavePathAsync_ServerError_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(_ => "error", statusCode: HttpStatusCode.InternalServerError);
        using var client = new HttpClient(handler);
        var api = new PcgwApiClient(client, requestPacing: TimeSpan.Zero);

        var result = await api.LookupSavePathAsync("Terraria");

        Assert.Null(result);
    }

    [Fact]
    public async Task LookupSavePathAsync_UnresolvableWindowsPath_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(url => url.Contains("list=search")
            ? SearchJson
            : url.Contains("prop=sections")
                ? SectionsJson
                : """{"parse":{"title":"Terraria","pageid":149,"wikitext":"{{Game data/saves|Windows|{{p|unknown_custom_placeholder}}\\userdata\\}}"}}""");
        using var client = new HttpClient(handler);
        var api = new PcgwApiClient(client, requestPacing: TimeSpan.Zero);

        var result = await api.LookupSavePathAsync("Terraria");

        Assert.Null(result);
    }

    private sealed class FakeHttpMessageHandler(
        Func<string, string> jsonByUrl,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonByUrl(request.RequestUri!.AbsoluteUri), Encoding.UTF8, "application/json")
            });
        }
    }
}
