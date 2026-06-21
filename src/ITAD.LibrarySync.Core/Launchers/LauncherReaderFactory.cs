using System.Runtime.Versioning;

namespace ITAD.LibrarySync.Core.Launchers;

public static class LauncherReaderFactory
{
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<ILauncherReader> CreateAll(
        IMicrosoftStoreLibraryReader? storeLibraryReader = null) =>
    [
        new EpicReader(),
        new UbisoftReader(),
        new BattleNetReader(),
        new XboxReader(storeLibraryReader)
    ];
}
