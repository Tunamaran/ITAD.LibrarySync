namespace ITAD.LibrarySync.Core.Auth.Xbox;

public sealed class XboxAuthRequiredException : Exception
{
    public XboxAuthRequiredException()
        : base("Xbox authentication is required.")
    {
    }

    public XboxAuthRequiredException(string message)
        : base(message)
    {
    }
}
