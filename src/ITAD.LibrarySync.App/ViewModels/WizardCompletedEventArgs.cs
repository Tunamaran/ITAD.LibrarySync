namespace ITAD.LibrarySync.App.ViewModels;

public sealed class WizardCompletedEventArgs : EventArgs
{
    public required bool InitialSyncRan { get; init; }
}
