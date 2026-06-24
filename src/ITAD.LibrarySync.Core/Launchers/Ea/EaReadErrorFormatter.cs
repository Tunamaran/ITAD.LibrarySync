using GameCollector.StoreHandlers.EADesktop.Crypto;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaReadErrorFormatter
{
    internal static string Format(Exception exception)
    {
        if (IsDecryptOrHardwareFailure(exception))
        {
            return "EA App library sync is unavailable on this PC. " +
                   "The EA App window shows your online library, but the encrypted local cache could not be read " +
                   "and no installed EA games were found on this system. " +
                   "Full EA library sync requires EA account sign-in (planned for a future release).";
        }

        return LauncherMessageSanitizer.SanitizeLine(exception.Message);
    }

    internal static bool IsDecryptOrHardwareFailure(Exception exception)
    {
        if (exception is HardwareInfoProviderException)
            return true;

        if (IsDecryptOrHardwareFailureMessage(exception.Message))
            return true;

        return exception.InnerException is not null &&
               IsDecryptOrHardwareFailure(exception.InnerException);
    }

    internal static string FormatFromReadError(string error) =>
        IsDecryptOrHardwareFailureMessage(error)
            ? "EA App library sync is unavailable on this PC. " +
              "The EA App window shows your online library, but the encrypted local cache could not be read " +
              "and no installed EA games were found on this system. " +
              "Full EA library sync requires EA account sign-in (planned for a future release)."
            : LauncherMessageSanitizer.SanitizeLine(error);

    internal static bool IsDecryptOrHardwareFailureMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("hardware", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("HardwareInfo", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("EA App library sync is unavailable on this PC", StringComparison.OrdinalIgnoreCase);
    }
}
