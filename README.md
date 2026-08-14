# SonglistSpinner Desktop

[![Windows CI](https://github.com/wowfood/songlistspinner-desktop/actions/workflows/ci.yml/badge.svg)](https://github.com/wowfood/songlistspinner-desktop/actions/workflows/ci.yml)

SonglistSpinner Desktop is the Windows-only desktop edition of the StreamerSongList queue spinner. It is a .NET MAUI Blazor Hybrid application with shared queue and spinner models in a separate core library. Queue and play-history changes arrive through StreamerSongList realtime events; REST remains the source of truth for initial and reconciled snapshots.

## Baseline

- Windows desktop only (`net10.0-windows10.0.19041.0`)
- One unpackaged, self-contained `win-x64` release executable
- No Microsoft Store or MSIX signing requirement
- No fixed WebView2 runtime binaries checked into source control
- No backend, Azure Functions, database, or browser-client projects
- Clean Git history based on source commit `56d51a4`

The queue and play-history client targets the documented StreamerSongList API v2 contract. A streamer access token lets the desktop app read and update its channel directly, without Twitch login or chatbot commands. See [docs/API_V2.md](docs/API_V2.md) for configuration and current limitations.

The optional **Display Now Playing** setting promotes each winner into StreamerSongList's now-playing slot, explicitly completes the previous song, and adds a configurable Now Playing card to the OBS overlay. The card supports ordered song fields, font family and size, panel width, and six screen positions.

## Prerequisites

- .NET 10 SDK (selected by `global.json`)
- .NET MAUI Windows workload
- Windows App SDK prerequisites supplied by the .NET workload

## Build and test

```powershell
dotnet restore SonglistSpinner.Desktop.sln
dotnet test SonglistSpinner.Desktop.sln
dotnet build SonglistSpinner.Desktop.sln -c Release
```

The Release configuration produces one unpackaged, self-contained `win-x64`
executable. Build the verified distribution artifact with:

```powershell
.\scripts\publish-single-file.ps1
```

The executable is written to
`artifacts\win-x64\SonglistSpinner.Desktop.exe`. End users do not need .NET,
MAUI, the Windows App SDK, MSIX, or an installer. WebView2 Evergreen remains the
only external runtime requirement. See
[docs/SINGLE_FILE_DISTRIBUTION.md](docs/SINGLE_FILE_DISTRIBUTION.md) for details.
Windows QA also launches the built executable, verifies its local OBS overlay,
and produces a SHA-256 checksum. Versioned tag builds create reviewable draft
GitHub releases; see
[docs/RELEASING.md](docs/RELEASING.md).

## Development credentials

No secrets belong in this repository. For a personal single-channel installation, create a StreamerSongList streamer access token under Settings > Access and add it on the app's Settings page. The token is kept in Windows secure storage.

For development and automation, these environment variables are also supported:

- `SONGLISTSPINNER_SSL_API_BASE_URL` — defaults to the staging v2 server documented by StreamerSongList
- `SONGLISTSPINNER_SSL_EVENTS_URL` — defaults to the anonymous staging Centrifugo WebSocket endpoint
- `SONGLISTSPINNER_SSL_ACCESS_TOKEN` — used only when no securely stored token exists
- `SONGLISTSPINNER_SSL_TOKEN_TYPE` — `streamer` (default), `user`, or `bearer`
- `SONGLISTSPINNER_SSL_CLIENT_ID` — optional OAuth client ID sent with bearer tokens
