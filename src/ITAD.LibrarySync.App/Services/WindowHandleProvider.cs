using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ITAD.LibrarySync.Core.Launchers;

namespace ITAD.LibrarySync.App.Services;

public sealed class WindowHandleProvider : IWindowHandleProvider
{
    private IntPtr _handle;
    private Window? _helperWindow;

    public IntPtr Handle
    {
        get
        {
            EnsureInitialized();
            return _handle;
        }
    }

    public void EnsureInitialized()
    {
        if (_handle != IntPtr.Zero)
            return;

        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("WPF application is not running.");

        if (dispatcher.CheckAccess())
            _handle = ResolveHandle();
        else
            dispatcher.Invoke(() => _handle = ResolveHandle());
    }

    private IntPtr ResolveHandle()
    {
        var visibleWindow = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsLoaded && window.IsVisible && window != _helperWindow);

        if (visibleWindow is not null)
        {
            var existingHandle = new WindowInteropHelper(visibleWindow).Handle;
            if (existingHandle != IntPtr.Zero)
                return existingHandle;
        }

        _helperWindow ??= CreateHiddenHelperWindow();
        if (!_helperWindow.IsLoaded)
        {
            _helperWindow.Show();
            _helperWindow.Hide();
        }

        var handle = new WindowInteropHelper(_helperWindow).Handle;
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Unable to acquire a window handle for Microsoft Store APIs.");

        return handle;
    }

    private static Window CreateHiddenHelperWindow() =>
        new()
        {
            Width = 0,
            Height = 0,
            Left = -32000,
            Top = -32000,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };
}
