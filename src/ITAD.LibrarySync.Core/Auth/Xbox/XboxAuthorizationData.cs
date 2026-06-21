namespace ITAD.LibrarySync.Core.Auth.Xbox;

public sealed class XboxAuthorizationData
{
    public required string Token { get; init; }
    public required XboxDisplayClaims DisplayClaims { get; init; }
}

public sealed class XboxDisplayClaims
{
    public required IReadOnlyList<XboxXuiClaim> Xui { get; init; }
}

public sealed class XboxXuiClaim
{
    public required string Xid { get; init; }
    public required string Uhs { get; init; }
    public string? Gtg { get; init; }
}
