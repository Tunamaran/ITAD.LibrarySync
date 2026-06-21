# ITAD Library Sync

Windows WPF tray application that syncs your game libraries from Epic Games, Ubisoft Connect, Battle.net, and Microsoft/Xbox to [IsThereAnyDeal](https://isthereanydeal.com/) Collection and Waitlist via the official Custom Profiles API.

## Features

- Read owned games from local launcher cache and store APIs (no store passwords stored)
- Push libraries to ITAD Collection per store profile
- Sync waitlist where local data is available; remove owned games from ITAD Waitlist
- Manual sync with optional confirmation, scheduled sync, and sync on startup
- Library preview before sync — inspect owned/wishlist game lists per store
- Tray tooltip shows last sync time and per-store summary

## Prerequisites

- **Windows 10 or later** (64-bit)
- **[Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)** — required for ITAD OAuth sign-in (preinstalled on Windows 11; install manually on Windows 10 if needed)
- **.NET 8 runtime** — only required when running a framework-dependent build; [GitHub Releases](https://github.com/Tunamaran/ITAD.LibrarySync/releases) are self-contained and do not need a separate runtime install
- **Game launchers installed and signed in** — Epic Games, Ubisoft Connect, Battle.net, and/or Xbox/Microsoft Store as needed for the stores you want to sync
- **Xbox / Microsoft Store** — connect your Xbox account in Settings for library sync beyond locally installed games (see [Xbox account](#xbox-account-microsoft-store) below)

## Download

Download the latest release from the repository’s **GitHub Releases** page. Releases are built as self-contained, single-file `win-x64` binaries tagged with `v*`.

## ITAD OAuth (contributors)

End users connect through the app’s built-in OAuth flow. **Contributors building from source** must register their own ITAD OAuth application and embed the client ID:

1. Register an app at [IsThereAnyDeal — My Apps](https://isthereanydeal.com/my/apps/).
2. Set the redirect URI to `http://127.0.0.1:8765/callback` (must match `appsettings.json`).
3. Request scopes: `profiles`, `wait_read`, `wait_write`, `coll_read`, `coll_write`.
4. Replace `YOUR_ITAD_CLIENT_ID` in `src/ITAD.LibrarySync.App/appsettings.json`:

```json
{
  "Itad": {
    "ClientId": "your-client-id-here",
    "RedirectUri": "http://127.0.0.1:8765/callback"
  }
}
```

The app is a public OAuth client (PKCE, no client secret). OAuth tokens are encrypted with Windows DPAPI under `%AppData%\ITADLibrarySync\`.

## Build

```powershell
dotnet build
```

Publish a self-contained release binary locally:

```powershell
dotnet publish src/ITAD.LibrarySync.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Run from source:

```powershell
dotnet run --project src/ITAD.LibrarySync.App
```

## Development

### Xbox debugging (XboxProbe)

`tools/XboxProbe` is a small console utility for verifying Xbox OAuth tokens and TitleHub responses without running the full WPF app.

```powershell
dotnet run --project tools/XboxProbe
```

If you have already connected Xbox in ITAD Library Sync, XboxProbe reuses the saved tokens from `%AppData%\ITADLibrarySync\`. Otherwise it prints the Microsoft authorize URL and accepts an authorization code from stdin.

### Manual ITAD verification

See [`docs/manual-itad-verification-checklist.md`](docs/manual-itad-verification-checklist.md) for a step-by-step checklist to verify sync against a live ITAD account.

## Usage

### System tray

The app runs in the system tray only (no main window). Right-click the tray icon for:

| Menu item | Action |
|-----------|--------|
| **Sync Now** | Sync all **enabled** launchers |
| **Sync Epic / Ubisoft / …** | Sync a single enabled store (disabled stores are hidden) |
| **Settings…** | Open the settings window |
| **View Last Sync Log** | Open the most recent log in `%AppData%\ITADLibrarySync\logs\` |
| **Connect to ITAD** / **Disconnect from ITAD** | Start or clear OAuth |
| **Exit** | Quit the application |

Tray icon tooltips reflect sync state (idle, syncing, success, partial, error) and, after a sync, show the last sync time plus a short per-store summary (for example `Epic: +2/-1 | Ubisoft: ok`).

Only one instance runs at a time. Launching the app again while it is already running opens Settings in the existing instance instead of creating a second tray icon.

Double-click the tray icon to open Settings.

On first run, complete the setup wizard: connect ITAD, review launcher detection, and optionally run an initial sync.

### Settings

- **ITAD Connection** — Connect or disconnect your ITAD account; shows your ITAD username after sign-in
- **Launchers** — Enable/disable stores, view detection status, last sync stats, **Test** read counts, **Detay** (detail) preview of the full game list before syncing
- **Sync Settings** — Auto-sync interval (6 h / 12 h / 24 h / weekly, or disabled), sync on startup, confirm before manual sync
- **General** — Start with Windows, toast notifications, log level

**Start with Windows** registers the published `.exe` path in the current-user Run key. When developing with `dotnet run`, enable this only for testing in a release/published build so Windows starts the real app binary, not the `dotnet` host.

#### Library preview (Detay)

1. Open **Settings → Launchers**
2. Click **Test** on a store to read the library
3. Click **Detay** to open a searchable owned/wishlist game list
4. Review titles before running **Sync Now**

If you click **Detay** without a prior test read, the app reads the library first.

#### Xbox account (Microsoft Store)

Microsoft/Xbox sync uses multiple sources:

1. **Local installs** — games installed via the Microsoft Store / Xbox app on this PC (via GameCollector)
2. **Xbox Live title history** — account-level play history from Xbox OAuth
3. **Microsoft Store license check** — filters title-history candidates to titles with a confirmed Store entitlement on this PC

To connect Xbox:

1. Open **Settings** from the tray menu
2. Go to the **Launchers** tab
3. Click **Connect Xbox** and sign in with your Microsoft account in the WebView window
4. After success, the panel shows your gamertag
5. Use **Test** and **Detay** to verify the game list before syncing

To disconnect, click **Disconnect Xbox**. Xbox tokens are stored separately from ITAD OAuth tokens (DPAPI-encrypted under `%AppData%\ITADLibrarySync\`).

**Important:** The synced Microsoft library reflects Store license verification combined with title history — not every played title and not every purchase you have never launched. Use **Detay** to see exactly what will sync.

### Sync behavior

For each **enabled** launcher the app:

1. **Reads** owned games (and wishlist where available)
2. **Syncs Collection** — `PUT /profiles/sync/collection/v1` to the store’s ITAD custom profile (Epic, Ubisoft, Battle.net, Xbox)
3. **Syncs Waitlist** — pushes local wishlist to the profile waitlist when data is available; owned games are filtered out before upload
4. **Global waitlist cleanup** — after all profiles, removes any ITAD waitlist entry that matches a game you own (including cross-store matches)

**Safety guards:** if a launcher returns zero owned games, collection sync is skipped (prevents wiping ITAD). If wishlist data is unreadable or empty, waitlist sync is skipped for that profile.

Manual sync from the tray or Settings can show a confirmation dialog listing the stores (and cached game counts when available). Disable this via **Settings → Sync Settings → Confirm before manual sync**. Automatic and scheduled sync never prompts.

Sync runs are spaced ~30 seconds apart between stores to respect ITAD rate limits.

## Limitations

- **Microsoft / Xbox library** — requires Xbox OAuth in Settings. Synced titles are filtered by Microsoft Store license checks on your PC; title history alone is not used as the final owned list. See [Xbox account](#xbox-account-microsoft-store) above.
- **Waitlist import** is **Epic-only, best-effort** — read from Epic’s local cache when available. Ubisoft Connect, Battle.net, and Xbox do not expose local wishlists; waitlist sync is skipped for those stores, but global waitlist cleanup still runs.
- **Battle.net** — local cache may omit uninstalled owned titles.
- **GameCollector 4.4.0.1** — launcher reading depends on [GameCollector](https://www.nuget.org/packages/GameCollector.StoreHandlers.EGS) store handlers; behavior may change if launcher cache formats change.
- **ITAD matching** — games are matched by store ID with title fallback; unmatched titles are logged but may not appear on ITAD.
- **Windows only** — reads local launcher data paths; not supported on macOS or Linux.

## License

MIT — see [LICENSE](LICENSE).
