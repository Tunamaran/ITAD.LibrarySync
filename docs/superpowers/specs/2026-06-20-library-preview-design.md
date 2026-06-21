# Library Preview Before Sync — Design Spec

**Date:** 2026-06-20  
**Status:** Approved (detail window approach)

## Problem

After "Test Read", users only see a summary string (e.g. "284 games (280 owned, 4 wishlist)"). The full `LauncherReadResult` with per-game titles is discarded. Users cannot verify which games will sync to ITAD before pressing Sync Now.

## Goal

Allow users to inspect the full owned/wishlist game list per launcher before syncing, with search/filter in a dedicated window.

## Approach (chosen)

**Detail window** — Launchers tab gets a "Detay" button per row. Opens `LibraryPreviewWindow` showing owned and wishlist tabs with search.

Alternatives rejected:
- Inline expanding panel — cramped in settings grid
- Sync wizard confirmation — adds friction to every sync; preview should be on-demand

## Data Flow

1. `TestReadAsync` or `ViewDetailsAsync` calls `ILauncherReader.ReadAsync()`
2. `ApplyReadResult` stores summary strings **and** caches `LauncherReadResult` on `LauncherSettingsItem.LastReadCache`
3. "Detay" opens preview from cache; if empty, triggers read first then opens

Sync continues to re-read at sync time (no sync-from-cache in v1 — avoids stale data risk).

## UI

### Launchers tab
- New column "Detay" with button (enabled when cache exists or triggers read)
- Existing "Test" unchanged

### LibraryPreviewWindow
- Title: `{LauncherDisplayName} — Library Preview`
- Header: summary line + warning/error if present
- Search box filters both tabs
- TabControl: **Owned** | **Wishlist**
- DataGrid columns: Title, Store ID
- Close button

## Components

| File | Role |
|------|------|
| `LauncherSettingsItem.cs` | `LastReadCache` property |
| `SettingsViewModel.cs` | Cache in `ApplyReadResult`, `ViewDetailsCommand` |
| `FirstRunWizardViewModel.cs` | Cache on wizard scan |
| `LibraryPreviewViewModel.cs` | Filter + game collections |
| `LibraryPreviewWindow.xaml` | Preview UI |

## Error Handling

- Read failure: no cache, Detay shows error via Test Read path
- Empty library: window opens with empty grids and summary
- Xbox auth required: same prompt as Test Read

## Out of Scope (v1)

- Sync-from-cache (avoid double-read)
- Export to CSV
- Per-game ITAD match preview
- Tray menu preview entry

## Testing

- Manual: Test Read Epic → Detay → verify game count matches summary
- Manual: Search filter narrows list
- Unit: optional filter logic test if extracted
