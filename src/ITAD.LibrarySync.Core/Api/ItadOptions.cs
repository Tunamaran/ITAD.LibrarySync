namespace ITAD.LibrarySync.Core.Api;

public sealed class ItadOptions
{
    public const string BaseUrl = "https://api.isthereanydeal.com";
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
}
