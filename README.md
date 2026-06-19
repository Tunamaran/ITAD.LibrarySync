# ITAD Library Sync

Windows WPF tray application that syncs your game libraries from Epic Games, Ubisoft Connect, Battle.net, and Microsoft/Xbox to [IsThereAnyDeal](https://isthereanydeal.com/) Collection and Waitlist via the official Custom Profiles API.

## Features

- Read owned games from local launcher cache (no store credentials stored)
- Push libraries to ITAD Collection per store profile
- Sync waitlist where local data is available; remove owned games from ITAD Waitlist
- Manual sync with optional scheduled sync from the system tray

## Prerequisites

- **Windows 10 or later** (64-bit)
- **[Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)** — required for ITAD OAuth sign-in (preinstalled on Windows 11; install manually on Windows 10 if needed)
- **.NET 8 runtime** — only required when running a framework-dependent build; [GitHub Releases](https://github.com/OWNER/REPO/releases) are self-contained and do not need a separate runtime install
- **Game launchers installed and signed in** — Epic Games, Ubisoft Connect, Battle.net, and/or Xbox/Microsoft Store as needed for the stores you want to sync

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

## Usage

### System tray

The app runs in the system tray only (no main window). Right-click the tray icon for:

| Menu item | Action |
|-----------|--------|
| **Sync Now** | Sync all enabled launchers |
| **Sync Epic / Ubisoft / Battle.net / Microsoft** | Sync a single store |
| **Settings…** | Open the settings window |
| **View Last Sync Log** | Open the most recent log in `%AppData%\ITADLibrarySync\logs\` |
| **Connect to ITAD** / **Disconnect from ITAD** | Start or clear OAuth |
| **Exit** | Quit the application |

Tray icon tooltips reflect sync state: idle, syncing, success, partial (some launchers failed), or error.

On first run, complete the setup wizard: connect ITAD, review launcher detection, and optionally run an initial sync.

### Settings

- **ITAD Connection** — Connect or disconnect your ITAD account
- **Launchers** — Enable/disable stores, view detection status, test-read game counts
- **Sync Settings** — Auto-sync interval (6 h / 12 h / 24 h / weekly, or disabled), sync on startup
- **General** — Start with Windows, toast notifications, log level

### Sync behavior

For each enabled launcher the app:

1. **Reads** owned games (and wishlist where available) from local launcher cache
2. **Syncs Collection** — `PUT /profiles/sync/collection/v1` to the store’s ITAD custom profile (Epic, Ubisoft, Battle.net, Xbox)
3. **Syncs Waitlist** — pushes local wishlist to the profile waitlist when data is available; owned games are filtered out before upload
4. **Global waitlist cleanup** — after all profiles, removes any ITAD waitlist entry that matches a game you own (including cross-store matches)

**Safety guards:** if a launcher returns zero owned games, collection sync is skipped (prevents wiping ITAD). If wishlist data is unreadable or empty, waitlist sync is skipped for that profile.

Sync runs are spaced ~30 seconds apart between stores to respect ITAD rate limits.

## Limitations

- **Waitlist import** is **Epic-only, best-effort** — read from Epic’s local cache when available. Ubisoft Connect, Battle.net, and Xbox do not expose local wishlists; waitlist sync is skipped for those stores, but global waitlist cleanup still runs.
- **GameCollector 4.4.0.1** — launcher reading depends on [GameCollector](https://www.nuget.org/packages/GameCollector.StoreHandlers.EGS) store handlers; behavior may change if launcher cache formats change.
- **ITAD matching** — games are matched by store ID with title fallback; unmatched titles are logged but may not appear on ITAD.
- **Windows only** — reads local launcher data paths; not supported on macOS or Linux.

## License

MIT — see [LICENSE](LICENSE).
