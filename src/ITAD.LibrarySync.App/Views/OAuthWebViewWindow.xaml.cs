using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ITAD.LibrarySync.App.Views;

public partial class OAuthWebViewWindow : Window
{
    private readonly string _authorizeUrl;
    private readonly string _redirectUri;

    public event EventHandler<AuthorizationCodeReceivedEventArgs>? AuthorizationCodeReceived;
    public event EventHandler<OAuthErrorEventArgs>? ErrorOccurred;

    public OAuthWebViewWindow(string authorizeUrl, string redirectUri, string? title = null)
    {
        _authorizeUrl = authorizeUrl;
        _redirectUri = redirectUri;

        if (!string.IsNullOrWhiteSpace(title))
            Title = title;

        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ITADLibrarySync",
                "OAuthWebView");
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);

            await OAuthWebView.EnsureCoreWebView2Async(environment);

            var core = OAuthWebView.CoreWebView2
                       ?? throw new InvalidOperationException("WebView2 failed to initialize.");

            core.NavigationStarting += OnNavigationStarting;
            core.Navigate(_authorizeUrl);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new OAuthErrorEventArgs(ex.Message, ex));
            Close();
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!TryParseCallback(e.Uri, out var code, out var error, out var errorDescription))
            return;

        e.Cancel = true;

        if (!string.IsNullOrEmpty(error))
        {
            var message = string.IsNullOrWhiteSpace(errorDescription)
                ? $"Authorization failed: {error}."
                : $"Authorization failed: {error} ({errorDescription}).";
            ErrorOccurred?.Invoke(this, new OAuthErrorEventArgs(message));
            Close();
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorOccurred?.Invoke(this, new OAuthErrorEventArgs("Authorization callback did not include an authorization code."));
            Close();
            return;
        }

        AuthorizationCodeReceived?.Invoke(this, new AuthorizationCodeReceivedEventArgs(code));
        Close();
    }

    private bool TryParseCallback(string uri, out string? code, out string? error, out string? errorDescription)
    {
        code = null;
        error = null;
        errorDescription = null;

        if (!IsRedirectUri(uri))
            return false;

        var query = ExtractQueryString(uri);
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = Uri.UnescapeDataString(part[..separator]);
            var value = Uri.UnescapeDataString(part[(separator + 1)..]);

            switch (key)
            {
                case "code":
                    code = value;
                    break;
                case "error":
                    error = value;
                    break;
                case "error_description":
                    errorDescription = value;
                    break;
            }
        }

        return true;
    }

    private bool IsRedirectUri(string uri)
    {
        if (_redirectUri.StartsWith("qrc:", StringComparison.OrdinalIgnoreCase))
        {
            return uri.StartsWith("qrc:", StringComparison.OrdinalIgnoreCase) &&
                   uri.Contains("login_successful.html", StringComparison.OrdinalIgnoreCase);
        }

        if (uri.StartsWith("https://www.ea.com/login_check", StringComparison.OrdinalIgnoreCase))
            return true;

        return uri.StartsWith(NormalizeRedirectPrefix(_redirectUri), StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractQueryString(string uri)
    {
        var queryIndex = uri.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
            return uri[(queryIndex + 1)..];

        var fragmentIndex = uri.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
            return uri[(fragmentIndex + 1)..];

        return string.Empty;
    }

    private static string NormalizeRedirectPrefix(string redirectUri)
    {
        var uri = new Uri(redirectUri);
        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{uri.Scheme}://{uri.Authority}{path}";
    }
}

public sealed class AuthorizationCodeReceivedEventArgs(string code) : EventArgs
{
    public string Code { get; } = code;
}

public sealed class OAuthErrorEventArgs(string message, Exception? exception = null) : EventArgs
{
    public string Message { get; } = message;

    public Exception? Exception { get; } = exception;
}
