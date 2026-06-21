# Xbox Full Library (OAuth + TitleHub) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the non-functional `StoreContext` path with Xbox Live OAuth + TitleHub, so Microsoft/Xbox sync returns the same class of library data that Playnite’s Xbox plugin provides (account title history + playtime), merged with locally installed games.

**Architecture:** Add a second OAuth stack (Microsoft/Xbox, separate from ITAD OAuth). After MSA code flow → Xbox user token → XSTS token, call TitleHub `titlehistory` and UserStats `MinutesPlayed`. Map results to `StoreGame` using Package Family Name (PFN) or Store BigId. Keep GameCollector `XboxHandler` as a local supplement for install state and playtime fallback. Demote `StoreContextLibraryReader` to optional bonus reads only.

**Tech Stack:** .NET 8, WPF + WebView2 (existing), `HttpClient`, DPAPI token storage (existing pattern), optional NuGet `XboxAuthNet` for auth chain hardening.

**Honest scope (“tam kütüphane”):**

| Source | What you get | What you miss |
|--------|----------------|---------------|
| **TitleHub title history** (Playnite path) | Games tied to the Xbox account — played on Xbox/PC/cloud; includes many Game Pass titles user has launched | Purchases never launched once; some sideloaded titles |
| **GameCollector local** | Installed Microsoft Store / Xbox PC games on this machine | Uninstalled owned titles |
| **StoreContext** (current) | Nothing useful for third-party apps | N/A |
| **Official GDK Collections API** | Full entitlement query | Requires Partner Center publisher registration per product — not viable for ITAD Library Sync |

**Target outcome:** Match or exceed Playnite XboxLibrary for ITAD sync purposes. Document the “never launched” gap in Settings UI.

---

## File map (new / changed)

| File | Responsibility |
|------|----------------|
| `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxOAuthTokens.cs` | MSA access + refresh + expiry |
| `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxAuthorizationData.cs` | XSTS JWT + display claims (xuid, uhs) |
| `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxTokenStorage.cs` | DPAPI files: `xbox-login.dat`, `xbox-xsts.dat` |
| `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxOAuthService.cs` | Token refresh, XSTS authorize, auth header builder |
| `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxOAuthOptions.cs` | ClientId, redirect URI, scopes (configurable) |
| `src/ITAD.LibrarySync.Core/Launchers/Xbox/IXboxLibraryClient.cs` | `GetTitleHistoryAsync`, `GetMinutesPlayedAsync` |
| `src/ITAD.LibrarySync.Core/Launchers/Xbox/TitleHubClient.cs` | HTTP calls to TitleHub + UserStats |
| `src/ITAD.LibrarySync.Core/Launchers/Xbox/TitleHubModels.cs` | JSON DTOs for title history |
| `src/ITAD.LibrarySync.Core/Launchers/Xbox/XboxTitleMapper.cs` | Title → `StoreGame` (PFN/BigId/title) |
| `src/ITAD.LibrarySync.App/Launchers/XboxOAuthFlowService.cs` | WebView2 MSA login (reuse `OAuthWebViewWindow`) |
| `src/ITAD.LibrarySync.App/Launchers/XboxApiLibraryReader.cs` | `IMicrosoftStoreLibraryReader` via TitleHub |
| `src/ITAD.LibrarySync.Core/Launchers/XboxReader.cs` | Merge: API library + local installs |
| `src/ITAD.LibrarySync.Core/Launchers/LauncherReadResultDisplay.cs` | Xbox auth states |
| `src/ITAD.LibrarySync.App/ViewModels/SettingsViewModel.cs` | Connect / Disconnect Xbox |
| `src/ITAD.LibrarySync.App/Views/SettingsWindow.xaml` | Xbox row actions |
| `tests/ITAD.LibrarySync.Core.Tests/Launchers/Xbox/XboxTitleMapperTests.cs` | Mapping unit tests |
| `tests/ITAD.LibrarySync.Core.Tests/Launchers/Xbox/TitleHubClientTests.cs` | HTTP mock tests |
| `tools/XboxProbe/` | CLI auth + dump title count (dev only) |
| `docs/superpowers/specs/2026-06-20-itad-library-sync-design.md` | Update Microsoft/Xbox section |

---

