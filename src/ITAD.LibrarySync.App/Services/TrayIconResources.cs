using System.Drawing;
using System.IO;
using System.Runtime.Versioning;

namespace ITAD.LibrarySync.App.Services;

[SupportedOSPlatform("windows")]
internal static class TrayIconResources
{
    private static readonly Lazy<Icon> Idle = new(() => Load("tray-idle.ico"));
    private static readonly Lazy<Icon> Syncing = new(() => Load("tray-syncing.ico"));
    private static readonly Lazy<Icon> Success = new(() => Load("tray-success.ico"));
    private static readonly Lazy<Icon> Partial = new(() => Load("tray-partial.ico"));
    private static readonly Lazy<Icon> Error = new(() => Load("tray-error.ico"));

    public static Icon GetIcon(TraySyncState state) => state switch
    {
        TraySyncState.Syncing => Syncing.Value,
        TraySyncState.Success => Success.Value,
        TraySyncState.Partial => Partial.Value,
        TraySyncState.Error => Error.Value,
        _ => Idle.Value
    };

    private static Icon Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", fileName);
        if (!File.Exists(path))
            return (Icon)SystemIcons.Application.Clone();

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new Icon(stream);
    }
}
