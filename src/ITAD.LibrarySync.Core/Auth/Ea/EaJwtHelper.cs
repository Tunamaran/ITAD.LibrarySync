using System.Text;
using System.Text.Json;

namespace ITAD.LibrarySync.Core.Auth.Ea;

internal static class EaJwtHelper
{
    internal static DateTimeOffset? TryGetExpiry(string jwt)
    {
        var payload = TryReadPayload(jwt);
        if (payload is null)
            return null;

        if (payload.Value.TryGetProperty("exp", out var exp) &&
            exp.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    internal static EaSessionInfo? TryGetSessionInfo(string jwt)
    {
        var payload = TryReadPayload(jwt);
        if (payload is null || !payload.Value.TryGetProperty("nexus", out var nexus))
            return null;

        var userId = ReadString(nexus, "pid");
        var personaId = ReadString(nexus, "psid");
        var displayName = ReadDisplayName(nexus);
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return new EaSessionInfo(
            userId,
            personaId ?? string.Empty,
            displayName ?? userId);
    }

    private static string? ReadDisplayName(JsonElement nexus)
    {
        if (!nexus.TryGetProperty("psif", out var profiles) || profiles.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var profile in profiles.EnumerateArray())
        {
            var displayName = ReadString(profile, "dis");
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement? TryReadPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=')
            .Replace('-', '+')
            .Replace('_', '/');
        return Convert.FromBase64String(padded);
    }
}
