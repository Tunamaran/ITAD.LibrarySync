using System.Text.RegularExpressions;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Logging;

public sealed partial class FileLogger
{
    private static readonly object WriteLock = new();

    private readonly string _logsDirectory;

    public static string LogsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ITADLibrarySync",
        "logs");

    public FileLogger()
        : this(LogsDirectory)
    {
    }

    public FileLogger(string logsDirectory)
    {
        _logsDirectory = logsDirectory;
        Directory.CreateDirectory(_logsDirectory);
    }

    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message) => Write("ERROR", message);

    public void LogSyncResults(IReadOnlyList<SyncResult> results)
    {
        if (results.Count == 0)
        {
            LogInfo("Sync completed — no launchers synced.");
            return;
        }

        var successes = results.Count(r => r.Success);
        LogInfo($"Sync completed — {successes}/{results.Count} launcher(s) succeeded.");

        foreach (var result in results)
        {
            if (result.Success)
            {
                LogInfo(
                    $"{FormatLauncher(result.Launcher)}: success — " +
                    $"collection +{result.CollectionAdded}/-{result.CollectionRemoved} (total {result.CollectionTotal}), " +
                    $"waitlist +{result.WaitlistAdded}/-{result.WaitlistRemoved} (total {result.WaitlistTotal}), " +
                    $"global waitlist removed {result.GlobalWaitlistRemoved}");
            }
            else
            {
                LogError($"{FormatLauncher(result.Launcher)}: failed — {result.Error ?? "Unknown error"}");
            }
        }
    }

    public static string? GetLatestLogPath(string? logsDirectory = null)
    {
        var directory = logsDirectory ?? LogsDirectory;
        if (!Directory.Exists(directory))
            return null;

        return Directory
            .EnumerateFiles(directory, "sync-*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private void Write(string level, string message)
    {
        var sanitized = SanitizeMessage(message);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {sanitized}{Environment.NewLine}";
        var path = GetLogPathForToday();

        lock (WriteLock)
        {
            File.AppendAllText(path, line);
        }
    }

    private string GetLogPathForToday() =>
        Path.Combine(_logsDirectory, $"sync-{DateTime.Now:yyyy-MM-dd}.log");

    internal static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var sanitized = message;

        sanitized = BearerTokenPattern().Replace(sanitized, "Bearer [REDACTED]");
        sanitized = AccessTokenJsonPattern().Replace(sanitized, "\"access_token\":\"[REDACTED]\"");
        sanitized = RefreshTokenJsonPattern().Replace(sanitized, "\"refresh_token\":\"[REDACTED]\"");
        sanitized = AccessTokenQueryPattern().Replace(sanitized, "access_token=[REDACTED]");
        sanitized = RefreshTokenQueryPattern().Replace(sanitized, "refresh_token=[REDACTED]");

        return sanitized;
    }

    private static string FormatLauncher(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => "Epic",
        LauncherId.Ubisoft => "Ubisoft",
        LauncherId.BattleNet => "Battle.net",
        LauncherId.Xbox => "Microsoft",
        _ => launcher.ToString()
    };

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"""access_token""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase)]
    private static partial Regex AccessTokenJsonPattern();

    [GeneratedRegex(@"""refresh_token""\s*:\s*""[^""]+""", RegexOptions.IgnoreCase)]
    private static partial Regex RefreshTokenJsonPattern();

    [GeneratedRegex(@"access_token=[^&\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex AccessTokenQueryPattern();

    [GeneratedRegex(@"refresh_token=[^&\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex RefreshTokenQueryPattern();
}
