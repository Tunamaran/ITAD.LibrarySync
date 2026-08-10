namespace ITAD.LibrarySync.Core.Models;

/// <summary>A game physically installed via Steam (found in an appmanifest).</summary>
public sealed record SteamInstalledGame(string AppId, string Title);
