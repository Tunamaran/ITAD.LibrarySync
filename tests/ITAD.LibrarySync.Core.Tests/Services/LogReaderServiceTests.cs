using ITAD.LibrarySync.Core.Services;
using Xunit;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class LogReaderServiceTests
{
    [Fact]
    public async Task GetRecentLogsAsync_ReturnsEmptyList_WhenNoLogFilesExist()
    {
        var service = new LogReaderService();
        var logs = await service.GetRecentLogsAsync();

        Assert.NotNull(logs);
    }
}
