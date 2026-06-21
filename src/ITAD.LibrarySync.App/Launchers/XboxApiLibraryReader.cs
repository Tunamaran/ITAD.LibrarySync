using System.Net.Http;
using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Launchers.Xbox;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.Launchers;

public sealed class XboxApiLibraryReader(
    StoreLicenseFilter storeLicenseFilter,
    XboxOAuthService oauthService,
    IXboxEntitlementsClient entitlementsClient,
    IXboxLibraryClient libraryClient) : IMicrosoftStoreLibraryReader
{
    public async Task<MicrosoftStoreLibraryReadResult> ReadOwnedGamesAsync(CancellationToken ct = default)
    {
        await oauthService.RefreshLoginAsync(ct);

        try
        {
            var licensingAuth = await oauthService.GetLicensingAuthorizationAsync(ct);
            var owned = await entitlementsClient.GetOwnedGamesAsync(licensingAuth, ct);
            if (owned.Count > 0)
            {
                var enriched = await EnrichWithPlaytimeAsync(owned, ct);
                return new MicrosoftStoreLibraryReadResult(enriched);
            }
        }
        catch (HttpRequestException)
        {
            // Collections API is unavailable without Microsoft partner registration.
        }

        var candidates = await ReadFilteredTitleHistoryAsync(ct);
        if (candidates.Count == 0)
            return new MicrosoftStoreLibraryReadResult([], XboxReader.TitleHistoryLimitedMessage);

        try
        {
            var verified = await storeLicenseFilter.FilterToCurrentlyOwnedAsync(candidates, ct);
            if (verified.Count > 0)
            {
                var enriched = await EnrichWithPlaytimeAsync(verified, ct);
                return new MicrosoftStoreLibraryReadResult(enriched);
            }
        }
        catch (InvalidOperationException)
        {
            // Microsoft Store is unavailable on this PC.
        }

        return new MicrosoftStoreLibraryReadResult(candidates, XboxReader.TitleHistoryLimitedMessage);
    }

    private async Task<IReadOnlyList<StoreGame>> ReadFilteredTitleHistoryAsync(CancellationToken ct)
    {
        var auth = await oauthService.GetAuthorizationAsync(ct);
        var titleHistory = await libraryClient.GetTitleHistoryAsync(auth, ct);
        var filtered = titleHistory
            .Where(XboxTitleHistoryFilter.IsEligibleOwnedCandidate)
            .ToList();

        var titleIds = filtered
            .Where(item => !string.IsNullOrWhiteSpace(item.TitleId))
            .Select(item => item.TitleId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var playtimeByTitleId = await libraryClient.GetMinutesPlayedAsync(auth, titleIds, ct);

        return filtered
            .Select(item =>
            {
                int? playtimeMinutes = null;
                if (!string.IsNullOrWhiteSpace(item.TitleId)
                    && playtimeByTitleId.TryGetValue(item.TitleId, out var minutes))
                {
                    playtimeMinutes = minutes;
                }

                return XboxTitleMapper.ToStoreGame(item, playtimeMinutes);
            })
            .Where(game => game is not null)
            .Cast<StoreGame>()
            .ToList();
    }

    private async Task<IReadOnlyList<StoreGame>> EnrichWithPlaytimeAsync(
        IReadOnlyList<StoreGame> games,
        CancellationToken ct)
    {
        if (games.Count == 0)
            return games;

        try
        {
            var auth = await oauthService.GetAuthorizationAsync(ct);
            var titleHistory = await libraryClient.GetTitleHistoryAsync(auth, ct);
            var titleIds = titleHistory
                .Where(item => !string.IsNullOrWhiteSpace(item.TitleId))
                .Select(item => item.TitleId!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (titleIds.Count == 0)
                return games;

            var playtimeByTitleId = await libraryClient.GetMinutesPlayedAsync(auth, titleIds, ct);
            var playtimeByTitle = titleHistory
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.TitleId))
                .GroupBy(item => NormalizeTitle(item.Name!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => playtimeByTitleId.TryGetValue(group.First().TitleId!, out var minutes)
                        ? minutes
                        : (int?)null,
                    StringComparer.OrdinalIgnoreCase);

            return games
                .Select(game =>
                {
                    if (game.PlaytimeMinutes is not null)
                        return game;

                    return playtimeByTitle.TryGetValue(NormalizeTitle(game.Title), out var minutes) && minutes is not null
                        ? game with { PlaytimeMinutes = minutes }
                        : game;
                })
                .ToList();
        }
        catch (XboxAuthRequiredException)
        {
            return games;
        }
    }

    private static string NormalizeTitle(string title) =>
        title
            .Replace("(PC)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(Windows)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("for Windows 10", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("- Windows 10", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
}
