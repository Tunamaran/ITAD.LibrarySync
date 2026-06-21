# ITAD End-to-End Verification Checklist

Use this after a sync to confirm the app is working against a live ITAD account.

## Prerequisites

- ITAD account connected in Settings → ITAD Connection
- At least one launcher enabled with a known game count (use **Test** + **Detay** first)
- App running from a recent build

## Steps

1. **Preview before sync**
   - Settings → Launchers → run **Test** on Epic (or another store)
   - Click **Detay** and note 2–3 game titles from the owned list

2. **Manual sync**
   - Click **Sync Now** in Settings (or tray)
   - Confirm the dialog lists the expected stores and cached counts
   - Wait for success notification / tray icon

3. **Verify on ITAD**
   - Open [isthereanydeal.com](https://isthereanydeal.com/) → your profile
   - Open **Collection** custom profile synced by this app
   - Search for the same game titles from step 1
   - Repeat for **Waitlist** if that launcher exposes wishlist data

4. **Last Sync column**
   - Reopen Settings → Launchers
   - **Last Sync** should show collection/waitlist totals for synced stores
   - Sync from tray, reopen Settings — stats should still match

5. **Enabled launcher filter**
   - Disable one launcher in Settings
   - Tray **Sync Now** → confirm dialog should omit disabled store
   - Only enabled stores should update on ITAD

6. **Start with Windows** (optional, use a published release build)
   - Publish or install the release `.exe` first — do not rely on `dotnet run` for this test
   - Enable **Start with Windows** in Settings → General
   - Sign out / restart Windows
   - App should appear in tray after login (only one tray icon)
   - Verify registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` contains `ITAD Library Sync` pointing to the `.exe`
   - Disable the setting and confirm the Run key entry is removed

7. **Single instance**
   - With the app running, start it again (shortcut, Run key, or `dotnet run`)
   - No second tray icon should appear; Settings should open in the existing instance

8. **Exit during sync** (optional)
   - Start a manual sync, then tray → **Exit**
   - Confirm the warning dialog appears; choosing **No** keeps the app running

## Expected Xbox behavior

- Owned count reflects Store license filter (~9 games in test environment), not full play history
- **Detay** list should match what syncs to ITAD Collection

## Log on failure

- Tray → **View Last Sync Log**
- Check `%AppData%\ITADLibrarySync\logs\` for API or auth errors
