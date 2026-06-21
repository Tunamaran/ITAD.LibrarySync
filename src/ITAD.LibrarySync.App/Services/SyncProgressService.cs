using System.Windows;
using System.Windows.Threading;
using ITAD.LibrarySync.App.ViewModels;
using ITAD.LibrarySync.App.Views;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.Services;

public sealed class SyncProgressService(FileLogger logger)
{
    private SyncProgressWindow? _window;
    private SyncProgressViewModel? _viewModel;

    public void BeginSync()
    {
        logger.EntryWritten += OnEntryWritten;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _window?.Close();
            _viewModel = new SyncProgressViewModel();
            _window = new SyncProgressWindow(_viewModel)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            _window.Closed += (_, _) =>
            {
                _window = null;
                _viewModel = null;
            };
            _window.Show();
        });
    }

    public void CompleteSync(IReadOnlyList<SyncResult> results)
    {
        logger.EntryWritten -= OnEntryWritten;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _viewModel?.Complete(results);
            _window?.Focus();
        });
    }

    public void FailSync(string message)
    {
        logger.EntryWritten -= OnEntryWritten;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _viewModel?.Fail(message);
            _window?.Focus();
        });
    }

    private void OnEntryWritten(SyncLogEntry entry)
    {
        if (_viewModel is null)
            return;

        Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                _viewModel.AddEntry(entry);
                _window?.ScrollToLatest();
            });
    }
}
