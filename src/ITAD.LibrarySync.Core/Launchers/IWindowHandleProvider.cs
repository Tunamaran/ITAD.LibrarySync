namespace ITAD.LibrarySync.Core.Launchers;

public interface IWindowHandleProvider
{
    IntPtr Handle { get; }

    void EnsureInitialized();
}
