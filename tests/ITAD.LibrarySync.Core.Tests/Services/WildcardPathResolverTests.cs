using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class WildcardPathResolverTests
{
    [Fact]
    public void Resolve_NoWildcard_ExpandsEnvVars()
    {
        var raw = @"%USERPROFILE%\Documents\My Games\Test";

        var (resolved, _) = WildcardPathResolver.Resolve(
            raw,
            directoryExists: _ => false);

        var expectedUserprofile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(expectedUserprofile, @"Documents\My Games\Test"), resolved);
    }

    [Fact]
    public void Resolve_WithWildcardMatchingDirectory_ReturnsExistingPath()
    {
        var raw = @"%LOCALAPPDATA%\Saber\RoadCraftGame\storage\steam\user\*\Main\save";

        var expectedUserFolder = @"C:\Users\TestUser\AppData\Local\Saber\RoadCraftGame\storage\steam\user\12345678";
        var expectedSaveFolder = @"C:\Users\TestUser\AppData\Local\Saber\RoadCraftGame\storage\steam\user\12345678\Main\save";

        var (resolved, exists) = WildcardPathResolver.Resolve(
            raw,
            getDirectories: (dir, pattern) =>
            {
                if (dir.EndsWith(@"storage\steam\user", StringComparison.OrdinalIgnoreCase) && pattern == "*")
                {
                    return [expectedUserFolder];
                }
                return [];
            },
            directoryExists: dir => string.Equals(dir, expectedSaveFolder, StringComparison.OrdinalIgnoreCase));

        Assert.True(exists);
        Assert.Equal(expectedSaveFolder, resolved);
    }
}
