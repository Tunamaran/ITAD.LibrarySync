# ITAD Library Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows WPF tray app that reads Epic, Ubisoft Connect, Battle.net, and Microsoft/Xbox libraries from local launcher cache and syncs Collection + Waitlist to IsThereAnyDeal via OAuth Custom Profiles API.

**Architecture:** Layered .NET 8 solution — `ITAD.LibrarySync.Core` handles launcher reading (GameCollector), ITAD API, sync orchestration, and scheduling; `ITAD.LibrarySync.App` is a WPF tray shell with WebView2 OAuth. Sync pipeline: read owned/wishlist → filter owned from waitlist → per-profile collection/waitlist sync → global waitlist cleanup.

**Tech Stack:** .NET 8, WPF, GameCollector NuGet handlers, WebView2, CommunityToolkit.Mvvm, Hardcodet.NotifyIcon.Wpf, xUnit, Moq, FluentAssertions

**Spec reference:** `docs/superpowers/specs/2026-06-20-itad-library-sync-design.md`

---

## File Map

| Path | Responsibility |
|------|----------------|
| `src/ITAD.LibrarySync.Core/Models/StoreGame.cs` | Normalized game DTO (store, id, title, playtime) |
| `src/ITAD.LibrarySync.Core/Models/SyncResult.cs` | Per-launcher sync outcome |
| `src/ITAD.LibrarySync.Core/Models/LauncherId.cs` | Enum: Epic, Ubisoft, BattleNet, Xbox |
| `src/ITAD.LibrarySync.Core/Launchers/ILauncherReader.cs` | Read owned + wishlist from local cache |
| `src/ITAD.LibrarySync.Core/Launchers/EpicReader.cs` | GameCollector EGSHandler wrapper |
| `src/ITAD.LibrarySync.Core/Launchers/UbisoftReader.cs` | GameCollector UbisoftHandler wrapper |
| `src/ITAD.LibrarySync.Core/Launchers/BattleNetReader.cs` | GameCollector BattleNetHandler wrapper |
| `src/ITAD.LibrarySync.Core/Launchers/XboxReader.cs` | GameCollector XboxHandler wrapper |
| `src/ITAD.LibrarySync.Core/Sync/GameMatcher.cs` | ID + title normalization/matching |
| `src/ITAD.LibrarySync.Core/Sync/WaitlistFilter.cs` | Remove owned from wishlist |
| `src/ITAD.LibrarySync.Core/Sync/CollectionSyncService.cs` | Push owned to ITAD collection profile |
| `src/ITAD.LibrarySync.Core/Sync/WaitlistSyncService.cs` | Push filtered wishlist to ITAD profile |
| `src/ITAD.LibrarySync.Core/Sync/WaitlistCleanupService.cs` | Global owned removal from ITAD waitlist |
| `src/ITAD.LibrarySync.Core/Sync/SyncOrchestrator.cs` | Full sync pipeline coordinator |
| `src/ITAD.LibrarySync.Core/Api/IItadApiClient.cs` | ITAD HTTP abstraction |
| `src/ITAD.LibrarySync.Core/Api/ItadApiClient.cs` | OAuth-authenticated ITAD calls |
| `src/ITAD.LibrarySync.Core/Api/ShopIdResolver.cs` | Map LauncherId → ITAD shop ID |
| `src/ITAD.LibrarySync.Core/Auth/ItadOAuthService.cs` | OAuth code flow + refresh |
| `src/ITAD.LibrarySync.Core/Auth/TokenStorage.cs` | DPAPI-encrypted token persistence |
| `src/ITAD.LibrarySync.Core/Profiles/ProfileManager.cs` | Link/store per-launcher ITAD profile tokens |
| `src/ITAD.LibrarySync.Core/Scheduling/SyncScheduler.cs` | Optional periodic sync timer |
| `src/ITAD.LibrarySync.App/App.xaml.cs` | DI composition root |
| `src/ITAD.LibrarySync.App/Services/TrayIconService.cs` | Tray icon + context menu |
| `src/ITAD.LibrarySync.App/Services/NotificationService.cs` | Windows toast notifications |
| `src/ITAD.LibrarySync.App/Views/OAuthWebViewWindow.xaml` | WebView2 OAuth popup |
| `src/ITAD.LibrarySync.App/Views/SettingsWindow.xaml` | Settings tabs UI |
| `src/ITAD.LibrarySync.App/Views/FirstRunWizard.xaml` | First-run wizard |
| `src/ITAD.LibrarySync.App/ViewModels/SettingsViewModel.cs` | Settings + sync commands |
| `tests/ITAD.LibrarySync.Core.Tests/Sync/GameMatcherTests.cs` | GameMatcher unit tests |
| `tests/ITAD.LibrarySync.Core.Tests/Sync/WaitlistFilterTests.cs` | WaitlistFilter unit tests |
| `tests/ITAD.LibrarySync.Core.Tests/Sync/SyncOrchestratorTests.cs` | Orchestrator unit tests |

---

### Task 1: Solution Scaffold

