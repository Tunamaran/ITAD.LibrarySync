using System.Text.Json;
using System.Text.Json.Serialization;
using ITAD.LibrarySync.Core.Models;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaInstallInfoLibraryReader
{
    private const string AllUsersFolderName = "530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e";
    private static readonly string[] EncryptedCategories = ["IS", "CATS2"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    internal static IReadOnlyList<StoreGame> TryReadOwnedGames(IFileSystem fileSystem)
    {
        var dataFolder = fileSystem
            .GetKnownPath(KnownPath.CommonApplicationDataDirectory)
            .Combine("EA Desktop")
            .Combine(AllUsersFolderName);

        foreach (var provider in EaHardwareCandidateFactory.CreateCandidates())
        {
            foreach (var category in EncryptedCategories)
            {
                var encryptedFile = dataFolder.Combine(category);
                if (!EaInstallInfoDecryptor.TryDecrypt(fileSystem, encryptedFile, provider, out var plaintext))
                    continue;

                var games = ParseOwnedGames(plaintext);
                if (games.Count > 0)
                    return games;
            }
        }

        return [];
    }

    private static IReadOnlyList<StoreGame> ParseOwnedGames(string plaintext)
    {
        var document = JsonSerializer.Deserialize<InstallInfoDocument>(plaintext, JsonOptions);
        if (document?.InstallInfos is null || document.InstallInfos.Count == 0)
            return [];

        var games = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.InstallInfos)
        {
            if (!string.IsNullOrWhiteSpace(entry.BaseGame) &&
                !string.Equals(entry.BaseGame, "False", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var storeId = EaStoreIdResolver.Resolve(entry.BaseSlug, entry.SoftwareId ?? string.Empty);
            if (string.IsNullOrWhiteSpace(storeId))
                continue;

            var title = !string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DisplayName.Trim()
                : FormatSlugTitle(entry.BaseSlug) ?? entry.SoftwareId ?? storeId;

            games[storeId] = new StoreGame(LauncherId.Ea, storeId, title);
        }

        return games
            .Values
            .OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? FormatSlugTitle(string? baseSlug)
    {
        if (string.IsNullOrWhiteSpace(baseSlug))
            return null;

        return baseSlug
            .Replace('-', ' ')
            .Replace('_', ' ');
    }

    private sealed record InstallInfoDocument(
        List<InstallInfoEntry>? InstallInfos,
        SchemaEntry? Schema);

    private sealed record InstallInfoEntry(
        string? BaseInstallPath,
        string? BaseSlug,
        [property: JsonPropertyName("softwareId")] string? SoftwareId,
        string? BaseGame,
        string? DisplayName);

    private sealed record SchemaEntry(int Version);
}
