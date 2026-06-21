using System.Runtime.Versioning;
using System.Windows;
using ITAD.LibrarySync.App.Views;
using ITAD.LibrarySync.Core.Auth.Xbox;

namespace ITAD.LibrarySync.App.Launchers;

[SupportedOSPlatform("windows")]
public sealed class XboxOAuthFlowService(XboxOAuthService oauthService)
{
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var authorizeUrl = oauthService.BuildAuthorizeUrl();
        var redirectUri = XboxOAuthOptions.CreateDefault().RedirectUri;

        var code = await RunOAuthDialogAsync(authorizeUrl, redirectUri, ct);
        await oauthService.ExchangeCodeAsync(code, ct);
    }

    private static async Task<string> RunOAuthDialogAsync(
        string authorizeUrl,
        string redirectUri,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = new OAuthWebViewWindow(authorizeUrl, redirectUri);

            window.AuthorizationCodeReceived += (_, e) => tcs.TrySetResult(e.Code);
            window.ErrorOccurred += (_, e) =>
                tcs.TrySetException(e.Exception ?? new InvalidOperationException(e.Message));
            window.Closed += (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetCanceled();
            };

            window.ShowDialog();
        });

        return await tcs.Task.ConfigureAwait(false);
    }
}
