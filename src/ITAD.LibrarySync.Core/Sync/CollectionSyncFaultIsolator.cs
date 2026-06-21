using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class CollectionSyncFaultIsolator(IItadApiClient api, FileLogger logger)
{
    public async Task<ItadSyncResponse> SyncCollectionAsync(
        string accessToken,
        string profileToken,
        IReadOnlyList<SyncGamePayload> payloads,
        string launcherName,
        CancellationToken ct = default)
    {
        if (payloads.Count == 0)
            throw new InvalidOperationException("Cannot sync an empty collection payload.");

        if (payloads.Count == 1)
            return await api.SyncCollectionAsync(accessToken, profileToken, payloads, ct);

        var accepted = new List<SyncGamePayload>();
        ItadSyncResponse? lastResponse = null;

        foreach (var payload in payloads)
        {
            var attempt = accepted.Concat([payload]).ToList();
            try
            {
                lastResponse = await api.SyncCollectionAsync(accessToken, profileToken, attempt, ct);
                accepted.Add(payload);
            }
            catch (HttpRequestException ex) when (IsServerError(ex))
            {
                logger.LogInfo(
                    $"{launcherName}: skipping '{payload.Title}' ({payload.Id}) — ITAD rejected this entry.");
            }
        }

        if (accepted.Count == 0)
            throw new HttpRequestException(
                $"{launcherName}: ITAD rejected every collection entry during fault isolation.");

        if (accepted.Count < payloads.Count)
        {
            logger.LogInfo(
                $"{launcherName}: synced {accepted.Count}/{payloads.Count} collection entries after skipping rejected games.");
        }

        return lastResponse!;
    }

    private static bool IsServerError(HttpRequestException exception) =>
        exception.Message.Contains("500", StringComparison.Ordinal);
}
