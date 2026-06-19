using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public interface ILauncherReader
{
    LauncherId Launcher { get; }
    Task<LauncherReadResult> ReadAsync(CancellationToken ct = default);
}
