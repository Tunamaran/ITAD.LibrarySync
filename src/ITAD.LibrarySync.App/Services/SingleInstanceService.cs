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
    private Task? _listenerTask;
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
        _listenerTask = Task.Run(() => ListenForActivation(_listenerCts.Token));
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

    private void ListenForActivation(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _activateEvent is not null)
        {
            try
            {
                var signaledHandle = WaitHandle.WaitAny([_activateEvent, ct.WaitHandle]);
                if (signaledHandle == 1)
                    break;

                if (signaledHandle == 0)
                    _onActivate?.Invoke();
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        var listenerCts = _listenerCts;
        var listenerTask = _listenerTask;
        listenerCts?.Cancel();

        // Wait for the listener to leave WaitAny before disposing its handles.
        // Disposing them first can surface an ObjectDisposedException during app exit.
        var listenerStopped = false;
        try
        {
            listenerStopped = listenerTask is null || listenerTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // A listener callback must not turn application shutdown into a crash.
            listenerStopped = true;
        }
        if (listenerStopped)
        {
            listenerCts?.Dispose();
            _activateEvent?.Dispose();
        }

        _listenerCts = null;
        _listenerTask = null;
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
