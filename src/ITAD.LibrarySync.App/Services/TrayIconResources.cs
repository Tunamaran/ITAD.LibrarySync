using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;

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
        // 1. Try loading from WPF Resource assembly stream (pack URI)
        try
        {
            var resourceUri = new Uri($"pack://application:,,,/Assets/Icons/{fileName}", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(resourceUri);
            if (streamInfo?.Stream != null)
            {
                using var stream = streamInfo.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
            // Ignore WPF resource load error and proceed to file fallback
        }

        // 2. Try loading from local file system
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", fileName);
        if (File.Exists(path))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new Icon(stream);
        }

        // 3. Fallback: extract icon directly from the main running EXE binary
        try
        {
            var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(mainModulePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(mainModulePath);
                if (extracted != null) return extracted;
            }
        }
        catch
        {
            // Ignore
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