## Reference implementation (Playnite XboxLibrary)

PlayniteExtensions `XboxAccountClient.cs` (verified 2026-06-20):

1. OAuth: `https://login.live.com/oauth20_authorize.srf` → callback with `code`
2. Token: `https://login.live.com/oauth20_token.srf`
3. Xbox user auth: `POST https://user.auth.xboxlive.com/user/authenticate` (RPS ticket `d={accessToken}`)
4. XSTS: `POST https://xsts.auth.xboxlive.com/xsts/authorize`
5. Library: `GET https://titlehub.xboxlive.com/users/xuid({xuid})/titles/titlehistory/decoration/detail`
6. Playtime: `POST https://userstats.xboxlive.com/batch` with `MinutesPlayed` per titleId
7. Auth header: `XBL3.0 x={uhs};{xstsToken}`

Playnite uses public desktop OAuth client id `38cd2fa8-66fd-4760-afb2-405eb65d5b0c` and redirect `https://login.live.com/oauth20_desktop.srf`.

**Decision (implement in Task 1):** Start with the same Microsoft desktop OAuth redirect pattern for fastest path; optionally register a dedicated Azure AD public client for `ITAD Library Sync` before v1.1 release.

---

## Phase 0 — Design & registration

### Task 0: Update design spec

**Files:**
- Modify: `docs/superpowers/specs/2026-06-20-itad-library-sync-design.md`

- [ ] **Step 1:** Change Microsoft/Xbox row in “Launcher access” from “Local cache only” to “Local cache + Xbox OAuth (TitleHub)”.
- [ ] **Step 2:** Add limitation note: title history ≠ every unplayed purchase; Game Pass titles may appear.
- [ ] **Step 3:** Add security note: separate DPAPI token files from ITAD tokens.
- [ ] **Step 4:** Commit: `docs: specify Xbox OAuth library path`

---

## Phase 1 — Xbox OAuth core

