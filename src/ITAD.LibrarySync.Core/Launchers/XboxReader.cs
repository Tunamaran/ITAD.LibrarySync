using System.Runtime.Versioning;

using GameCollector.StoreHandlers.Xbox;

using GameFinder.Common;

using ITAD.LibrarySync.Core.Auth.Xbox;

using ITAD.LibrarySync.Core.Models;

using ITAD.LibrarySync.Core.Sync;

using NexusMods.Paths;



namespace ITAD.LibrarySync.Core.Launchers;



[SupportedOSPlatform("windows")]

public sealed class XboxReader(IMicrosoftStoreLibraryReader? storeLibraryReader = null) : ILauncherReader

{

    public const string XboxConnectMessage = "Connect your Xbox account in Settings";



    public const string TitleHistoryLimitedMessage =

        "Microsoft Store license check unavailable; fell back to Xbox play history, which may over-count.";



    private static readonly Settings OwnedGamesSettings = new() { OwnedOnly = true };



    public LauncherId Launcher => LauncherId.Xbox;



    public async Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)

    {

        ct.ThrowIfCancellationRequested();



        try

        {

            var handler = new XboxHandler(FileSystem.Shared);

            var clientPath = LauncherClientDetection.NormalizeClientPath(

                handler.FindClient(),

                FileSystem.Shared);

            var isInstalled = LauncherClientDetection.IsXboxInstalled(FileSystem.Shared);

            var results = handler.FindAllGames(OwnedGamesSettings);



            var installedResult = LauncherReadHelper.ReadOwnedGames(

                LauncherId.Xbox,

                clientPath,

                FileSystem.Shared,

                results,

                game => LauncherReadHelper.MapGame(

                    LauncherId.Xbox,

                    game.Id.Value,

                    game.DisplayName),

                treatAsInstalled: isInstalled);



            if (storeLibraryReader is null)

                return FilterXboxLocalScanNoise(installedResult) with { Installed = installedResult.Owned };



            try

            {

                var apiRead = await storeLibraryReader.ReadOwnedGamesAsync(ct);

                var merged = MergeLibraries(apiRead.Games, installedResult.Owned);



                if (merged.Count == 0)

                {

                    return FilterXboxLocalScanNoise(installedResult) with

                    {

                        IsDetected = installedResult.IsDetected || isInstalled,

                        IsLoggedIn = true,

                        Owned = merged,

                        Installed = installedResult.Owned,

                        Error = apiRead.Warning ?? TitleHistoryLimitedMessage

                    };

                }



                var result = FilterXboxLocalScanNoise(installedResult) with

                {

                    IsDetected = true,

                    IsLoggedIn = true,

                    Owned = merged,

                    Installed = installedResult.Owned,

                    Error = null

                };



                if (apiRead.Warning is not null)

                    result = AppendWarnings(result, apiRead.Warning);



                return result;

            }

            catch (XboxAuthRequiredException)

            {

                return FilterXboxLocalScanNoise(installedResult) with

                {

                    IsDetected = installedResult.IsDetected || isInstalled,

                    IsLoggedIn = false,

                    Installed = installedResult.Owned,

                    Error = XboxConnectMessage

                };

            }

            catch (Exception ex)
            {
                if (installedResult.Owned.Count > 0)
                {
                    return AppendWarnings(
                        FilterXboxLocalScanNoise(installedResult) with { IsLoggedIn = true, Installed = installedResult.Owned },
                        ex.Message);
                }

                return FilterXboxLocalScanNoise(installedResult) with
                {
                    IsDetected = isInstalled,
                    IsLoggedIn = false,
                    Installed = installedResult.Owned,
                    Error = null,
                    Warnings = [ex.Message]
                };
            }

        }

        catch (Exception ex)

        {

            return LauncherReadHelper.FromException(LauncherId.Xbox, ex);

        }

    }



    private static IReadOnlyList<StoreGame> MergeLibraries(

        IReadOnlyList<StoreGame> apiOwned,

        IReadOnlyList<StoreGame> localOwned)

    {

        var merged = new List<StoreGame>(apiOwned.Count + localOwned.Count);

        var unmatchedLocal = localOwned.ToList();



        foreach (var apiGame in apiOwned)

        {

            var localMatch = unmatchedLocal.FirstOrDefault(local => GameMatcher.IsSameGame(apiGame, local));

            if (localMatch is not null)

            {

                unmatchedLocal.Remove(localMatch);

                merged.Add(EnrichPlaytime(apiGame, localMatch));

            }

            else

            {

                merged.Add(apiGame);

            }

        }



        merged.AddRange(unmatchedLocal);



        return merged

            .GroupBy(game => game.StoreId, StringComparer.OrdinalIgnoreCase)

            .Select(group => group.First())

            .ToList();

    }



    private static StoreGame EnrichPlaytime(StoreGame primary, StoreGame local) =>

        primary with

        {

            PlaytimeMinutes = PreferHigherPlaytime(primary.PlaytimeMinutes, local.PlaytimeMinutes),

            LastPlayed = local.LastPlayed ?? primary.LastPlayed

        };



    private static int? PreferHigherPlaytime(int? primary, int? local)

    {

        if (primary is null)

            return local;



        if (local is null)

            return primary;



        return Math.Max(primary.Value, local.Value);

    }



    private static LauncherReadResult FilterXboxLocalScanNoise(LauncherReadResult result)

    {

        var filteredWarnings = result.WarningMessages

            .Where(message =>

                !message.Contains("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase) &&

                !message.Contains("GamingRoot", StringComparison.OrdinalIgnoreCase) &&

                !message.Contains("appxmanifest.xml", StringComparison.OrdinalIgnoreCase) &&

                !message.Contains("does not contain any sub directories", StringComparison.OrdinalIgnoreCase))

            .ToList();



        var filteredError = result.Error;

        if (!string.IsNullOrWhiteSpace(filteredError) &&

            (filteredError.Contains("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase) ||

             filteredError.Contains("GamingRoot", StringComparison.OrdinalIgnoreCase) ||

             filteredError.Contains("appxmanifest.xml", StringComparison.OrdinalIgnoreCase) ||

             filteredError.Contains("does not contain any sub directories", StringComparison.OrdinalIgnoreCase)))

        {

            filteredError = null;

        }



        return result with

        {

            Error = filteredError,

            Warnings = filteredWarnings.Count > 0 ? filteredWarnings : null

        };

    }



    private static LauncherReadResult AppendWarnings(LauncherReadResult result, params string?[] messages)

    {

        var warnings = result.WarningMessages.ToList();



        foreach (var message in messages)

        {

            if (string.IsNullOrWhiteSpace(message))

                continue;



            warnings.AddRange(LauncherMessageSanitizer.SplitCombined(message));

        }



        return result with

        {

            Warnings = warnings.Count > 0 ? warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList() : null

        };

    }

}


