using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Services;

public interface ILogReaderService
{
    Task<IReadOnlyList<LogEntry>> GetRecentLogsAsync(int maxLines = 500, CancellationToken ct = default);
}
