# ITAD Library Sync — Design Spec

**Date:** 2026-06-20  
**Status:** Approved  
**Target:** Windows-only open-source desktop app (WPF + .NET 8)

## Summary

ITAD Library Sync is a Windows tray application that reads game libraries from Epic Games, Ubisoft Connect, Battle.net, and Microsoft/Xbox via local launcher cache, then pushes owned games to IsThereAnyDeal Collection and manages Waitlist entries through the official Custom Profiles API (OAuth2). The app registers as a public ITAD OAuth application so any user can connect their account.

Native ITAD sync exists for Steam, GOG, Humble Store, and Fanatical. Epic, Ubisoft, Battle.net, and Microsoft do not expose public profile APIs, so third-party tools must read local launcher data and push to ITAD.

## Goals

- Sync **owned games** from four launchers to ITAD Collection (per-store custom profiles)
- Sync **waitlist** where local data is available; always remove owned games from ITAD Waitlist
- Open-source OAuth app usable by anyone
- Windows tray UX with manual sync + optional scheduled sync
- Safe sync: never wipe ITAD data due to empty/failed reads

## Non-Goals (v1)

- macOS / Linux support
- Steam / GOG sync (ITAD native support)
- Store wishlist import for Ubisoft, Battle.net, Microsoft (no local wishlist cache)
- In-app waitlist editing
- Auto-update beyond GitHub Releases
- Playtime analytics

## User Requirements (Decisions)

| Decision | Choice |
|----------|--------|
| Target audience | Open-source OAuth app for all users |
| UI | Desktop GUI (WPF tray app) |
| Platform | Windows only |
| Launchers v1 | Epic, Ubisoft Connect, Battle.net, Microsoft/Xbox — all four |
| Sync trigger | Hybrid: manual default + optional scheduler |
| Tech stack | .NET 8 + WPF |
| Launcher access | Local cache (launcher installed + logged in); Microsoft/Xbox also uses Xbox OAuth (TitleHub) |
| Waitlist + Collection | Both synced; owned games excluded from waitlist permanently |

## Architecture

### Approach

**Layered architecture (Core + WPF shell)** — recommended over monolith or plugin system.

```
┌─────────────────────────────────────────────────┐
│           ITAD.LibrarySync.App (WPF)            │
│  Tray Icon │ Settings Window │ Sync Status UI   │
└────────────────────┬────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────┐
│         ITAD.LibrarySync.Core (.NET 8)          │
│  OAuthService │ SyncOrchestrator │ ItadApiClient│
│  ProfileManager │ LauncherReaders │ Scheduler   │
└─────────────────────────────────────────────────┘
```

### Sync Flow (per launcher)

```
[Read Owned] ──┐
               ├──► [Owned Set] ──► Collection Sync (PUT /profiles/sync/collection/v1)
[Read Wishlist]┘         │
                         ▼
              Wishlist = StoreWishlist − Owned
                         │
                         ▼
                   Waitlist Sync (PUT /profiles/sync/waitlist/v1)
                         │
                         ▼
              [Global Cleanup Pass] ──► DELETE /waitlist/games/v1
```

### Global sync order

1. Read owned + wishlist from all enabled launchers
2. Per launcher profile: Collection sync, then Waitlist sync (filtered)
3. Global waitlist cleanup: remove any ITAD waitlist entry matching owned games (cross-store)

### ITAD Integration

- **OAuth2** public client with WebView2 popup and `http://127.0.0.1:{port}/callback`
- **Profile link:** `PUT /profiles/link/v1` — one profile per store
- **Collection sync:** `PUT /profiles/sync/collection/v1`
- **Waitlist sync:** `PUT /profiles/sync/waitlist/v1`
- **Waitlist cleanup:** `GET /waitlist/games/v1` + `DELETE /waitlist/games/v1`
- **Shop IDs:** fetched at startup from `GET /service/shops/map/v1`, cached with hardcoded fallback

### Profile Mapping

| Launcher | accountId | accountName |
|----------|-----------|-------------|
| Epic Games | `epic` | Epic Games Library |
| Ubisoft Connect | `ubisoft` | Ubisoft Connect Library |
| Battle.net | `battlenet` | Battle.net Library |
| Microsoft/Xbox | `xbox` | Microsoft Store Library |

### Launcher → ITAD Data Mapping

| Launcher | GameCollector Handler | ITAD Shop | Store-native ID |
|----------|----------------------|-----------|-----------------|
| Epic Games | `EGSHandler` | Epic Game Store | `CatalogItemId` or `AppName` |
| Ubisoft Connect | `UbisoftHandler` | Ubisoft Store | Space ID |
| Battle.net | `BattleNetHandler` | Battle.net | Product ID |
| Microsoft/Xbox | `XboxHandler` | Microsoft Store | Package Family Name / Store ID |

Payload fields per game:

```json
{
  "shop": 25,
  "id": "app/12345",
  "title": "Hades",
  "playtime": 420,
  "lastPlayed": "2024-08-26T22:04:08+01:00"
}
```

