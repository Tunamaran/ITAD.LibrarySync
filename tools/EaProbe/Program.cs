using ITAD.LibrarySync.Core.Launchers;

var reader = new EaReader();
var result = await reader.ReadAsync();
Console.WriteLine($"Detected={result.IsDetected} LoggedIn={result.IsLoggedIn} Owned={result.Owned.Count}");
Console.WriteLine($"Error={result.Error ?? "<none>"}");
if (result.WarningMessages.Count > 0)
    Console.WriteLine($"Warning={result.WarningMessages[0]}");
foreach (var game in result.Owned.Take(20))
    Console.WriteLine($"  {game.Title} ({game.StoreId})");
