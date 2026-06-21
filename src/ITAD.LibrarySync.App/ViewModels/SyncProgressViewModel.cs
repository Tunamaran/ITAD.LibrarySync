using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class SyncProgressViewModel : ObservableObject
{
    public ObservableCollection<string> Lines { get; } = [];

    [ObservableProperty]
    private string _statusText = "Syncing libraries…";

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private bool _canClose;

    public void AddEntry(SyncLogEntry entry) =>
        Lines.Add($"{entry.Timestamp:HH:mm:ss} [{entry.Level}] {entry.Message}");

    public void Complete(IReadOnlyList<SyncResult> results)
    {
        IsRunning = false;
        CanClose = true;

        if (results.Count == 0)
        {
            StatusText = "Sync finished — nothing to sync.";
            return;
        }

        var successes = results.Count(r => r.Success);
        StatusText = successes == results.Count
            ? $"Sync completed — {successes}/{results.Count} launcher(s) succeeded."
            : successes == 0
                ? "Sync failed — see log for details."
                : $"Sync completed with errors — {successes}/{results.Count} launcher(s) succeeded.";
    }

    public void Fail(string message)
    {
        IsRunning = false;
        CanClose = true;
        StatusText = "Sync failed — see log for details.";
        AddEntry(new SyncLogEntry(DateTimeOffset.Now, "ERROR", message));
    }
}
