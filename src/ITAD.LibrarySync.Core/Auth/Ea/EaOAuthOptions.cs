namespace ITAD.LibrarySync.Core.Auth.Ea;

public sealed record EaOAuthOptions(
    string ClientId,
    string ClientSecret,
    string RedirectUri,
    string AuthorizeEndpoint,
    string TokenEndpoint,
    string GraphQlEndpoint,
    string JunoClientIdHeader,
    string Referer,
    string UserAgent)
{
    public static EaOAuthOptions CreateDefault() => new(
        ClientId: "JUNO_PC_CLIENT",
        ClientSecret: "4mRLtYMb6vq9qglomWEaT4ChxsXWcyqbQpuBNfMPOYOiDmYYQmjuaBsF2Zp0RyVeWkfqhE9TuGgAw7te",
        RedirectUri: "qrc:///html/login_successful.html",
        AuthorizeEndpoint: "https://accounts.ea.com/connect/auth",
        TokenEndpoint: "https://accounts.ea.com/connect/token",
        GraphQlEndpoint: "https://service-aggregation-layer.juno.ea.com/graphql",
        JunoClientIdHeader: "EAX-JUNO-CLIENT",
        Referer: "https://pc.ea.com/",
        UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Origin/10.6.0.00000 EAApp/13.680.0.6193 Chrome/109.0.5414.120 Safari/537.36");

    public static EaOAuthOptions CreateWebFallback() => new(
        ClientId: "EADOTCOM-WEB-SERVER",
        ClientSecret: string.Empty,
        RedirectUri: "https://www.ea.com/login_check",
        AuthorizeEndpoint: "https://accounts.ea.com/connect/auth",
        TokenEndpoint: "https://accounts.ea.com/connect/token",
        GraphQlEndpoint: "https://service-aggregation-layer.juno.ea.com/graphql",
        JunoClientIdHeader: "EAX-JUNO-CLIENT",
        Referer: "https://www.ea.com/",
        UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
}
