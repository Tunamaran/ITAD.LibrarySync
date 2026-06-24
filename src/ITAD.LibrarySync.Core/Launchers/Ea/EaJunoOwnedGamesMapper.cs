using System.Text.Json;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

public static class EaJunoOwnedGamesMapper
{
    public static IReadOnlyList<StoreGame> Map(IReadOnlyList<JsonElement> entitlements)
    {
        var bestBySlug = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var withoutSlug = new List<JsonElement>();

        foreach (var entitlement in entitlements)
        {
            if (!TryGetProduct(entitlement, out var product))
                continue;

            if (IsTrial(product) || IsDlc(product))
                continue;

            var slug = ReadString(product, "gameSlug")
                       ?? ReadNestedString(product, "baseItem", "baseGameSlug");
            if (string.IsNullOrWhiteSpace(slug))
            {
                withoutSlug.Add(entitlement);
                continue;
            }

            if (!bestBySlug.TryGetValue(slug, out var existing))
            {
                bestBySlug[slug] = entitlement;
                continue;
            }

            if (ShouldReplace(existing, entitlement))
                bestBySlug[slug] = entitlement;
        }

        var games = new Dictionary<string, StoreGame>(StringComparer.OrdinalIgnoreCase);
        foreach (var entitlement in bestBySlug.Values.Concat(withoutSlug))
        {
            if (!TryGetProduct(entitlement, out var product))
                continue;

            var title = ReadString(product, "name")
                        ?? ReadNestedString(product, "baseItem", "title")
                        ?? ReadString(entitlement, "id")
                        ?? "Unknown EA game";
            var originOfferId = ReadString(entitlement, "id");
            var slug = ReadString(product, "gameSlug")
                       ?? ReadNestedString(product, "baseItem", "baseGameSlug");
            var storeId = EaStoreIdResolver.Resolve(slug, originOfferId ?? string.Empty);
            if (string.IsNullOrWhiteSpace(storeId))
                continue;

            games[storeId] = new StoreGame(LauncherId.Ea, storeId, title.Trim());
        }

        return games.Values
            .OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldReplace(JsonElement existing, JsonElement candidate)
    {
        if (!TryGetProduct(existing, out var existingProduct) ||
            !TryGetProduct(candidate, out var candidateProduct))
        {
            return false;
        }

        var existingDownloadable = ReadBool(existingProduct, "downloadable");
        var candidateDownloadable = ReadBool(candidateProduct, "downloadable");
        if (candidateDownloadable && !existingDownloadable)
            return true;

        if (candidateDownloadable != existingDownloadable)
            return false;

        var existingDate = ReadNestedString(existingProduct, "gameProductUser", "initialEntitlementDate") ?? string.Empty;
        var candidateDate = ReadNestedString(candidateProduct, "gameProductUser", "initialEntitlementDate") ?? string.Empty;
        return string.CompareOrdinal(candidateDate, existingDate) < 0;
    }

    private static bool IsTrial(JsonElement product) =>
        product.TryGetProperty("trialDetails", out var trial) &&
        trial.ValueKind == JsonValueKind.Object &&
        trial.TryGetProperty("trialType", out var trialType) &&
        trialType.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(trialType.GetString());

    private static bool IsDlc(JsonElement product)
    {
        var gameType = ReadNestedString(product, "baseItem", "gameType");
        return string.Equals(gameType, "extra_content", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(gameType, "expansion", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetProduct(JsonElement entitlement, out JsonElement product)
    {
        if (entitlement.TryGetProperty("product", out product) && product.ValueKind == JsonValueKind.Object)
            return true;

        product = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadNestedString(JsonElement element, string objectName, string propertyName) =>
        element.TryGetProperty(objectName, out var child) && child.ValueKind == JsonValueKind.Object
            ? ReadString(child, propertyName)
            : null;

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();
}