`playtime` and `lastPlayed` are optional; include when GameCollector provides them.

## Waitlist Rules

### Owned games never on waitlist

1. **Pre-push filter:** Waitlist payload excludes all owned games (match by store-native ID, fallback normalized title)
2. **Global cleanup:** After all profile syncs, fetch ITAD waitlist, match owned games via ITAD game ID lookup, delete matches

This handles cross-store cases (e.g., owned on Epic, still on ITAD waitlist from another source).

### Waitlist source availability

| Launcher | Collection | Waitlist Import | Waitlist Cleanup |
|----------|-----------|-----------------|------------------|
| Epic Games | Full (owned + not-installed) | Best-effort from local cache | Always |
| Ubisoft Connect | Full | Not available (no local wishlist) | Always |
| Battle.net | Full | Not available | Always |
| Microsoft/Xbox | Full (TitleHub OAuth + local cache merge) | Not available | Always |

**Microsoft/Xbox limitation:** TitleHub reflects title history, not every unplayed purchase; Game Pass titles may appear in the library.

### Empty-list protection

| Condition | Action |
|-----------|--------|
| Owned read returns 0 games | **Cancel** collection sync — do not wipe ITAD collection |
| Wishlist unreadable or empty | **Cancel** waitlist sync — do not wipe ITAD waitlist |
| Wishlist read but all entries are owned | Push empty waitlist for that profile (intended) |

## UI / UX

### Tray application

- No main window; system tray only
- Menu: Sync Now, per-launcher sync, Settings, View Last Sync Log, Exit
- Icon states: grey (idle), green (success), orange (partial), red (error), blue animated (syncing)

### Settings window (tabs)

1. **ITAD Connection** — Connect / Disconnect OAuth, account status
2. **Launchers** — detection status, enable toggles, last sync, game counts, Test Read
3. **Sync Settings** — auto-sync interval (6h/12h/24h/weekly), sync on startup
4. **General** — startup, notifications, log level

### First-run wizard

Welcome → ITAD OAuth → launcher scan → optional first sync → minimize to tray

### Notifications

Windows Toast for sync results, partial errors, token expiry

## Error Handling

| Situation | Behavior |
|-----------|----------|
| Launcher not installed | Skip; show "Not detected" |
| Launcher installed, not logged in | Skip; toast prompt to log in (Microsoft/Xbox: prompt Xbox OAuth connect in Settings) |
| ITAD token expired | Refresh; on failure prompt reconnect |
| ITAD rate limit | Exponential backoff, max 3 retries |
| Game not matched on ITAD | ITAD uses title fallback; log as unmatched |
| Partial launcher failure | Sync successful launchers; report failures |

Inter-launcher delay: 30 seconds between profile syncs to respect rate limits.

## Project Structure

```
ITAD.LibrarySync/
├── src/
│   ├── ITAD.LibrarySync.Core/
│   └── ITAD.LibrarySync.App/
├── tests/
│   └── ITAD.LibrarySync.Core.Tests/
├── docs/superpowers/specs/
├── .github/workflows/release.yml
├── ITAD.LibrarySync.sln
├── README.md
└── LICENSE (MIT)
```

### Key NuGet packages

- `GameCollector.StoreHandlers.{EGS,Ubisoft,BattleNet,Xbox}`
- `Microsoft.Web.WebView2`
- `CommunityToolkit.Mvvm`
- `Hardcodet.NotifyIcon.Wpf`
- `Microsoft.Toolkit.Uwp.Notifications`

## Security

- ITAD OAuth tokens encrypted with Windows DPAPI (`%AppData%/ITADLibrarySync/tokens.dat`)
- Xbox OAuth tokens stored separately in DPAPI files (`xbox-login.dat`, `xbox-xsts.dat` under `%AppData%/ITADLibrarySync/`), distinct from ITAD tokens
- Public OAuth client (no embedded client secret)
- Launcher files read-only; data sent only to ITAD API
- No tokens or PII in logs

## Distribution

- **v1:** GitHub Releases — self-contained `win-x64` installer
- **v1.1:** winget package
- WebView2 Runtime required (preinstalled on Windows 11; installer checks on Windows 10)

## Testing

### Unit tests (Core)

- `GameMatcher` — ID matching, title fallback, normalization
- `WaitlistFilter` — owned exclusion, empty-list guards
- `SyncOrchestrator` — ordering, error paths (mocked API)
- `ProfileManager` — link idempotency

### Integration tests

- GameCollector readers with fixture JSON (no real launcher required)
- ITAD API with mock/recorded responses

### Manual checklist

- ITAD OAuth connect + token refresh
- Epic collection + waitlist sync with owned exclusion
- Global waitlist cleanup (cross-store)
- Ubisoft / Battle.net / Xbox collection sync
- Empty-list protection when launcher offline
- Auto scheduler + tray states + toasts
- First-run wizard

## ITAD OAuth App Registration

- Redirect URI: `http://127.0.0.1:{dynamic-port}/callback`
- Scopes: collection, waitlist, profile sync
- Client ID embedded in app binary
- Users authorize their own ITAD accounts
