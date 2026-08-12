# SonglistSpinner Desktop

SonglistSpinner Desktop is the Windows-only desktop edition of the StreamerSongList queue spinner. It is a .NET MAUI Blazor Hybrid application with shared queue and spinner models in a separate core library.

## Baseline

- Windows desktop only (`net8.0-windows10.0.19041.0`)
- Unpackaged and self-contained release configuration
- No Microsoft Store or MSIX signing requirement
- No fixed WebView2 runtime binaries checked into source control
- No backend, Azure Functions, database, or browser-client projects
- Clean Git history based on source commit `56d51a4`

The current StreamerSongList client still targets the legacy v1 endpoint. Updating it for the new API is the next migration phase; see [docs/BASELINE.md](docs/BASELINE.md).

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

No secrets belong in this repository. The existing Twitch authorization implementation is transitional and reads its client secret from `SONGLISTSPINNER_TWITCH_CLIENT_SECRET`. A public-client OAuth flow that does not embed or require a distributed secret should replace it before release.

