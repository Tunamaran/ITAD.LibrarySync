using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.Services;

/// <summary>
/// Service that tracks live sync progress and streams log messages for inline UI rendering.
/// Automatic background scheduled syncs run silently without popping up any window.
/// </summary>
public sealed partial class SyncProgressService : ObservableObject
{
    private readonly FileLogger _logger;

    public ObservableCollection<string> Lines { get; } = [];

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private bool _hasLastResult;

    [ObservableProperty]
    private string _lastResultSummary = string.Empty;

    public SyncProgressService(FileLogger logger)
    {
        _logger = logger;
        _statusText = Lang["SyncProgressIdle"];
    }

    public void BeginSync()
    {
        _logger.EntryWritten += OnEntryWritten;

        Application.Current.Dispatcher.Invoke(() =>
        {
            Lines.Clear();
            IsSyncing = true;
            HasLastResult = false;
            StatusText = Lang["SyncProgressSyncing"];
        });
    }

    public void CompleteSync(IReadOnlyList<SyncResult> results)
    {
        _logger.EntryWritten -= OnEntryWritten;

        Application.Current.Dispatcher.Invoke(() =>
        {
            IsSyncing = false;
            HasLastResult = true;

            if (results.Count == 0)
            {
                StatusText = Lang["SyncProgressNothing"];
                LastResultSummary = Lang["SyncProgressNothing"];
                return;
            }

            var successes = results.Count(r => r.Success);
            var total = results.Count;

            if (successes == total)
            {
                StatusText = string.Format(Lang["SyncProgressSuccess"], successes, total);
            }
            else if (successes == 0)
            {
                StatusText = Lang["SyncProgressFailed"];
            }
            else
            {
                StatusText = string.Format(Lang["SyncProgressPartial"], successes, total);
            }

            LastResultSummary = StatusText;
        });
    }

    public void FailSync(string message)
    {
        _logger.EntryWritten -= OnEntryWritten;

        Application.Current.Dispatcher.Invoke(() =>
        {
            IsSyncing = false;
            HasLastResult = true;
            StatusText = Lang["SyncProgressFailed"];
            LastResultSummary = message;
            AddEntryInternal(new SyncLogEntry(DateTimeOffset.Now, "ERROR", message));
        });
    }

    private void OnEntryWritten(SyncLogEntry entry)
    {
        Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => AddEntryInternal(entry));
    }

    private void AddEntryInternal(SyncLogEntry entry)
    {
        Lines.Add($"{entry.Timestamp:HH:mm:ss} [{entry.Level}] {entry.Message}");
    }
}
