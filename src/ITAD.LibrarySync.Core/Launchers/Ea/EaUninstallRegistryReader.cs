using System.Runtime.Versioning;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

[SupportedOSPlatform("windows")]
internal static class EaUninstallRegistryReader
{
    private static readonly string[] PublisherMarkers =
    [
        "Electronic Arts",
        "EA Swiss",
        "EA Canada",
        "BioWare",
        "Respawn",
        "DICE",
        "Maxis",
        "Criterion"
    ];

    internal static IReadOnlyList<StoreGame> ReadInstalledGames()
    {
        var registry = WindowsRegistry.Shared;
        var games = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var hiveView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var root = registry.OpenBaseKey(RegistryHive.LocalMachine, hiveView)
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (root is null)
                continue;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                var entry = root.OpenSubKey(subKeyName);
                if (entry is null)
                    continue;

                if (!entry.TryGetString("DisplayName", out var displayName) ||
                    string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                if (!entry.TryGetString("Publisher", out var publisher) ||
                    !PublisherMatches(publisher))
                {
                    continue;
                }

                var storeId = ResolveStoreId(entry, subKeyName, displayName);
                if (string.IsNullOrWhiteSpace(storeId))
                    continue;

                games[storeId] = new StoreGame(LauncherId.Ea, storeId, displayName.Trim());
            }
        }

        return games.Values
            .OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool PublisherMatches(string publisher) =>
        PublisherMarkers.Any(marker =>
            publisher.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveStoreId(IRegistryKey entry, string subKeyName, string displayName)
    {
        foreach (var valueName in new[] { "BundleManifestUrl", "HelpLink", "URLInfoAbout", "InstallLocation" })
        {
            if (!entry.TryGetString(valueName, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            var slug = TryExtractSlugFromUrl(value);
            if (slug is not null)
                return slug;
        }

        if (entry.TryGetString("DisplayIcon", out var iconPath) &&
            !string.IsNullOrWhiteSpace(iconPath))
        {
            var slugFromPath = TryExtractSlugFromPath(iconPath);
            if (slugFromPath is not null)
                return slugFromPath;
        }

        return Slugify(displayName);
    }

    private static string? TryExtractSlugFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("games", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(segments[i + 1]))
            {
                return segments[i + 1].ToLowerInvariant();
            }
        }

        return null;
    }

    private static string? TryExtractSlugFromPath(string path)
    {
        var parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Equals("EA Games", StringComparison.OrdinalIgnoreCase) &&
                parts.Length > Array.IndexOf(parts, part) + 1)
            {
                return Slugify(parts[Array.IndexOf(parts, part) + 1]);
            }
        }

        return null;
    }

    private static string Slugify(string value) =>
        value.Trim()
            .Replace(':', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Aggregate(string.Empty, (current, word) => current + (current.Length > 0 ? "-" : string.Empty) + word.ToLowerInvariant());
}
