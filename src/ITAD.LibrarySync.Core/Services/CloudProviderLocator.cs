using System.Text.Json;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Detects the local sync-root folder of installed cloud sync clients
/// (OneDrive, Google Drive, Dropbox) without talking to any cloud API —
/// it only locates the folder the user's sync client already watches.
/// </summary>
public sealed class CloudProviderLocator : ICloudProviderLocator
{
    /// <summary>
    /// Localized names Google Drive for desktop uses for the "My Drive" folder
    /// (the mount is shown with the account/OS language, e.g. "Drive'ım" in Turkish).
    /// </summary>
    private static readonly string[] GoogleDriveMountNames =
    [
        "My Drive",
        "Google Drive",
        "Drive'ım",             // tr
        "Mein Laufwerk",        // de
        "Mon Drive",            // fr
        "Mi unidad",            // es
        "Il mio Drive",         // it
        "Meu Drive",            // pt
        "Mijn Drive",           // nl
        "Mój dysk",             // pl
        "Мой диск",             // ru
        "Мій диск",             // uk
        "Můj disk",             // cs
        "Mitt Drive",           // sv
        "マイドライブ",           // ja
        "내 드라이브",            // ko
        "我的云端硬盘"            // zh-CN
    ];

    private const string GoogleDriveMarkerFolder = ".shortcut-targets-by-id";

    private readonly Func<string, bool> _pathExists;
    private readonly Func<string, string?> _getEnv;
    private readonly Func<IEnumerable<string>> _getDriveRoots;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;

    public CloudProviderLocator(
        Func<string, bool> pathExists,
        Func<string, string?> getEnv,
        Func<IEnumerable<string>>? driveRoots = null,
        Func<string, IEnumerable<string>>? enumerateDirectories = null)
    {
        _pathExists = pathExists;
        _getEnv = getEnv;
        _getDriveRoots = driveRoots ?? GetReadyDriveRoots;
        _enumerateDirectories = enumerateDirectories ?? EnumerateTopLevelDirectories;
    }

    public static CloudProviderLocator CreateDefault() => new(
        Directory.Exists,
        Environment.GetEnvironmentVariable);

    /// <summary>All ready drive roots (e.g. "C:\", "G:\") — used to find cloud mounts exposed as drive letters.</summary>
    private static IEnumerable<string> GetReadyDriveRoots()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady)
                .Select(drive => drive.RootDirectory.FullName)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateTopLevelDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToList();
        }
        catch
        {
            return [];
        }
    }

    public IReadOnlyList<CloudProvider> GetAvailableProviders() =>
        Enum.GetValues<CloudProvider>()
            .Where(provider => GetCloudRoot(provider) is not null)
            .ToList();

    public string? GetCloudRoot(CloudProvider provider) => provider switch
    {
        CloudProvider.OneDrive => ResolveOneDriveRoot(),
        CloudProvider.GoogleDrive => ResolveGoogleDriveRoot(),
        CloudProvider.Dropbox => ResolveDropboxRoot(),
        _ => null
    };

    private string? ResolveOneDriveRoot()
    {
        // The OneDrive client sets the "OneDrive" environment variable per user.
        var env = _getEnv("OneDrive");
        if (!string.IsNullOrWhiteSpace(env) && _pathExists(env))
            return env;

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
        return _pathExists(fallback) ? fallback : null;
    }

    private string? ResolveGoogleDriveRoot()
    {
        // Google Drive for desktop mounts the drive under a folder whose name is
        // localized ("My Drive", "Drive'ım", "Mein Laufwerk", …); legacy
        // Backup & Sync used "Google Drive".
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var name in GoogleDriveMountNames)
        {
            var candidate = Path.Combine(userProfile, name);
            if (_pathExists(candidate))
                return candidate;
        }

        // The mount can also be exposed as a drive letter (e.g. "G:\Drive'ım").
        foreach (var drive in _getDriveRoots())
        {
            foreach (var name in GoogleDriveMountNames)
            {
                var candidate = Path.Combine(drive, name);
                if (_pathExists(candidate))
                    return candidate;
            }
        }

        // Language-agnostic fallback: Google Drive for desktop keeps a hidden
        // ".shortcut-targets-by-id" folder inside My Drive regardless of the
        // display language.
        foreach (var drive in _getDriveRoots())
        {
            foreach (var directory in _enumerateDirectories(drive))
            {
                var marker = Path.Combine(directory, GoogleDriveMarkerFolder);
                if (_pathExists(marker))
                    return directory;
            }
        }

        return null;
    }

    private string? ResolveDropboxRoot()
    {
        // Dropbox writes its sync root into %LOCALAPPDATA%\Dropbox\info.json.
        try
        {
            var infoPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dropbox",
                "info.json");
            if (File.Exists(infoPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(infoPath));
                var root = document.RootElement;
                if (root.TryGetProperty("personal", out var personal) &&
                    personal.TryGetProperty("path", out var path) &&
                    path.ValueKind == JsonValueKind.String)
                {
                    var resolved = path.GetString();
                    if (!string.IsNullOrWhiteSpace(resolved) && _pathExists(resolved))
                        return resolved;
                }
            }
        }
        catch
        {
            // Fall through to the conventional path below.
        }

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox");
        return _pathExists(fallback) ? fallback : null;
    }
}
