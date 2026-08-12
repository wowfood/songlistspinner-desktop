# SonglistSpinner Desktop

SonglistSpinner Desktop is the Windows-only desktop edition of the StreamerSongList queue spinner. It is a .NET MAUI Blazor Hybrid application with shared queue and spinner models in a separate core library.

## Baseline

- Windows desktop only (`net8.0-windows10.0.19041.0`)
- Unpackaged and self-contained release configuration
- No Microsoft Store or MSIX signing requirement
- No fixed WebView2 runtime binaries checked into source control
- No backend, Azure Functions, database, or browser-client projects
- Clean Git history based on source commit `56d51a4`

The queue and play-history client now targets the documented StreamerSongList API v2 contract. Transport DTOs are isolated from the wheel models, and API credentials are stored separately from Twitch credentials. See [docs/API_V2.md](docs/API_V2.md) for configuration and current limitations.

## Prerequisites

- .NET 8 SDK
- .NET MAUI Windows workload
- Windows App SDK prerequisites supplied by the .NET workload

## Build and test

```powershell
dotnet restore SonglistSpinner.Desktop.sln
dotnet test SonglistSpinner.Desktop.sln
dotnet build SonglistSpinner.Desktop.sln -c Release
```

The Release configuration uses an unpackaged, self-contained `win-x64` target. A distributable publish layout can be produced with:

```powershell
dotnet publish src/SonglistSpinner.Desktop/SonglistSpinner.Desktop.csproj -c Release
```

## Development credentials

No secrets belong in this repository. Add a StreamerSongList streamer, user, or OAuth token on the Settings page; the token is kept in Windows secure storage.

For development and automation, these environment variables are also supported:

- `SONGLISTSPINNER_SSL_API_BASE_URL` — defaults to the staging v2 server documented by StreamerSongList
- `SONGLISTSPINNER_SSL_ACCESS_TOKEN` — used only when no securely stored token exists
- `SONGLISTSPINNER_SSL_TOKEN_TYPE` — `streamer` (default), `user`, or `bearer`
- `SONGLISTSPINNER_SSL_CLIENT_ID` — optional OAuth client ID sent with bearer tokens

The existing Twitch authorization implementation is transitional and reads its client secret from `SONGLISTSPINNER_TWITCH_CLIENT_SECRET`. A public-client OAuth flow that does not embed or require a distributed secret should replace it before release.
