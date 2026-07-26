using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class SyncProgressViewModel : ObservableObject
{
    public ObservableCollection<string> Lines { get; } = [];

    public LanguageManager Lang => LanguageManager.Instance;

    public SyncProgressViewModel()
    {
        _statusText = Lang["SyncProgressSyncing"];
    }

    [ObservableProperty]
    private string _statusText = string.Empty;

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
            StatusText = Lang["SyncProgressNothing"];
            return;
        }

        var successes = results.Count(r => r.Success);
        StatusText = successes == results.Count
            ? string.Format(Lang["SyncProgressSuccess"], successes, results.Count)
            : successes == 0
                ? Lang["SyncProgressFailed"]
                : string.Format(Lang["SyncProgressPartial"], successes, results.Count);
    }

    public void Fail(string message)
    {
        IsRunning = false;
        CanClose = true;
        StatusText = Lang["SyncProgressFailed"];
        AddEntry(new SyncLogEntry(DateTimeOffset.Now, "ERROR", message));
    }
}
