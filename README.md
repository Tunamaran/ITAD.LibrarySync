# ITAD Library Sync

Windows WPF tray application that syncs your game libraries from Epic Games, Ubisoft Connect, Battle.net, and Microsoft/Xbox to [IsThereAnyDeal](https://isthereanydeal.com/) Collection and Waitlist via the official Custom Profiles API.

## Features (planned)

- Read owned games from local launcher cache (no store credentials stored)
- Push libraries to ITAD Collection per store profile
- Sync waitlist where local data is available; remove owned games from ITAD Waitlist
- Manual sync with optional scheduled sync from the system tray

## Requirements

- Windows 10/11
- .NET 8 SDK
- Launchers installed and signed in (Epic, Ubisoft Connect, Battle.net, Xbox)

## Build

```powershell
dotnet build
```

## License

MIT — see [LICENSE](LICENSE).