**Files:**
- Create: `ITAD.LibrarySync.sln`
- Create: `src/ITAD.LibrarySync.Core/ITAD.LibrarySync.Core.csproj`
- Create: `src/ITAD.LibrarySync.App/ITAD.LibrarySync.App.csproj`
- Create: `tests/ITAD.LibrarySync.Core.Tests/ITAD.LibrarySync.Core.Tests.csproj`
- Create: `.gitignore`
- Create: `README.md`
- Create: `LICENSE`

- [ ] **Step 1: Create solution and projects**

Run from repo root:

```powershell
cd C:\Users\Tunahan\ITAD.LibrarySync
dotnet new sln -n ITAD.LibrarySync
dotnet new classlib -n ITAD.LibrarySync.Core -o src/ITAD.LibrarySync.Core -f net8.0
dotnet new wpf -n ITAD.LibrarySync.App -o src/ITAD.LibrarySync.App -f net8.0
dotnet new xunit -n ITAD.LibrarySync.Core.Tests -o tests/ITAD.LibrarySync.Core.Tests -f net8.0
dotnet sln add src/ITAD.LibrarySync.Core/ITAD.LibrarySync.Core.csproj
dotnet sln add src/ITAD.LibrarySync.App/ITAD.LibrarySync.App.csproj
dotnet sln add tests/ITAD.LibrarySync.Core.Tests/ITAD.LibrarySync.Core.Tests.csproj
dotnet add src/ITAD.LibrarySync.App/ reference src/ITAD.LibrarySync.Core/ITAD.LibrarySync.Core.csproj
dotnet add tests/ITAD.LibrarySync.Core.Tests/ reference src/ITAD.LibrarySync.Core/ITAD.LibrarySync.Core.csproj
```

- [ ] **Step 2: Add NuGet packages**

```powershell
dotnet add src/ITAD.LibrarySync.Core/ package GameCollector.StoreHandlers.EGS
dotnet add src/ITAD.LibrarySync.Core/ package GameCollector.StoreHandlers.Ubisoft
dotnet add src/ITAD.LibrarySync.Core/ package GameCollector.StoreHandlers.BattleNet
dotnet add src/ITAD.LibrarySync.Core/ package GameCollector.StoreHandlers.Xbox
dotnet add src/ITAD.LibrarySync.App/ package Microsoft.Web.WebView2
dotnet add src/ITAD.LibrarySync.App/ package CommunityToolkit.Mvvm
dotnet add src/ITAD.LibrarySync.App/ package Hardcodet.NotifyIcon.Wpf
dotnet add src/ITAD.LibrarySync.App/ package Microsoft.Toolkit.Uwp.Notifications
dotnet add tests/ITAD.LibrarySync.Core.Tests/ package Moq
dotnet add tests/ITAD.LibrarySync.Core.Tests/ package FluentAssertions
dotnet add tests/ITAD.LibrarySync.Core.Tests/ package Microsoft.NET.Test.Sdk
```

- [ ] **Step 3: Add `.gitignore` for .NET**

Create `.gitignore` with standard Visual Studio / .NET entries (bin/, obj/, .vs/, *.user).

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "chore: scaffold ITAD Library Sync solution"
```

---

### Task 2: Core Domain Models

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Models/LauncherId.cs`
- Create: `src/ITAD.LibrarySync.Core/Models/StoreGame.cs`
- Create: `src/ITAD.LibrarySync.Core/Models/SyncGamePayload.cs`
- Create: `src/ITAD.LibrarySync.Core/Models/SyncResult.cs`
- Create: `src/ITAD.LibrarySync.Core/Models/LauncherReadResult.cs`
- Delete: `src/ITAD.LibrarySync.Core/Class1.cs`

- [ ] **Step 1: Create LauncherId enum**

```csharp
// src/ITAD.LibrarySync.Core/Models/LauncherId.cs
namespace ITAD.LibrarySync.Core.Models;

public enum LauncherId
{
    Epic,
    Ubisoft,
    BattleNet,
    Xbox
}
```

- [ ] **Step 2: Create StoreGame and SyncGamePayload**

```csharp
// src/ITAD.LibrarySync.Core/Models/StoreGame.cs
namespace ITAD.LibrarySync.Core.Models;

public sealed record StoreGame(
    LauncherId Launcher,
    string StoreId,
    string Title,
    int? PlaytimeMinutes = null,
    DateTimeOffset? LastPlayed = null);

// src/ITAD.LibrarySync.Core/Models/SyncGamePayload.cs
namespace ITAD.LibrarySync.Core.Models;

public sealed record SyncGamePayload(
    int Shop,
    string Id,
    string Title,
    int? Playtime = null,
    DateTimeOffset? LastPlayed = null);
```

- [ ] **Step 3: Create SyncResult and LauncherReadResult**

```csharp
// src/ITAD.LibrarySync.Core/Models/SyncResult.cs
namespace ITAD.LibrarySync.Core.Models;

public sealed record SyncResult(
    LauncherId Launcher,
    bool Success,
    int CollectionTotal,
    int CollectionAdded,
    int CollectionRemoved,
    int WaitlistTotal,
    int WaitlistAdded,
    int WaitlistRemoved,
    int GlobalWaitlistRemoved,
    string? Error = null);

// src/ITAD.LibrarySync.Core/Models/LauncherReadResult.cs
namespace ITAD.LibrarySync.Core.Models;

public sealed record LauncherReadResult(
    LauncherId Launcher,
    bool IsDetected,
    bool IsLoggedIn,
    IReadOnlyList<StoreGame> Owned,
    IReadOnlyList<StoreGame> Wishlist,
    string? Error = null);
```

