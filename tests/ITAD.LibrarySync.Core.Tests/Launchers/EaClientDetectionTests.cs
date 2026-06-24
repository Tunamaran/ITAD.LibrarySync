using FluentAssertions;
using GameFinder.RegistryUtils;
using ITAD.LibrarySync.Core.Launchers.Ea;
using NexusMods.Paths;

namespace ITAD.LibrarySync.Core.Tests.Launchers;

public class EaClientDetectionTests
{
    [Fact]
    public void IsEaInstalled_ReturnsTrueWhenInstallInfoFileExists()
    {
        var fileSystem = FileSystem.Shared;
        var installInfoFile = EaClientDetection.GetInstallInfoFile(fileSystem);

        if (!fileSystem.FileExists(installInfoFile))
            return;

        EaClientDetection.IsEaInstalled(fileSystem, default)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ResolveClientPath_UsesRegistryWhenHandlerReturnsDefault()
    {
        var fileSystem = FileSystem.Shared;
        var registry = WindowsRegistry.Shared;
        var resolved = EaClientDetection.ResolveClientPath(default, fileSystem, registry);

        if (resolved == default)
            return;

        fileSystem.FileExists(resolved).Should().BeTrue();
    }
}
