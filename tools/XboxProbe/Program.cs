using System.Text.Json;
using ITAD.LibrarySync.Core.Auth.Xbox;
using ITAD.LibrarySync.Core.Launchers.Xbox;

var httpClient = new HttpClient();
var options = XboxOAuthOptions.CreateDefault();
var storage = new XboxTokenStorage();
var oauth = new XboxOAuthService(httpClient, options, storage);
var titleHub = new TitleHubClient(httpClient);
var collections = new XboxCollectionsClient(httpClient);

try
{
    await oauth.RefreshLoginAsync(CancellationToken.None);

    Console.WriteLine("=== Microsoft Store Collections (owned entitlements) ===");
    try
    {
        var licensingAuth = await oauth.GetLicensingAuthorizationAsync(CancellationToken.None);
        var owned = await collections.GetOwnedGamesAsync(licensingAuth, CancellationToken.None);
        Console.WriteLine($"Owned game count: {owned.Count}");
        foreach (var game in owned.Take(10))
            Console.WriteLine($"  - {game.Title} ({game.StoreId})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Collections failed: {ex.Message}");
    }

    Console.WriteLine();
    Console.WriteLine("=== TitleHub title history (play history, not ownership) ===");
    var auth = await oauth.GetAuthorizationAsync(CancellationToken.None);
    var titles = await titleHub.GetTitleHistoryAsync(auth, CancellationToken.None);
    var filtered = titles.Where(XboxTitleHistoryFilter.IsEligibleOwnedCandidate).ToList();
    Console.WriteLine($"Raw title history: {titles.Count}");
    Console.WriteLine($"Filtered PC candidates: {filtered.Count}");
    foreach (var title in filtered.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  - {title.Name} | pfn={title.Pfn ?? "(none)"} | devices=[{string.Join(',', title.Devices ?? [])}]");

    var sample = titles.FirstOrDefault(t => string.Equals(t.Name, "SnowRunner (Windows 10)", StringComparison.OrdinalIgnoreCase));
    if (sample is not null)
    {
        Console.WriteLine();
        Console.WriteLine("=== Sample raw JSON field names (SnowRunner) ===");
        var json = JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"XboxProbe failed: {ex.Message}");
    return 1;
}

return 0;
