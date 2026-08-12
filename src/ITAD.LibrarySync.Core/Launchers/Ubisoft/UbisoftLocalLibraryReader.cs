using System.Runtime.Versioning;
using System.Text;
using ITAD.LibrarySync.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ITAD.LibrarySync.Core.Launchers.Ubisoft;

[SupportedOSPlatform("windows")]
internal static class UbisoftLocalLibraryReader
{
    private static readonly HashSet<string> BlacklistedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "gamename", "l1", "", "ubisoft game", "name"
    };

    internal static bool IsPlaceholderTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        var trimmed = title.Trim();
        if (BlacklistedNames.Contains(trimmed))
            return true;

        return trimmed.Length is >= 2 and <= 3 &&
               (trimmed[0] == 'l' || trimmed[0] == 'L') &&
               trimmed[1..].All(char.IsDigit);
    }

    internal static IReadOnlyList<StoreGame> ReadOwnedGames()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ubisoft Game Launcher",
            "cache");

        var configurationsPath = Path.Combine(basePath, "configuration", "configurations");
        var ownershipPath = ResolveOwnershipPath(Path.Combine(basePath, "ownership"));

        if (!File.Exists(configurationsPath) || ownershipPath is null || !File.Exists(ownershipPath))
            return [];

        var configurationBytes = File.ReadAllBytes(configurationsPath);
        var ownershipBytes = File.ReadAllBytes(ownershipPath);

        var ownedIds = UbisoftBinaryParser.ParseOwnedIds(ownershipBytes);
        if (ownedIds.Count == 0)
            return [];

        var configurationRecords = UbisoftBinaryParser.ParseConfigurationRecords(configurationBytes);
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var games = new List<StoreGame>();

        foreach (var record in configurationRecords)
        {
            if (!ownedIds.Contains(record.InstallId) && !ownedIds.Contains(record.LaunchId))
                continue;

            if (record.YamlOffset < 0 ||
                record.YamlSize <= 0 ||
                record.YamlOffset + record.YamlSize > configurationBytes.Length)
            {
                continue;
            }

            var yaml = Encoding.UTF8.GetString(
                configurationBytes,
                record.YamlOffset,
                record.YamlSize);

            if (!yaml.Contains("start_game", StringComparison.Ordinal))
                continue;

            if (!TryParseOwnedGame(yaml, knownIds, out var game))
                continue;

            games.Add(game);
        }

        return games;
    }

    private static string? ResolveOwnershipPath(string ownershipDirectory)
    {
        if (!Directory.Exists(ownershipDirectory))
            return null;

        return Directory.EnumerateFiles(ownershipDirectory)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool TryParseOwnedGame(string yaml, HashSet<string> knownIds, out StoreGame game)
    {
        game = null!;

        UbisoftConfigurationFile config;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            config = deserializer.Deserialize<UbisoftConfigurationFile>(yaml.Replace('\t', ' '));
        }
        catch
        {
            return false;
        }

        if (config.root is null)
            return false;

        if (config.root.third_party_platform is not null)
            return false;

        var title = ResolveTitle(config);
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var storeId = ResolveStoreId(config, title);
        if (string.IsNullOrWhiteSpace(storeId))
            return false;

        if (!knownIds.Add(storeId))
            return false;

        game = new StoreGame(LauncherId.Ubisoft, storeId, title);
        return true;
    }

    private static string ResolveTitle(UbisoftConfigurationFile config)
    {
        var title = config.root?.display_name ?? config.root?.name ?? string.Empty;
        if (config.localizations?.Default is { } localization)
            title = Localize(title, localization);

        if (BlacklistedNames.Contains(title.Trim()) &&
            config.localizations?.Default?.gamename is { } localizedGameName &&
            !IsPlaceholderTitle(localizedGameName))
        {
            title = localizedGameName;
        }

        if (IsPlaceholderTitle(title) &&
            !string.IsNullOrWhiteSpace(config.root?.uplay?.game_code) &&
            !IsPlaceholderTitle(config.root.uplay.game_code))
        {
            title = config.root.uplay.game_code;
        }

        title = title.Trim();
        return IsPlaceholderTitle(title) ? string.Empty : title;
    }

    private static string ResolveStoreId(UbisoftConfigurationFile config, string title)
    {
        var iconFile = config.root?.icon_image ?? config.root?.thumb_image ?? string.Empty;
        if (config.localizations?.Default is { } iconLocalization)
            iconFile = Localize(iconFile, iconLocalization);

        if (!string.IsNullOrWhiteSpace(iconFile) &&
            (iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
             iconFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
             iconFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
        {
            return Path.GetFileNameWithoutExtension(iconFile);
        }

        if (!string.IsNullOrWhiteSpace(config.root.uplay?.game_code))
            return config.root.uplay.game_code;

        if (!string.IsNullOrWhiteSpace(config.root.uplay?.achievements_sync_id))
            return config.root.uplay.achievements_sync_id;

        return new string(title.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string Localize(string key, UbisoftConfigurationLanguage language) =>
        key switch
        {
            "NAME" => language.name ?? key,
            "GAMENAME" => language.gamename ?? key,
            "ICONIMAGE" => language.iconimage ?? key,
            "THUMBIMAGE" => language.thumbimage ?? key,
            _ => key
        };
}
