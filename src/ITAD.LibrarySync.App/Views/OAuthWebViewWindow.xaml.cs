using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ITAD.LibrarySync.App.Views;

public partial class OAuthWebViewWindow : Window
{
    private readonly string _authorizeUrl;
    private readonly string _redirectUriPrefix;

    public event EventHandler<AuthorizationCodeReceivedEventArgs>? AuthorizationCodeReceived;
    public event EventHandler<OAuthErrorEventArgs>? ErrorOccurred;

    public OAuthWebViewWindow(string authorizeUrl, string redirectUri)
    {
        _authorizeUrl = authorizeUrl;
        _redirectUriPrefix = NormalizeRedirectPrefix(redirectUri);

        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await OAuthWebView.EnsureCoreWebView2Async();
            OAuthWebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            OAuthWebView.Source = new Uri(_authorizeUrl);
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
                ? $"ITAD authorization failed: {error}."
                : $"ITAD authorization failed: {error} ({errorDescription}).";
            ErrorOccurred?.Invoke(this, new OAuthErrorEventArgs(message));
            Close();
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorOccurred?.Invoke(this, new OAuthErrorEventArgs("ITAD authorization callback did not include an authorization code."));
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

        if (!uri.StartsWith(_redirectUriPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var queryIndex = uri.IndexOf('?', _redirectUriPrefix.Length - 1);
        if (queryIndex < 0)
            return true;

        var query = uri[(queryIndex + 1)..];
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
