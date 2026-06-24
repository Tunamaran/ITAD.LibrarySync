using GameFinder.RegistryUtils;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaClientDetection
{
    private const string AllUsersFolderName = "530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e";
    private const string InstallInfoFileName = "IS";

    private static readonly (RegistryHive Hive, string SubKey, string ValueName)[] ClientRegistryValues =
    [
        (RegistryHive.LocalMachine, @"SOFTWARE\Electronic Arts\EA Desktop", "ClientPath"),
        (RegistryHive.LocalMachine, @"SOFTWARE\Electronic Arts\EA Desktop", "DesktopAppPath"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop", "ClientPath"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop", "DesktopAppPath")
    ];

    internal static AbsolutePath ResolveClientPath(
        AbsolutePath handlerClientPath,
        IFileSystem fileSystem,
        IRegistry registry)
    {
        var normalized = LauncherClientDetection.NormalizeClientPath(handlerClientPath, fileSystem);
        if (normalized != default)
            return normalized;

        foreach (var (hive, subKey, valueName) in ClientRegistryValues)
        {
            if (!TryReadRegistryString(registry, hive, subKey, valueName, out var rawPath))
                continue;

            var candidate = LauncherClientDetection.NormalizeClientPath(
                fileSystem.FromUnsanitizedFullPath(rawPath),
                fileSystem);
            if (candidate != default)
                return candidate;
        }

        return default;
    }

    internal static bool IsEaInstalled(IFileSystem fileSystem, AbsolutePath clientPath) =>
        LauncherClientDetection.NormalizeClientPath(clientPath, fileSystem) != default ||
        fileSystem.FileExists(GetInstallInfoFile(fileSystem));

    internal static AbsolutePath GetInstallInfoFile(IFileSystem fileSystem) =>
        fileSystem
            .GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("EA Desktop")
            .Combine(AllUsersFolderName)
            .Combine(InstallInfoFileName);

    private static bool TryReadRegistryString(
        IRegistry registry,
        RegistryHive hive,
        string subKey,
        string valueName,
        out string rawPath)
    {
        rawPath = string.Empty;

        var baseKey = registry.OpenBaseKey(hive, RegistryView.Default);
        var key = baseKey.OpenSubKey(subKey);
        if (key is null)
            return false;

        return key.TryGetString(valueName, out rawPath) && !string.IsNullOrWhiteSpace(rawPath);
    }
}
