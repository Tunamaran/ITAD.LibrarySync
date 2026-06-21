using GameCollector.StoreHandlers.Xbox;
using ITAD.LibrarySync.Core.Launchers;
using NexusMods.Paths;
var handler = new XboxHandler(FileSystem.Shared);
Console.WriteLine("IsXboxInstalled: " + LauncherClientDetection.IsXboxInstalled(FileSystem.Shared));