- [ ] **Step 4: Build and commit**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

```powershell
git add src/ITAD.LibrarySync.Core/Models/
git commit -m "feat: add core domain models"
```

---

### Task 3: GameMatcher (TDD)

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Sync/GameMatcher.cs`
- Create: `tests/ITAD.LibrarySync.Core.Tests/Sync/GameMatcherTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/ITAD.LibrarySync.Core.Tests/Sync/GameMatcherTests.cs
using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class GameMatcherTests
{
    [Fact]
    public void MatchesByStoreId_CaseInsensitive()
    {
        var owned = new StoreGame(LauncherId.Epic, "abc123", "Hades");
        var candidate = new StoreGame(LauncherId.Epic, "ABC123", "Different Title");
        GameMatcher.IsSameGame(owned, candidate).Should().BeTrue();
    }

    [Fact]
    public void MatchesByNormalizedTitle_WhenStoreIdDiffers()
    {
        var owned = new StoreGame(LauncherId.Epic, "id1", "Grand Theft Auto V");
        var candidate = new StoreGame(LauncherId.Epic, "id2", "grand theft auto  v ");
        GameMatcher.IsSameGame(owned, candidate).Should().BeTrue();
    }

    [Fact]
    public void DoesNotMatchDifferentLauncher()
    {
        var owned = new StoreGame(LauncherId.Epic, "same", "Hades");
        var candidate = new StoreGame(LauncherId.Ubisoft, "same", "Hades");
        GameMatcher.IsSameGame(owned, candidate).Should().BeFalse();
    }

    [Theory]
    [InlineData("Hades", "hades")]
    [InlineData("  Observer_ ", "observer_")]
    public void NormalizeTitle_TrimsAndLowercases(string input, string expected)
    {
        GameMatcher.NormalizeTitle(input).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ITAD.LibrarySync.Core.Tests --filter GameMatcherTests -v n`
Expected: FAIL — `GameMatcher` not found

- [ ] **Step 3: Implement GameMatcher**

```csharp
// src/ITAD.LibrarySync.Core/Sync/GameMatcher.cs
using System.Text.RegularExpressions;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static partial class GameMatcher
{
    public static bool IsSameGame(StoreGame a, StoreGame b)
    {
        if (a.Launcher != b.Launcher)
            return false;

        if (string.Equals(a.StoreId, b.StoreId, StringComparison.OrdinalIgnoreCase))
            return true;

        return NormalizeTitle(a.Title) == NormalizeTitle(b.Title);
    }

    public static string NormalizeTitle(string title)
    {
        var collapsed = WhitespaceRegex().Replace(title.Trim(), " ");
        return collapsed.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ITAD.LibrarySync.Core.Tests --filter GameMatcherTests -v n`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Sync/GameMatcher.cs tests/ITAD.LibrarySync.Core.Tests/Sync/GameMatcherTests.cs
git commit -m "feat: add GameMatcher with title and store ID matching"
```

---

### Task 4: WaitlistFilter (TDD)

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Sync/WaitlistFilter.cs`
- Create: `tests/ITAD.LibrarySync.Core.Tests/Sync/WaitlistFilterTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/ITAD.LibrarySync.Core.Tests/Sync/WaitlistFilterTests.cs
using FluentAssertions;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class WaitlistFilterTests
{
    [Fact]
    public void RemoveOwnedGames_ExcludesMatches()
    {
        var owned = new List<StoreGame>
        {
            new(LauncherId.Epic, "e1", "Hades"),
            new(LauncherId.Epic, "e2", "Celeste")
        };
        var wishlist = new List<StoreGame>
        {
            new(LauncherId.Epic, "w1", "Hades"),
            new(LauncherId.Epic, "w2", "Disco Elysium")
        };

        var result = WaitlistFilter.RemoveOwnedGames(wishlist, owned);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Disco Elysium");
    }

    [Fact]
    public void ShouldSkipCollectionSync_WhenOwnedEmpty()
    {
        WaitlistFilter.ShouldSkipCollectionSync(Array.Empty<StoreGame>()).Should().BeTrue();
        WaitlistFilter.ShouldSkipCollectionSync(new[] { new StoreGame(LauncherId.Epic, "1", "A") }).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipWaitlistSync_WhenWishlistUnreadable()
    {
        WaitlistFilter.ShouldSkipWaitlistSync(wishlistReadable: false, wishlistCount: 0).Should().BeTrue();
        WaitlistFilter.ShouldSkipWaitlistSync(wishlistReadable: true, wishlistCount: 0).Should().BeTrue();
        WaitlistFilter.ShouldSkipWaitlistSync(wishlistReadable: true, wishlistCount: 3).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/ITAD.LibrarySync.Core.Tests --filter WaitlistFilterTests -v n`

- [ ] **Step 3: Implement WaitlistFilter**

```csharp
// src/ITAD.LibrarySync.Core/Sync/WaitlistFilter.cs
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public static class WaitlistFilter
{
    public static IReadOnlyList<StoreGame> RemoveOwnedGames(
        IReadOnlyList<StoreGame> wishlist,
        IReadOnlyList<StoreGame> owned)
    {
        return wishlist
            .Where(w => !owned.Any(o => GameMatcher.IsSameGame(w, o)))
            .ToList();
    }

    public static bool ShouldSkipCollectionSync(IReadOnlyList<StoreGame> owned)
        => owned.Count == 0;

    public static bool ShouldSkipWaitlistSync(bool wishlistReadable, int wishlistCount)
        => !wishlistReadable || wishlistCount == 0;
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/ITAD.LibrarySync.Core.Tests --filter WaitlistFilterTests -v n`

- [ ] **Step 5: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Sync/WaitlistFilter.cs tests/ITAD.LibrarySync.Core.Tests/Sync/WaitlistFilterTests.cs
git commit -m "feat: add waitlist filter and empty-list guards"
```

---

### Task 5: ITAD API Client

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Api/IItadApiClient.cs`
- Create: `src/ITAD.LibrarySync.Core/Api/ItadApiClient.cs`
- Create: `src/ITAD.LibrarySync.Core/Api/ItadSyncResponse.cs`
- Create: `src/ITAD.LibrarySync.Core/Api/ShopIdResolver.cs`
- Create: `src/ITAD.LibrarySync.Core/Api/ItadOptions.cs`

- [ ] **Step 1: Define interfaces and options**

```csharp
// src/ITAD.LibrarySync.Core/Api/ItadOptions.cs
namespace ITAD.LibrarySync.Core.Api;

public sealed class ItadOptions
{
    public const string BaseUrl = "https://api.isthereanydeal.com";
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
}

// src/ITAD.LibrarySync.Core/Api/IItadApiClient.cs
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public interface IItadApiClient
{
    Task<string> LinkProfileAsync(string accessToken, string accountId, string accountName, CancellationToken ct = default);
    Task<ItadSyncResponse> SyncCollectionAsync(string accessToken, string profileToken, IReadOnlyList<SyncGamePayload> games, CancellationToken ct = default);
    Task<ItadSyncResponse> SyncWaitlistAsync(string accessToken, string profileToken, IReadOnlyList<SyncGamePayload> games, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetWaitlistGameIdsAsync(string accessToken, CancellationToken ct = default);
    Task DeleteWaitlistGamesAsync(string accessToken, IReadOnlyList<string> gameIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, int>> GetShopMapAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> LookupGameIdsByShopIdsAsync(int shopId, IReadOnlyList<string> shopGameIds, CancellationToken ct = default);
}

public sealed record ItadSyncResponse(int Total, int Added, int Removed);
```

- [ ] **Step 2: Implement ShopIdResolver**

```csharp
// src/ITAD.LibrarySync.Core/Api/ShopIdResolver.cs
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Api;

public sealed class ShopIdResolver
{
    private readonly Dictionary<LauncherId, int> _map = new();
    private static readonly Dictionary<LauncherId, string[]> FallbackNames = new()
    {
        [LauncherId.Epic] = ["Epic Game Store", "Epic Games Store"],
        [LauncherId.Ubisoft] = ["Ubisoft Store", "Ubisoft Connect"],
        [LauncherId.BattleNet] = ["Battle.net", "Blizzard Shop"],
        [LauncherId.Xbox] = ["Microsoft Store", "Xbox Store"]
    };

    public void LoadFromShopMap(IReadOnlyDictionary<string, int> shopMapByTitle)
    {
        foreach (var (launcher, names) in FallbackNames)
        {
            foreach (var name in names)
            {
                if (shopMapByTitle.TryGetValue(name, out var id))
                {
                    _map[launcher] = id;
                    break;
                }
            }
        }
    }

    public int GetShopId(LauncherId launcher) =>
        _map.TryGetValue(launcher, out var id)
            ? id
            : throw new InvalidOperationException($"Shop ID not resolved for {launcher}");
}
```

- [ ] **Step 3: Implement ItadApiClient**

Use `HttpClient` with base URL `https://api.isthereanydeal.com`. Key endpoints:

- `PUT /profiles/link/v1` — body `{ accountId, accountName }`, header `Authorization: Bearer {token}`, returns `{ token }`
- `PUT /profiles/sync/collection/v1` — header `ITAD-Profile: {profileToken}`
- `PUT /profiles/sync/waitlist/v1` — same header
- `GET /waitlist/games/v1` — returns game list with ITAD IDs
- `DELETE /waitlist/games/v1` — body array of ITAD game IDs
- `GET /service/shops/map/v1` — shop name → ID map

Serialize payloads with `System.Text.Json`. Map `SyncGamePayload` to ITAD JSON shape (`shop`, `id`, `title`, optional `playtime`, `lastPlayed` as ISO8601 string).

- [ ] **Step 4: Build and commit**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

```powershell
git add src/ITAD.LibrarySync.Core/Api/
git commit -m "feat: add ITAD API client and shop ID resolver"
```

---

### Task 6: OAuth + Token Storage

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Auth/TokenStorage.cs`
- Create: `src/ITAD.LibrarySync.Core/Auth/ItadOAuthService.cs`
- Create: `src/ITAD.LibrarySync.Core/Auth/OAuthTokens.cs`

- [ ] **Step 1: Implement DPAPI token storage**

```csharp
// src/ITAD.LibrarySync.Core/Auth/OAuthTokens.cs
namespace ITAD.LibrarySync.Core.Auth;

public sealed record OAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

// src/ITAD.LibrarySync.Core/Auth/TokenStorage.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITAD.LibrarySync.Core.Auth;

public sealed class TokenStorage
{
    private readonly string _path;

    public TokenStorage()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "tokens.dat");
    }

    public void Save(OAuthTokens tokens)
    {
        var json = JsonSerializer.Serialize(tokens);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public OAuthTokens? Load()
    {
        if (!File.Exists(_path)) return null;
        var protectedBytes = File.ReadAllBytes(_path);
        var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<OAuthTokens>(Encoding.UTF8.GetString(plain));
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
```

- [ ] **Step 2: Implement ItadOAuthService**

Responsibilities:
- Build authorize URL: `https://isthereanydeal.com/oauth/authorize?client_id=...&redirect_uri=...&response_type=code&scope=...`
- Exchange code for tokens via ITAD token endpoint
- Refresh expired access tokens
- Expose `Task<string> GetValidAccessTokenAsync(CancellationToken ct)`

Store profile tokens separately in `%AppData%/ITADLibrarySync/profiles.json` (also DPAPI-protected).

- [ ] **Step 3: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Auth/
git commit -m "feat: add OAuth token storage and refresh service"
```

---

### Task 7: Profile Manager

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Profiles/ProfileManager.cs`
- Create: `src/ITAD.LibrarySync.Core/Profiles/ProfileConfig.cs`

- [ ] **Step 1: Implement profile config mapping**

```csharp
// src/ITAD.LibrarySync.Core/Profiles/ProfileConfig.cs
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Profiles;

public static class ProfileConfig
{
    public static (string AccountId, string AccountName) Get(LauncherId launcher) => launcher switch
    {
        LauncherId.Epic => ("epic", "Epic Games Library"),
        LauncherId.Ubisoft => ("ubisoft", "Ubisoft Connect Library"),
        LauncherId.BattleNet => ("battlenet", "Battle.net Library"),
        LauncherId.Xbox => ("xbox", "Microsoft Store Library"),
        _ => throw new ArgumentOutOfRangeException(nameof(launcher))
    };
}
```

- [ ] **Step 2: Implement ProfileManager**

```csharp
// src/ITAD.LibrarySync.Core/Profiles/ProfileManager.cs
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Profiles;

public sealed class ProfileManager(IItadApiClient api, ItadOAuthService oauth, ProfileTokenStorage storage)
{
    public async Task<string> GetOrLinkProfileTokenAsync(LauncherId launcher, CancellationToken ct = default)
    {
        if (storage.TryGet(launcher, out var existing))
            return existing;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var (accountId, accountName) = ProfileConfig.Get(launcher);
        var profileToken = await api.LinkProfileAsync(accessToken, accountId, accountName, ct);
        storage.Save(launcher, profileToken);
        return profileToken;
    }
}
```

Also create `ProfileTokenStorage` mirroring `TokenStorage` pattern for `{ LauncherId → profileToken }` dictionary.

- [ ] **Step 3: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Profiles/
git commit -m "feat: add per-launcher ITAD profile manager"
```

---

### Task 8: Launcher Readers

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Launchers/ILauncherReader.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/EpicReader.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/UbisoftReader.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/BattleNetReader.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/XboxReader.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/LauncherReaderFactory.cs`

- [ ] **Step 1: Define ILauncherReader**

```csharp
// src/ITAD.LibrarySync.Core/Launchers/ILauncherReader.cs
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public interface ILauncherReader
{
    LauncherId Launcher { get; }
    Task<LauncherReadResult> ReadAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Implement EpicReader using GameCollector**

```csharp
// src/ITAD.LibrarySync.Core/Launchers/EpicReader.cs
using GameCollector.StoreHandlers.EGS;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Launchers;

public sealed class EpicReader : ILauncherReader
{
    public LauncherId Launcher => LauncherId.Epic;

    public Task<LauncherReadResult> ReadAsync(CancellationToken ct = default)
    {
        try
        {
            var handler = new EGSHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var games = handler.FindAllGames().ToList();
            // Map GameCollector results to StoreGame list for owned
            // Wishlist: best-effort — attempt secondary read from EGS cache if handler exposes it; else empty with wishlistReadable=false
            // Return LauncherReadResult with IsDetected=true when handler finds install path
        }
        catch (Exception ex)
        {
            return Task.FromResult(new LauncherReadResult(
                LauncherId.Epic, false, false, [], [], ex.Message));
        }
    }
}
```

Implement mapping logic: extract `StoreId` from GameCollector game ID field, `Title` from display name, optional playtime if available.

- [ ] **Step 3: Implement UbisoftReader, BattleNetReader, XboxReader**

Same pattern using:
- `UbisoftHandler`
- `BattleNetHandler`
- `XboxHandler`

For Ubisoft/BattleNet/Xbox: `Wishlist` returns empty list, set a flag or use `wishlistReadable: false` semantics via empty wishlist + documented behavior in orchestrator (WaitlistFilter.ShouldSkipWaitlistSync).

- [ ] **Step 4: Create LauncherReaderFactory**

Returns `IReadOnlyList<ILauncherReader>` for all four launchers.

- [ ] **Step 5: Build and commit**

Run: `dotnet build`

```powershell
git add src/ITAD.LibrarySync.Core/Launchers/
git commit -m "feat: add GameCollector-based launcher readers"
```

---

### Task 9: Sync Services

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Sync/CollectionSyncService.cs`
- Create: `src/ITAD.LibrarySync.Core/Sync/WaitlistSyncService.cs`
- Create: `src/ITAD.LibrarySync.Core/Sync/WaitlistCleanupService.cs`
- Create: `src/ITAD.LibrarySync.Core/Sync/SyncPayloadBuilder.cs`

- [ ] **Step 1: Implement SyncPayloadBuilder**

```csharp
// src/ITAD.LibrarySync.Core/Sync/SyncPayloadBuilder.cs
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Sync;

public sealed class SyncPayloadBuilder(ShopIdResolver shopIds)
{
    public SyncGamePayload ToPayload(StoreGame game) =>
        new(
            Shop: shopIds.GetShopId(game.Launcher),
            Id: game.StoreId,
            Title: game.Title,
            Playtime: game.PlaytimeMinutes,
            LastPlayed: game.LastPlayed);
}
```

- [ ] **Step 2: Implement CollectionSyncService**

```csharp
public sealed class CollectionSyncService(
    IItadApiClient api,
    ItadOAuthService oauth,
    ProfileManager profiles,
    SyncPayloadBuilder payloadBuilder)
{
    public async Task<ItadSyncResponse?> SyncAsync(
        LauncherReadResult read,
        CancellationToken ct = default)
    {
        if (WaitlistFilter.ShouldSkipCollectionSync(read.Owned))
            return null;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var profileToken = await profiles.GetOrLinkProfileTokenAsync(read.Launcher, ct);
        var payloads = read.Owned.Select(payloadBuilder.ToPayload).ToList();
        return await api.SyncCollectionAsync(accessToken, profileToken, payloads, ct);
    }
}
```

- [ ] **Step 3: Implement WaitlistSyncService**

Same pattern using `WaitlistFilter.RemoveOwnedGames` before building payloads. Call `SyncWaitlistAsync`. Return `null` when `ShouldSkipWaitlistSync` is true.

- [ ] **Step 4: Implement WaitlistCleanupService**

```csharp
public sealed class WaitlistCleanupService(IItadApiClient api, ItadOAuthService oauth, SyncPayloadBuilder payloadBuilder)
{
    public async Task<int> RemoveOwnedFromGlobalWaitlistAsync(
        IReadOnlyList<StoreGame> allOwned,
        CancellationToken ct = default)
    {
        if (allOwned.Count == 0) return 0;

        var accessToken = await oauth.GetValidAccessTokenAsync(ct);
        var waitlistIds = await api.GetWaitlistGameIdsAsync(accessToken, ct);
        if (waitlistIds.Count == 0) return 0;

        // Group owned by shop, lookup ITAD IDs via /lookup/id/shop/{shopId}/v1
        // Intersect with waitlistIds
        // DELETE matched IDs
        // Return count removed
    }
}
```

- [ ] **Step 5: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Sync/
git commit -m "feat: add collection, waitlist, and global cleanup sync services"
```

---

### Task 10: SyncOrchestrator (TDD)

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Sync/SyncOrchestrator.cs`
- Create: `src/ITAD.LibrarySync.Core/Sync/ISyncOrchestrator.cs`
- Create: `tests/ITAD.LibrarySync.Core.Tests/Sync/SyncOrchestratorTests.cs`

- [ ] **Step 1: Write failing orchestrator tests**

Test cases with mocked services:
1. Skips collection sync when owned list empty
2. Filters owned from waitlist before waitlist sync
3. Runs global cleanup after all launchers
4. Continues on partial launcher failure
5. Waits 30s between launcher profiles (use injectable `IDelayProvider` for tests)

- [ ] **Step 2: Implement ISyncOrchestrator**

```csharp
public interface ISyncOrchestrator
{
    Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        IReadOnlyList<LauncherId>? launchers = null,
        CancellationToken ct = default);
}
```

Pipeline in `SyncAllAsync`:
1. Load shop map into `ShopIdResolver`
2. For each enabled launcher reader:
   a. `ReadAsync`
   b. Collection sync (or skip)
   c. Waitlist sync (or skip)
   d. Delay 30s (except after last)
3. Aggregate all owned → global cleanup
4. Return `SyncResult` per launcher

- [ ] **Step 3: Run tests — expect PASS**

Run: `dotnet test tests/ITAD.LibrarySync.Core.Tests --filter SyncOrchestratorTests -v n`

- [ ] **Step 4: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Sync/SyncOrchestrator.cs tests/ITAD.LibrarySync.Core.Tests/Sync/SyncOrchestratorTests.cs
git commit -m "feat: add sync orchestrator with empty-list protection"
```

---

### Task 11: Sync Scheduler

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Scheduling/SyncScheduler.cs`
- Create: `src/ITAD.LibrarySync.Core/Scheduling/SyncScheduleOptions.cs`

- [ ] **Step 1: Implement scheduler**

```csharp
// src/ITAD.LibrarySync.Core/Scheduling/SyncScheduleOptions.cs
namespace ITAD.LibrarySync.Core.Scheduling;

public enum SyncInterval { Disabled, Every6Hours, Every12Hours, Every24Hours, Weekly }

public sealed class SyncScheduleOptions
{
    public SyncInterval Interval { get; set; } = SyncInterval.Disabled;
    public bool SyncOnStartup { get; set; }
}

// src/ITAD.LibrarySync.Core/Scheduling/SyncScheduler.cs
public sealed class SyncScheduler(ISyncOrchestrator orchestrator) : IDisposable
{
    private PeriodicTimer? _timer;

    public void Apply(SyncScheduleOptions options)
    {
        _timer?.Dispose();
        _timer = null;

        var period = options.Interval switch
        {
            SyncInterval.Every6Hours => TimeSpan.FromHours(6),
            SyncInterval.Every12Hours => TimeSpan.FromHours(12),
            SyncInterval.Every24Hours => TimeSpan.FromHours(24),
            SyncInterval.Weekly => TimeSpan.FromDays(7),
            _ => (TimeSpan?)null
        };

        if (period is not null)
            _ = RunLoopAsync(period.Value);
    }

    private async Task RunLoopAsync(TimeSpan period)
    {
        _timer = new PeriodicTimer(period);
        while (await _timer.WaitForNextTickAsync())
            await orchestrator.SyncAllAsync();
    }

    public void Dispose() => _timer?.Dispose();
}
```

Persist `SyncScheduleOptions` to `%AppData%/ITADLibrarySync/settings.json`.

- [ ] **Step 2: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Scheduling/
git commit -m "feat: add optional periodic sync scheduler"
```

---

### Task 12: WPF App Shell + DI

**Files:**
- Modify: `src/ITAD.LibrarySync.App/App.xaml`
- Modify: `src/ITAD.LibrarySync.App/App.xaml.cs`
- Create: `src/ITAD.LibrarySync.App/appsettings.json`

- [ ] **Step 1: Configure DI in App.xaml.cs**

Register all Core services:
- `ItadOptions` from embedded config (ClientId placeholder until ITAD app registered)
- `IItadApiClient` → `ItadApiClient`
- `TokenStorage`, `ItadOAuthService`, `ProfileManager`
- All four `ILauncherReader` implementations
- `ShopIdResolver`, `SyncPayloadBuilder`
- Sync services + `ISyncOrchestrator`
- `SyncScheduler`
- `TrayIconService`, `NotificationService`

- [ ] **Step 2: Set startup behavior**

On startup:
- Initialize tray icon
- If first run → show wizard
- If `SyncOnStartup` → trigger sync
- Don't show main window

- [ ] **Step 3: Commit**

```powershell
git add src/ITAD.LibrarySync.App/
git commit -m "feat: add WPF app shell with dependency injection"
```

---

### Task 13: OAuth WebView Window

**Files:**
- Create: `src/ITAD.LibrarySync.App/Views/OAuthWebViewWindow.xaml`
- Create: `src/ITAD.LibrarySync.App/Views/OAuthWebViewWindow.xaml.cs`

- [ ] **Step 1: Create WebView2 OAuth popup**

- Start localhost HTTP listener on random port before navigation
- Navigate WebView2 to ITAD authorize URL
- Intercept redirect to `http://127.0.0.1:{port}/callback?code=...`
- Pass code to `ItadOAuthService.ExchangeCodeAsync`
- Close window on success/failure

- [ ] **Step 2: Wire to Settings "Connect to ITAD" button**

- [ ] **Step 3: Manual test**

Run app → Connect → complete ITAD OAuth → verify token saved in `%AppData%/ITADLibrarySync/tokens.dat`

- [ ] **Step 4: Commit**

```powershell
git add src/ITAD.LibrarySync.App/Views/OAuthWebViewWindow.*
git commit -m "feat: add WebView2 OAuth login window"
```

---

### Task 14: Tray Icon + Notifications

**Files:**
- Create: `src/ITAD.LibrarySync.App/Services/TrayIconService.cs`
- Create: `src/ITAD.LibrarySync.App/Services/NotificationService.cs`

- [ ] **Step 1: Implement TrayIconService**

Context menu items per spec:
- Sync Now → `orchestrator.SyncAllAsync()`
- Sync Epic / Ubisoft / Battle.net / Microsoft → single launcher sync
- Settings → open SettingsWindow
- View Last Sync Log → open log file
- Exit

Update icon color/state based on last sync result.

- [ ] **Step 2: Implement NotificationService**

Use `Microsoft.Toolkit.Uwp.Notifications` for sync complete / partial error / token expired toasts.

- [ ] **Step 3: Commit**

```powershell
git add src/ITAD.LibrarySync.App/Services/
git commit -m "feat: add tray icon and Windows toast notifications"
```

---

### Task 15: Settings Window

**Files:**
- Create: `src/ITAD.LibrarySync.App/Views/SettingsWindow.xaml`
- Create: `src/ITAD.LibrarySync.App/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Build SettingsViewModel**

Properties:
- `IsConnected`, `AccountName`
- `LauncherStatuses` — ObservableCollection with detection/sync stats
- `SelectedInterval`, `SyncOnStartup`, `StartWithWindows`, `ShowNotifications`
- Commands: `ConnectCommand`, `DisconnectCommand`, `SyncNowCommand`, `TestReadCommand`

- [ ] **Step 2: Build SettingsWindow with 4 tabs**

Tab 1: ITAD Connection
Tab 2: Launchers (DataGrid with enable toggles)
Tab 3: Sync Settings
Tab 4: General

- [ ] **Step 3: Commit**

```powershell
git add src/ITAD.LibrarySync.App/Views/SettingsWindow.* src/ITAD.LibrarySync.App/ViewModels/
git commit -m "feat: add settings window with launcher and sync options"
```

---

### Task 16: First-Run Wizard

**Files:**
- Create: `src/ITAD.LibrarySync.App/Views/FirstRunWizard.xaml`
- Create: `src/ITAD.LibrarySync.App/ViewModels/FirstRunWizardViewModel.cs`

- [ ] **Step 1: Implement 4-step wizard**

Steps: Welcome → ITAD OAuth → Launcher scan results → Optional first sync

Persist `HasCompletedFirstRun` flag in settings.json.

- [ ] **Step 2: Commit**

```powershell
git add src/ITAD.LibrarySync.App/Views/FirstRunWizard.* src/ITAD.LibrarySync.App/ViewModels/FirstRunWizardViewModel.cs
git commit -m "feat: add first-run setup wizard"
```

---

### Task 17: Logging

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Logging/FileLogger.cs`

- [ ] **Step 1: Implement file logger**

Write to `%AppData%/ITADLibrarySync/logs/sync-{date}.log`. Never log tokens. Include sync results per launcher.

- [ ] **Step 2: Wire "View Last Sync Log" tray menu item**

- [ ] **Step 3: Commit**

```powershell
git add src/ITAD.LibrarySync.Core/Logging/
git commit -m "feat: add file-based sync logging"
```

---

### Task 18: Release Pipeline + README

**Files:**
- Create: `.github/workflows/release.yml`
- Modify: `README.md`

- [ ] **Step 1: Add GitHub Actions release workflow**

Build `win-x64` self-contained publish:

```yaml
dotnet publish src/ITAD.LibrarySync.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Upload artifact to GitHub Release on tag `v*`.

- [ ] **Step 2: Write README**

Include:
- What the app does
- Prerequisites (Windows 10+, WebView2, launchers installed + logged in)
- ITAD OAuth app registration note for contributors
- Setup steps
- Sync behavior (collection + waitlist + owned removal)
- Limitations (waitlist import Epic-only best-effort)

- [ ] **Step 3: Commit**

```powershell
git add .github/workflows/release.yml README.md LICENSE
git commit -m "docs: add README and GitHub release workflow"
```

---

### Task 19: Manual Integration Test

- [ ] **Step 1: Register ITAD OAuth app**

Register at ITAD apps page. Set redirect URI pattern. Embed ClientId in `appsettings.json`.

- [ ] **Step 2: Run manual checklist from spec**

- [ ] ITAD OAuth connect + token refresh
- [ ] Epic collection sync (owned + not-installed)
- [ ] Epic waitlist sync + owned exclusion
- [ ] Global waitlist cleanup
- [ ] Ubisoft / Battle.net / Xbox collection sync
- [ ] Empty-list protection (launcher closed)
- [ ] Scheduler + tray states + toasts
- [ ] First-run wizard

- [ ] **Step 3: Fix any issues found**

- [ ] **Step 4: Tag release**

```powershell
git tag v0.1.0
git push origin v0.1.0
```

---

## Spec Coverage Checklist

| Spec Requirement | Task |
|-----------------|------|
| Open-source OAuth app | Task 5, 6, 13, 19 |
| WPF tray app | Task 12, 14, 15 |
| Windows only | Task 1, 18 |
| 4 launchers v1 | Task 8 |
| Hybrid sync (manual + scheduler) | Task 10, 11, 14 |
| Local cache reading | Task 8 |
| Collection sync | Task 9 |
| Waitlist sync + owned exclusion | Task 4, 9, 10 |
| Global waitlist cleanup | Task 9, 10 |
| Empty-list protection | Task 4, 10 |
| Per-store ITAD profiles | Task 7 |
| Shop ID resolution | Task 5 |
| DPAPI token storage | Task 6 |
| First-run wizard | Task 16 |
| Unit tests | Task 3, 4, 10 |

## Plan Self-Review

- No TBD/TODO placeholders in task steps
- Type names consistent: `StoreGame`, `SyncGamePayload`, `LauncherId`, `ISyncOrchestrator`
- All spec sections mapped to tasks
- Scope limited to v1 non-goals (no Steam/GOG, no cross-platform)
