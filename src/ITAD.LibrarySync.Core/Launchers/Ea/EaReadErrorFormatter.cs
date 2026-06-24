using GameCollector.StoreHandlers.EADesktop.Crypto;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaReadErrorFormatter
{
    internal static string Format(Exception exception)
    {
        if (IsDecryptOrHardwareFailure(exception))
        {
            return EaOnlineLibraryReader.ConnectEaMessage;
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
            ? EaOnlineLibraryReader.ConnectEaMessage
            : LauncherMessageSanitizer.SanitizeLine(error);

    internal static bool IsDecryptOrHardwareFailureMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("hardware", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("HardwareInfo", StringComparison.OrdinalIgnoreCase) ||
               message.Contains(EaOnlineLibraryReader.ConnectEaMessage, StringComparison.OrdinalIgnoreCase);
    }
}
