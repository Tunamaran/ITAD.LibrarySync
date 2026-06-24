using System.Net.Http;
using System.Windows;
using ITAD.LibrarySync.Core.Auth.Ea;
using ITAD.LibrarySync.Core.Launchers.Ea;

namespace EaOAuthProbe;

public static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        MainAsync(args).GetAwaiter().GetResult();

    private static async Task<int> MainAsync(string[] args)
    {
        var loginRequested = args.Contains("--login", StringComparer.OrdinalIgnoreCase);
        var useWebLogin = args.Contains("--login-web", StringComparer.OrdinalIgnoreCase);
        var httpClient = new HttpClient();
        var options = loginRequested || useWebLogin
            ? (useWebLogin ? EaOAuthOptions.CreateWebFallback() : EaOAuthOptions.CreateDefault())
            : EaOAuthOptions.CreateWebFallback();
        var storage = new EaTokenStorage();
        var oauth = new EaOAuthService(httpClient, options, storage);
        var juno = new EaJunoClient(httpClient, options, oauth);

        var app = new App();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            if (loginRequested || useWebLogin || !oauth.HasStoredLogin())
            {
                if (!loginRequested && !useWebLogin && !oauth.HasStoredLogin())
                {
                    Console.WriteLine("No EA session stored. Run with --login or --login-web to sign in.");
                    return 1;
                }

                string authorizeUrl;
                string codeVerifier;

                if (useWebLogin)
                {
                    codeVerifier = EaOAuthService.GenerateCodeVerifier();
                    authorizeUrl = oauth.BuildWebAuthorizeUrl();
                    Console.WriteLine("Using EA web login fallback (EADOTCOM-WEB-SERVER).");
                }
                else
                {
                    var pcSign = EaPcSignGenerator.Generate();
                    codeVerifier = EaOAuthService.GenerateCodeVerifier();
                    Console.WriteLine($"pc_sign length: {pcSign.Length}");
                    authorizeUrl = oauth.BuildAuthorizeUrl(pcSign);
                }

                var code = await EaLoginWindow.GetAuthorizationCodeAsync(authorizeUrl, options.RedirectUri);
                await oauth.ExchangeCodeAsync(code, codeVerifier);
                Console.WriteLine("EA login successful.");
            }
            else
            {
                await oauth.GetValidAccessTokenAsync();
            }

            var session = oauth.GetStoredSession();
            Console.WriteLine($"Account: {session?.DisplayName ?? "(unknown)"}");

            var entitlements = await juno.GetOwnedEntitlementsAsync();
            var games = EaJunoOwnedGamesMapper.Map(entitlements);
            Console.WriteLine($"Owned={games.Count} (raw entitlements={entitlements.Count})");
            foreach (var game in games.Take(25))
                Console.WriteLine($"  {game.Title} ({game.StoreId})");

            return games.Count > 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EaOAuthProbe failed: {ex.Message}");
            return 1;
        }
    }
}