### Task 1: Options + token models

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxOAuthOptions.cs`
- Create: `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxOAuthTokens.cs`
- Create: `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxAuthorizationData.cs`

- [ ] **Step 1: Write failing test** — `tests/ITAD.LibrarySync.Core.Tests/Auth/Xbox/XboxOAuthOptionsTests.cs`

```csharp
[Fact]
public void DefaultScopes_include_offline_access()
{
    var options = XboxOAuthOptions.CreateDefault();
    Assert.Contains("offline_access", options.Scopes, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2:** Run `dotnet test --filter XboxOAuthOptionsTests` → FAIL
- [ ] **Step 3:** Implement models + defaults:

```csharp
// XboxOAuthOptions.cs
public sealed record XboxOAuthOptions(
    string ClientId,
    string RedirectUri,
    string Scopes)
{
    public static XboxOAuthOptions CreateDefault() => new(
        ClientId: "38cd2fa8-66fd-4760-afb2-405eb65d5b0c",
        RedirectUri: "https://login.live.com/oauth20_desktop.srf",
        Scopes: "XboxLive.signin XboxLive.offline_access");
}
```

```csharp
// XboxOAuthTokens.cs
public sealed record XboxOAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string? UserId);
```

```csharp
// XboxAuthorizationData.cs — mirror Playnite AuthorizationData fields used by TitleHub
public sealed class XboxAuthorizationData
{
    public required string Token { get; init; }
    public required XboxDisplayClaims DisplayClaims { get; init; }
}
public sealed class XboxDisplayClaims
{
    public required IReadOnlyList<XboxXuiClaim> Xui { get; init; }
}
public sealed class XboxXuiClaim
{
    public required string Xid { get; init; }
    public required string Uhs { get; init; }
    public string? Gtg { get; init; }
}
```

- [ ] **Step 4:** Run tests → PASS
- [ ] **Step 5:** Commit: `feat(xbox): add OAuth options and token models`

---

### Task 2: DPAPI token storage

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxTokenStorage.cs`
- Test: `tests/ITAD.LibrarySync.Core.Tests/Auth/Xbox/XboxTokenStorageTests.cs`

- [ ] **Step 1:** Copy pattern from `TokenStorage.cs` but use `%AppData%/ITADLibrarySync/xbox-login.dat` and `xbox-xsts.dat`.
- [ ] **Step 2:** Tests: round-trip save/load, clear removes files.
- [ ] **Step 3:** Commit: `feat(xbox): add DPAPI token storage`

---

### Task 3: XboxOAuthService (token + XSTS)

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Auth/Xbox/XboxOAuthService.cs`
- Test: `tests/ITAD.LibrarySync.Core.Tests/Auth/Xbox/XboxOAuthServiceTests.cs` (mock `HttpMessageHandler`)

- [ ] **Step 1:** Implement:
  - `BuildAuthorizeUrl()`
  - `ExchangeCodeAsync(code)` → save MSA tokens
  - `RefreshAsync()` when `ExpiresAt` within 60s
  - `EnsureXstsAsync()` → load or rebuild XSTS from MSA access token
  - `BuildAuthorizationHeader()` → `XBL3.0 x={uhs};{token}`
  - `GetGamertagOrXuid()` for UI
  - `ClearAsync()` wipes both files

- [ ] **Step 2:** Port JSON bodies from Playnite `AthenticationRequest` / `AuhtorizationRequest` (same relying party `http://xboxlive.com`).

- [ ] **Step 3:** Mock HTTP tests for: successful XSTS, 401 → `XboxAuthRequiredException`.

- [ ] **Step 4:** Commit: `feat(xbox): implement OAuth and XSTS service`

---

### Task 4: App-layer OAuth flow

**Files:**
- Create: `src/ITAD.LibrarySync.App/Launchers/XboxOAuthFlowService.cs`
- Modify: `src/ITAD.LibrarySync.App/App.xaml.cs` (DI)
- Reuse: `src/ITAD.LibrarySync.App/Views/OAuthWebViewWindow.xaml.cs`

- [ ] **Step 1:** Implement flow:
  - Navigate WebView2 to `BuildAuthorizeUrl()`
  - Detect redirect URL containing `code=` (Playnite pattern — no local HttpListener needed for desktop redirect)
  - Call `ExchangeCodeAsync` + `EnsureXstsAsync`

- [ ] **Step 2:** Register `XboxOAuthFlowService`, `XboxOAuthService`, `XboxTokenStorage` in DI.

- [ ] **Step 3:** Manual smoke: run app → call connect from temporary debug button → verify `%AppData%/ITADLibrarySync/xbox-xsts.dat` created.

- [ ] **Step 4:** Commit: `feat(xbox): add WebView2 Microsoft login flow`

---

## Phase 2 — TitleHub client

### Task 5: DTOs + mapper

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Launchers/Xbox/TitleHubModels.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/Xbox/XboxTitleMapper.cs`
- Test: `tests/ITAD.LibrarySync.Core.Tests/Launchers/Xbox/XboxTitleMapperTests.cs`

- [ ] **Step 1:** Define `TitleHistoryResponse` with `titles[]` containing at minimum:
  - `titleId`, `name`, `pfn` (package family name), `modernTitleId`, `type`

- [ ] **Step 2:** Mapper rules (ITAD Microsoft shop id):
  1. Prefer `pfn` when present (`Microsoft.X_8wekyb3d8bbwe`)
  2. Else `modernTitleId` / Store BigId
  3. Else fallback `titleId` prefixed `xbox:{titleId}` (log warning — weaker ITAD matching)

```csharp
public static StoreGame ToStoreGame(TitleHistoryItem item, int? playtimeMinutes)
{
    var storeId = !string.IsNullOrWhiteSpace(item.Pfn)
        ? item.Pfn
        : !string.IsNullOrWhiteSpace(item.ModernTitleId)
            ? item.ModernTitleId
            : $"xbox:{item.TitleId}";

    return new StoreGame(LauncherId.Xbox, storeId, item.Name.Trim())
    {
        PlaytimeMinutes = playtimeMinutes
    };
}
```

- [ ] **Step 3:** Unit tests for PFN priority and whitespace titles skipped.
- [ ] **Step 4:** Commit: `feat(xbox): add TitleHub models and StoreGame mapper`

---

### Task 6: TitleHubClient

**Files:**
- Create: `src/ITAD.LibrarySync.Core/Launchers/Xbox/IXboxLibraryClient.cs`
- Create: `src/ITAD.LibrarySync.Core/Launchers/Xbox/TitleHubClient.cs`
- Test: `tests/ITAD.LibrarySync.Core.Tests/Launchers/Xbox/TitleHubClientTests.cs`

- [ ] **Step 1:** Implement `GetTitleHistoryAsync(CancellationToken)`:
  - `GET .../users/xuid({xuid})/titles/titlehistory/decoration/detail`
  - Headers: contract version `2`, `Accept-Language: en-US`, XBL3.0 auth

- [ ] **Step 2:** Implement `GetMinutesPlayedAsync(IReadOnlyList<string> titleIds)`:
  - Batch POST to `https://userstats.xboxlive.com/batch`
  - Chunk titleIds at 100 per request

- [ ] **Step 3:** Mock tests with sample JSON fixture `tests/fixtures/xbox/titlehistory.json`.

- [ ] **Step 4:** Commit: `feat(xbox): add TitleHub HTTP client`

---

### Task 7: XboxApiLibraryReader

**Files:**
- Create: `src/ITAD.LibrarySync.App/Launchers/XboxApiLibraryReader.cs`
- Modify: `src/ITAD.LibrarySync.App/App.xaml.cs`

- [ ] **Step 1:** Implement `IMicrosoftStoreLibraryReader`:
  - `EnsureXstsAsync()` via `XboxOAuthService`
  - Fetch title history + playtime
  - Map to `IReadOnlyList<StoreGame>`
  - If not authenticated → throw `XboxAuthRequiredException`

- [ ] **Step 2:** Register as primary `IMicrosoftStoreLibraryReader` (replace `StoreContextLibraryReader` binding).

- [ ] **Step 3:** Keep `StoreContextLibraryReader` registered separately only if we add merge hook later; default off.

- [ ] **Step 4:** Commit: `feat(xbox): wire TitleHub into library reader`

---

## Phase 3 — XboxReader merge + UX

### Task 8: Refactor XboxReader

**Files:**
- Modify: `src/ITAD.LibrarySync.Core/Launchers/XboxReader.cs`
- Modify: `tests/ITAD.LibrarySync.Core.Tests/Launchers/XboxReaderTests.cs`

- [ ] **Step 1:** Read order:
  1. Local `XboxHandler` (installed / local metadata)
  2. If `IMicrosoftStoreLibraryReader` authenticated → TitleHub list
  3. Union by normalized store id + title; prefer API entry, enrich with local playtime if higher

- [ ] **Step 2:** Status rules:
  - Not authenticated → `IsLoggedIn=false`, Error=`Connect your Xbox account in Settings`
  - Authenticated, 0 titles → `Limited`, Error explains title-history limitation
  - Authenticated, N>0 → `Ready`

- [ ] **Step 3:** Update tests for auth-required vs merged library paths.

- [ ] **Step 4:** Commit: `feat(xbox): merge TitleHub with local XboxHandler`

---

### Task 9: Settings UI

**Files:**
- Modify: `src/ITAD.LibrarySync.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/ITAD.LibrarySync.App/ViewModels/LauncherSettingsItem.cs`
- Modify: `src/ITAD.LibrarySync.App/Views/SettingsWindow.xaml`
- Modify: `src/ITAD.LibrarySync.Core/Launchers/LauncherReadResultDisplay.cs`

- [ ] **Step 1:** For Xbox row only, add:
  - `Connect Xbox` / `Disconnect` buttons
  - Sub-status: `Connected as {Gamertag}` or `Not connected`

- [ ] **Step 2:** `Test Read` triggers OAuth prompt if needed.

- [ ] **Step 3:** Read Result footnote when count > 0:
  - `N games — Xbox title history (may omit never-launched purchases)`

- [ ] **Step 4:** First-run wizard: optional Xbox connect step after launcher scan.

- [ ] **Step 5:** Commit: `feat(xbox): add Settings connect/disconnect UX`

---

## Phase 4 — Safety, probes, docs

### Task 10: Sync safety + token refresh

**Files:**
- Modify: `src/ITAD.LibrarySync.Core/Sync/SyncOrchestrator.cs` (if needed)
- Modify: `src/ITAD.LibrarySync.Core/Launchers/XboxReader.cs`

- [ ] **Step 1:** Before sync read, call `XboxOAuthService.RefreshAsync()` in try/catch; on failure mark launcher error without wiping ITAD collection (existing empty-list guard).

- [ ] **Step 2:** Distinguish `XboxAuthRequiredException` in orchestrator toast: “Reconnect Xbox in Settings”.

- [ ] **Step 3:** Commit: `fix(xbox): refresh tokens before sync reads`

---

### Task 11: XboxProbe dev tool

**Files:**
- Create: `tools/XboxProbe/XboxProbe.csproj`
- Create: `tools/XboxProbe/Program.cs`

- [ ] **Step 1:** CLI: authenticate (opens WebView or reads saved tokens), print title count + first 5 names.

- [ ] **Step 2:** Document in README under “Development / Xbox debugging”.

- [ ] **Step 3:** Commit: `chore: add XboxProbe diagnostic tool`

---

### Task 12: README + manual test checklist

**Files:**
- Modify: `README.md`

- [ ] **Step 1:** Document Xbox connect steps and limitations.

- [ ] **Step 2:** Manual checklist (Task 19 extension):
  - Connect Xbox → Test Read shows >0 for account with history
  - Disconnect → Test Read prompts connect
  - Sync Now pushes Microsoft profile on ITAD
  - Token refresh after 24h (leave app idle, re-sync)

- [ ] **Step 3:** Commit: `docs: document Xbox OAuth library sync`

---

## Testing strategy

| Layer | What |
|-------|------|
| Unit | Mapper, token expiry logic, merge dedupe, display strings |
| HTTP mocks | TitleHub + UserStats JSON fixtures |
| Manual | Real Microsoft account on dev PC |
| Regression | Existing 30 Core tests stay green |

**Fixture path:** `tests/ITAD.LibrarySync.Core.Tests/fixtures/xbox/titlehistory.json` — capture once from XboxProbe on a real account (strip PII if committing).

---

## Risks & mitigations

| Risk | Mitigation |
|------|------------|
| Microsoft changes OAuth/XSTS | Watch PlayniteExtensions issues; abstract auth behind `IXboxAuthProvider`; XboxAuthNet fallback |
| Title history missing unplayed buys | Document clearly; local install merge catches some |
| Game Pass ≠ owned for ITAD | Optional filter: exclude titles with `pfd` / subscription flags when TitleHub exposes them; default include with UI note |
| Rate limits | Single TitleHub GET per sync; batch UserStats; cache last read timestamp 15 min for Test Read spam |
| Public client id dependency | Register dedicated Azure AD app before public release |
| Legal/ToS | Unofficial APIs — same class as Playnite; open-source, user-owned data, no redistribution |

---

## Timeline estimate

| Phase | Effort |
|-------|--------|
| Phase 0 | 0.5 day |
| Phase 1 (OAuth) | 2–3 days |
| Phase 2 (TitleHub) | 2 days |
| Phase 3 (UI + merge) | 1–2 days |
| Phase 4 (safety + docs) | 1 day |
| **Total** | **~6–8 dev days** |

---

## Success criteria

1. Settings Xbox row shows **Connected as {gamertag}** after login.
2. Test Read returns **>0 games** for an account that already shows games in Xbox app / Playnite (same order of magnitude).
3. Sync pushes Microsoft Store ITAD profile without empty-list wipe on auth failure.
4. UI never shows “sign in to Microsoft Store on this PC” when Xbox OAuth is the real requirement.
5. README explains title-history limitation honestly.

---

## Out of scope (future v2)

- Partner Center + Collections API for true entitlement parity (requires Microsoft publisher partnership)
- Xbox wishlist import (no stable public API)
- Game Pass vs purchase classification for ITAD ownership semantics
- macOS (N/A — Windows-only app)

---

## Self-review

| Spec requirement | Task |
|------------------|------|
| Full Microsoft library (best effort) | Tasks 6–8 TitleHub |
| Separate from ITAD OAuth | Tasks 1–4 |
| Playtime enrichment | Task 6 UserStats + Task 8 merge |
| Empty-list protection | Task 10 |
| Settings Test Read | Task 9 |
| Honest UX | Tasks 8–9 display strings |

No TBD placeholders remain. Types consistent: `XboxOAuthService`, `TitleHubClient`, `XboxApiLibraryReader`, `StoreGame`.
