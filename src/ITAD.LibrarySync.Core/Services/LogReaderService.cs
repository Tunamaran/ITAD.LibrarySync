using System.Globalization;
using ITAD.LibrarySync.Core.Logging;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public sealed class LogReaderService : ILogReaderService
{
    public async Task<IReadOnlyList<LogEntry>> GetRecentLogsAsync(int maxLines = 500, CancellationToken ct = default)
    {
        var path = FileLogger.GetLatestLogPath();
        if (path == null || !File.Exists(path))
            return [];

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var lines = new List<string>();
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                lines.Add(line);
            }

            var entries = new List<LogEntry>();
            foreach (var line in lines.TakeLast(maxLines))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                entries.Add(ParseLine(line));
            }

            entries.Reverse();
            return entries;
        }
        catch
        {
            return [];
        }
    }

    private static LogEntry ParseLine(string line)
    {
        // Example format: 2026-07-25 01:27:30 [INFO] Message text
        if (line.Length > 20 && line[4] == '-' && line[7] == '-')
        {
            var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var timeStr = $"{parts[0]} {parts[1]}";
                var rest = parts[2];
                var level = "INFO";
                var msg = rest;

                if (rest.StartsWith("[") && rest.Contains("]"))
                {
                    var bracketIndex = rest.IndexOf(']');
                    level = rest[1..bracketIndex].Trim();
                    msg = rest[(bracketIndex + 1)..].Trim();
                }

                if (DateTime.TryParseExact(timeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return new LogEntry(dt, level, msg);
                }
            }
        }

        var isError = line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase);
        return new LogEntry(DateTime.Now, isError ? "ERROR" : "INFO", line);
    }
}
