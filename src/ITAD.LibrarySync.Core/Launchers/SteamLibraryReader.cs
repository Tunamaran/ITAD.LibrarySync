using System.Runtime.Versioning;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

/// <summary>
/// Enumerates games physically installed through Steam by reading
/// <c>libraryfolders.vdf</c> and the <c>appmanifest_*.acf</c> files of every
/// library. Used ONLY by the Cloud Saves feature — Steam is not part of the
/// ITAD sync pipeline.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SteamLibraryReader
{
    private readonly string? _steamRootOverride;

    public SteamLibraryReader(string? steamRootOverride = null)
    {
        _steamRootOverride = steamRootOverride;
    }

    public async Task<IReadOnlyList<SteamInstalledGame>> GetInstalledGamesAsync(CancellationToken ct = default)
    {
        var root = _steamRootOverride ?? ResolveSteamRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return [];

        var libraryRoots = new List<string> { root };
        var libraryFoldersFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersFile))
        {
            try
            {
                var vdf = await File.ReadAllTextAsync(libraryFoldersFile, ct);
                libraryRoots.AddRange(SteamVdfParser.ParseLibraryFolders(vdf));
            }
            catch
            {
                // Fall back to the Steam root only.
            }
        }

        var games = new List<SteamInstalledGame>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var libraryRoot in libraryRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var appsDir = Path.Combine(libraryRoot, "steamapps");
            if (!Directory.Exists(appsDir))
                continue;

            // Materialize immediately: EnumerateFiles is lazy and an unreadable
            // directory would otherwise abort the whole Steam scan.
            IReadOnlyList<string> manifests;
            try
            {
                manifests = Directory
                    .EnumerateFiles(appsDir, "appmanifest_*.acf", SearchOption.TopDirectoryOnly)
                    .ToList();
            }
            catch
            {
                continue;
            }

            foreach (var manifestPath in manifests)
            {
                ct.ThrowIfCancellationRequested();

                SteamAppManifest? manifest;
                try
                {
                    manifest = SteamVdfParser.ParseAppManifest(await File.ReadAllTextAsync(manifestPath, ct));
                }
                catch
                {
                    continue;
                }

                if (manifest is null || !manifest.IsInstalled || !seen.Add(manifest.AppId))
                    continue;

                games.Add(new SteamInstalledGame(manifest.AppId, manifest.Title));
            }
        }

        return games;
    }

    /// <summary>
    /// Resolves the Steam install root: registry (HKCU\Software\Valve\Steam\SteamPath)
    /// first, then conventional install locations.
    /// </summary>
    internal static string? ResolveSteamRoot()
    {
        try
        {
            var registry = WindowsRegistry.Shared;
            var key = registry
                .OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
                .OpenSubKey(@"Software\Valve\Steam");
            if (key is not null &&
                key.TryGetString("SteamPath", out var steamPath) &&
                !string.IsNullOrWhiteSpace(steamPath) &&
                Directory.Exists(steamPath))
            {
                return steamPath;
            }
        }
        catch
        {
            // Fall through to conventional paths.
        }

        foreach (var candidate in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
                     @"C:\Steam",
                     @"D:\Steam"
                 })
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
