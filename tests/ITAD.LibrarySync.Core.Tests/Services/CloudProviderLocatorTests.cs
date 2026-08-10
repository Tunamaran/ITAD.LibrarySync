using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.Core.Tests.Services;

public sealed class CloudProviderLocatorTests
{
    [Fact]
    public void GetCloudRoot_OneDrive_UsesEnvironmentVariable()
    {
        var locator = new CloudProviderLocator(
            pathExists: path => path == @"C:\Users\Test\OneDrive",
            getEnv: key => key == "OneDrive" ? @"C:\Users\Test\OneDrive" : null);

        Assert.Equal(@"C:\Users\Test\OneDrive", locator.GetCloudRoot(CloudProvider.OneDrive));
    }

    [Fact]
    public void GetCloudRoot_OneDrive_FallsBackToUserProfile()
    {
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "OneDrive");

        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, fallback, StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null);

        Assert.Equal(fallback, locator.GetCloudRoot(CloudProvider.OneDrive));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_PrefersMyDrive()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var myDrive = Path.Combine(userProfile, "My Drive");

        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, myDrive, StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null);

        Assert.Equal(myDrive, locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetCloudRoot_Dropbox_FallsBackToUserProfile()
    {
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Dropbox");

        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, fallback, StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null);

        Assert.Equal(fallback, locator.GetCloudRoot(CloudProvider.Dropbox));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_DetectsDriveLetterMount()
    {
        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, @"G:\My Drive", StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null,
            driveRoots: () => [@"C:\", @"G:\"]);

        Assert.Equal(@"G:\My Drive", locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_DriveLetterMountWithGoogleDriveFolder()
    {
        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, @"G:\Google Drive", StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null,
            driveRoots: () => [@"C:\", @"G:\"]);

        Assert.Equal(@"G:\Google Drive", locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_DetectsLocalizedDriveLetterMount()
    {
        // Turkish Google Drive for desktop names the mount folder "Drive'ım".
        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, @"G:\Drive'ım", StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null,
            driveRoots: () => [@"C:\", @"G:\"]);

        Assert.Equal(@"G:\Drive'ım", locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_DetectsLocalizedProfileFolder()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localized = Path.Combine(userProfile, "Drive'ım");

        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(path, localized, StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null);

        Assert.Equal(localized, locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_MarkerFallback_DetectsAnyLanguage()
    {
        // Language-agnostic fallback: the My Drive folder contains a hidden
        // ".shortcut-targets-by-id" marker regardless of the localized name.
        var locator = new CloudProviderLocator(
            pathExists: path => string.Equals(
                path,
                @"G:\HerhangiBirIsim\.shortcut-targets-by-id",
                StringComparison.OrdinalIgnoreCase),
            getEnv: _ => null,
            driveRoots: () => [@"G:\"],
            enumerateDirectories: dir => string.Equals(dir, @"G:\", StringComparison.OrdinalIgnoreCase)
                ? [@"G:\HerhangiBirIsim"]
                : []);

        Assert.Equal(@"G:\HerhangiBirIsim", locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetCloudRoot_GoogleDrive_PlainDrivesWithoutMount_ReturnsNull()
    {
        var locator = new CloudProviderLocator(
            pathExists: _ => false,
            getEnv: _ => null,
            driveRoots: () => [@"C:\", @"D:\"]);

        Assert.Null(locator.GetCloudRoot(CloudProvider.GoogleDrive));
    }

    [Fact]
    public void GetAvailableProviders_OnlyReturnsResolvedRoots()
    {
        var locator = new CloudProviderLocator(
            pathExists: _ => false,
            getEnv: _ => null);

        Assert.Empty(locator.GetAvailableProviders());
    }
}
