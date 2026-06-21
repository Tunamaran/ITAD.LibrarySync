using System.Runtime.Versioning;

namespace ITAD.LibrarySync.App.Services;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\ITADLibrarySync.SingleInstance.v1";
    private const string ActivateEventName = "Local\\ITADLibrarySync.Activate.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private CancellationTokenSource? _listenerCts;
    private Action? _onActivate;

    public bool TryBecomePrimary(Action onActivate)
    {
        _onActivate = onActivate;
        _mutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            SignalExistingInstance();
            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _listenerCts = new CancellationTokenSource();
        _ = ListenForActivationAsync(_listenerCts.Token);
        return true;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
            activate.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private async Task ListenForActivationAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _activateEvent is not null)
        {
            try
            {
                var signaled = await Task.Run(
                    () => _activateEvent.WaitOne(TimeSpan.FromMilliseconds(500)),
                    ct);

                if (signaled)
                    _onActivate?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _listenerCts = null;
        _activateEvent?.Dispose();
        _activateEvent = null;

        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
        _mutex = null;
    }
}
