using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace EaOAuthProbe;

public partial class EaLoginWindow : Window
{
    private const string EaDesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Origin/10.6.0.00000 EAApp/13.680.0.6193 Chrome/109.0.5414.120 Safari/537.36";

    private readonly string _authorizeUrl;
    private readonly string _redirectUri;
    private bool _completed;
    private readonly TaskCompletionSource<string> _codeTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EaLoginWindow(string authorizeUrl, string redirectUri)
    {
        _authorizeUrl = authorizeUrl;
        _redirectUri = redirectUri;
        InitializeComponent();
        Loaded += OnLoadedAsync;
        Closed += (_, _) =>
        {
            if (!_completed)
                _codeTcs.TrySetCanceled();
        };
    }

    public static Task<string> GetAuthorizationCodeAsync(string authorizeUrl, string redirectUri)
    {
        var window = new EaLoginWindow(authorizeUrl, redirectUri);
        window.ShowDialog();
        return window._codeTcs.Task;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ITADLibrarySync",
                "EaOAuthWebView");
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);

            await LoginWebView.EnsureCoreWebView2Async(environment);

            var core = LoginWebView.CoreWebView2
                       ?? throw new InvalidOperationException("WebView2 failed to initialize.");

            core.Settings.UserAgent = EaDesktopUserAgent;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            core.ProcessFailed += (_, args) =>
                SetStatus($"WebView process failed: {args.ProcessFailedKind}");

            SetStatus("Opening EA sign-in...");
            core.Navigate(_authorizeUrl);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load EA sign-in: {ex.Message}");
            _codeTcs.TrySetException(ex);
            Close();
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!TryParseCallback(e.Uri, out var code, out var error, out var errorDescription))
        {
            SetStatus("Continue signing in to EA...");
            return;
        }

        e.Cancel = true;
        Complete(code, error, errorDescription);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_completed || LoginWebView.CoreWebView2?.Source is null)
            return;

        if (!e.IsSuccess)
        {
            SetStatus($"EA page failed to load ({e.WebErrorStatus}). Check your connection and try again.");
            return;
        }

        TryComplete(LoginWebView.CoreWebView2.Source);
    }

    private void TryComplete(string uri)
    {
        if (!TryParseCallback(uri, out var code, out var error, out var errorDescription))
            return;

        Complete(code, error, errorDescription);
    }

    private void Complete(string? code, string? error, string? errorDescription)
    {
        if (_completed)
            return;

        _completed = true;

        if (!string.IsNullOrWhiteSpace(error))
        {
            var message = string.IsNullOrWhiteSpace(errorDescription)
                ? $"EA authorization failed: {error}."
                : $"EA authorization failed: {error} ({errorDescription}).";
            SetStatus(message);
            _codeTcs.TrySetException(new InvalidOperationException(message));
            Close();
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            const string message = "EA callback did not include an authorization code.";
            SetStatus(message);
            _codeTcs.TrySetException(new InvalidOperationException(message));
            Close();
            return;
        }

        SetStatus("EA sign-in complete.");
        _codeTcs.TrySetResult(code);
        Close();
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
        Console.WriteLine(message);
    }

    private bool TryParseCallback(string uri, out string? code, out string? error, out string? errorDescription)
    {
        code = null;
        error = null;
        errorDescription = null;

        if (!IsEaRedirect(uri))
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

    private bool IsEaRedirect(string uri)
    {
        if (uri.StartsWith("qrc:", StringComparison.OrdinalIgnoreCase))
            return uri.Contains("login_successful.html", StringComparison.OrdinalIgnoreCase);

        if (uri.StartsWith("https://www.ea.com/login_check", StringComparison.OrdinalIgnoreCase))
            return true;

        return uri.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase);
    }
}
