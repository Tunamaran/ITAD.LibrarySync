using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using ITAD.LibrarySync.App.Views;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;

namespace ITAD.LibrarySync.App.Services;

[SupportedOSPlatform("windows")]
public sealed class OAuthFlowService(ItadOAuthService oauth, ItadOptions options)
{
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var redirectUri = new Uri(options.RedirectUri);
        var listenerPrefix = BuildListenerPrefix(redirectUri);
        var expectedState = GenerateState();

        using var listener = new HttpListener();
        listener.Prefixes.Add(listenerPrefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                $"Unable to start OAuth callback listener on {listenerPrefix}. Ensure port {redirectUri.Port} is available.",
                ex);
        }

        var codeVerifier = GenerateCodeVerifier();
        var authorizeUrl = oauth.BuildAuthorizeUrl(expectedState, codeVerifier);
        var codeCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listenerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var listenerTask = WaitForCallbackAsync(
            listener,
            expectedState,
            codeCompletion,
            listenerCts.Token);

        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowOAuthWindow(authorizeUrl, options.RedirectUri, codeCompletion));

            var code = await codeCompletion.Task.WaitAsync(ct);
            await oauth.ExchangeCodeAsync(code, ct);
        }
        finally
        {
            listenerCts.Cancel();
            if (listener.IsListening)
                listener.Stop();

            try
            {
                await listenerTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static void ShowOAuthWindow(
        string authorizeUrl,
        string redirectUri,
        TaskCompletionSource<string> codeCompletion)
    {
        var window = new OAuthWebViewWindow(authorizeUrl, redirectUri);

        window.AuthorizationCodeReceived += (_, args) => codeCompletion.TrySetResult(args.Code);
        window.ErrorOccurred += (_, args) =>
            codeCompletion.TrySetException(args.Exception ?? new InvalidOperationException(args.Message));
        window.Closed += (_, _) =>
        {
            if (!codeCompletion.Task.IsCompleted)
                codeCompletion.TrySetException(new OperationCanceledException("ITAD authorization was cancelled."));
        };

        window.ShowDialog();
    }

    private static async Task WaitForCallbackAsync(
        HttpListener listener,
        string expectedState,
        TaskCompletionSource<string> codeCompletion,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync().WaitAsync(ct);
            var request = context.Request;
            var response = context.Response;

            try
            {
                var code = request.QueryString["code"];
                var state = request.QueryString["state"];
                var error = request.QueryString["error"];
                var errorDescription = request.QueryString["error_description"];

                if (!string.IsNullOrEmpty(error))
                {
                    var message = string.IsNullOrWhiteSpace(errorDescription)
                        ? $"ITAD authorization failed: {error}."
                        : $"ITAD authorization failed: {error} ({errorDescription}).";
                    await WriteResponseAsync(response, HttpStatusCode.BadRequest, message);
                    codeCompletion.TrySetException(new InvalidOperationException(message));
                    return;
                }

                if (!string.Equals(state, expectedState, StringComparison.Ordinal))
                {
                    const string message = "ITAD authorization callback state did not match.";
                    await WriteResponseAsync(response, HttpStatusCode.BadRequest, message);
                    codeCompletion.TrySetException(new InvalidOperationException(message));
                    return;
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    const string message = "ITAD authorization callback did not include an authorization code.";
                    await WriteResponseAsync(response, HttpStatusCode.BadRequest, message);
                    codeCompletion.TrySetException(new InvalidOperationException(message));
                    return;
                }

                await WriteResponseAsync(
                    response,
                    HttpStatusCode.OK,
                    "<html><body><h2>Connected to IsThereAnyDeal</h2><p>You can close this window.</p></body></html>");

                codeCompletion.TrySetResult(code);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                codeCompletion.TrySetException(ex);
                throw;
            }
        }
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string body)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/html; charset=utf-8";
        var buffer = Encoding.UTF8.GetBytes(body);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private static string BuildListenerPrefix(Uri redirectUri)
    {
        var path = redirectUri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        return $"{redirectUri.Scheme}://{redirectUri.Authority}{path}/";
    }

    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
