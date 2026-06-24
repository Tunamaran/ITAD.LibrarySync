namespace ITAD.LibrarySync.Core.Auth.Ea;

public sealed record EaSessionInfo(
    string UserId,
    string PersonaId,
    string DisplayName);
