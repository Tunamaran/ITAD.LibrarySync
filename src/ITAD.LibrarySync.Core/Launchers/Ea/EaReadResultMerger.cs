using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaReadResultMerger
{
    internal const string RegistryFallbackWarning =
        "EA App local library cache could not be decrypted; syncing installed EA games detected from Windows only. " +
        "Connect your EA account in Settings for your full online library.";

    internal static LauncherReadResult MergeRegistryFallback(LauncherReadResult result)
    {
        if (result.Owned.Count > 0 ||
            !EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(result.Error))
        {
            return result;
        }

        var registryOwned = EaRegistryLibraryReader.ReadInstalledGames();
        if (registryOwned.Count == 0)
            return result;

        return result with
        {
            Owned = registryOwned,
            IsLoggedIn = true,
            Error = null,
            Warnings = [RegistryFallbackWarning]
        };
    }
}
