using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Scheduling;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed class SyncIntervalOption(SyncInterval interval, string langKey)
{
    public SyncInterval Interval { get; } = interval;
    public string LangKey { get; } = langKey;

    public string DisplayName => LanguageManager.Instance[LangKey];

    public override string ToString() => DisplayName;
}
