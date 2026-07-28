using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Scheduling;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed class LogLevelOption(AppLogLevel level, string langKey)
{
    public AppLogLevel Level { get; } = level;
    public string LangKey { get; } = langKey;

    public string DisplayName => LanguageManager.Instance[LangKey];

    public override string ToString() => DisplayName;
}
