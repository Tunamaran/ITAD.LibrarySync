using GameCollector.StoreHandlers.EADesktop;
using GameFinder.Common;
using OneOf;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaDecryptFailureDetector
{
    internal static bool IsDecryptFailure(IEnumerable<OneOf<EADesktopGame, ErrorMessage>> results)
    {
        var (games, errors) = results.SplitResults();
        if (games.Length > 0)
            return false;

        return errors.Any(error => EaReadErrorFormatter.IsDecryptOrHardwareFailureMessage(error.Message));
    }
}
