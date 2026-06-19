using FluentAssertions;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Tests.Logging;

public class FileLoggerTests : IDisposable
{
    private readonly string _logsDirectory;
    private readonly FileLogger _logger;

    public FileLoggerTests()
    {
        _logsDirectory = Path.Combine(Path.GetTempPath(), "ITADLibrarySyncTests", Guid.NewGuid().ToString("N"));
        _logger = new FileLogger(_logsDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_logsDirectory))
            Directory.Delete(_logsDirectory, recursive: true);
    }

    [Fact]
    public void LogInfo_WritesToDailyLogFile()
    {
        _logger.LogInfo("Test message");

        var logFile = Directory.GetFiles(_logsDirectory, "sync-*.log").Single();
        var content = File.ReadAllText(logFile);

        content.Should().Contain("[INFO] Test message");
        logFile.Should().EndWith($"sync-{DateTime.Now:yyyy-MM-dd}.log");
    }

    [Fact]
    public void LogWarning_And_LogError_WriteCorrectLevels()
    {
        _logger.LogWarning("Warn message");
        _logger.LogError("Error message");

        var content = File.ReadAllText(Directory.GetFiles(_logsDirectory, "sync-*.log").Single());

        content.Should().Contain("[WARN] Warn message");
        content.Should().Contain("[ERROR] Error message");
    }

    [Fact]
    public void LogSyncResults_WritesPerLauncherOutcomes()
    {
        var results = new[]
        {
            new SyncResult(LauncherId.Epic, true, 10, 2, 1, 5, 1, 0, 0),
            new SyncResult(LauncherId.Ubisoft, false, 0, 0, 0, 0, 0, 0, 0, "API unavailable")
        };

        _logger.LogSyncResults(results);

        var content = File.ReadAllText(Directory.GetFiles(_logsDirectory, "sync-*.log").Single());

        content.Should().Contain("1/2 launcher(s) succeeded");
        content.Should().Contain("Epic: success");
        content.Should().Contain("collection +2/-1 (total 10)");
        content.Should().Contain("Ubisoft: failed — API unavailable");
    }

    [Fact]
    public void LogError_RedactsTokensInMessage()
    {
        var message =
            "Bearer abc123token access_token=secret refresh_token=also-secret " +
            "\"access_token\":\"json-secret\" \"refresh_token\":\"json-refresh\"";

        _logger.LogError(message);

        var content = File.ReadAllText(Directory.GetFiles(_logsDirectory, "sync-*.log").Single());

        content.Should().NotContain("abc123token");
        content.Should().NotContain("secret");
        content.Should().NotContain("json-secret");
        content.Should().NotContain("json-refresh");
        content.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void GetLatestLogPath_ReturnsMostRecentlyWrittenFile()
    {
        var older = Path.Combine(_logsDirectory, "sync-2020-01-01.log");
        var newer = Path.Combine(_logsDirectory, "sync-2020-01-02.log");
        File.WriteAllText(older, "old");
        File.WriteAllText(newer, "new");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        FileLogger.GetLatestLogPath(_logsDirectory).Should().Be(newer);
    }
}
