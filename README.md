# SonglistSpinner Desktop

[![Windows CI](https://github.com/wowfood/songlistspinner-desktop/actions/workflows/ci.yml/badge.svg)](https://github.com/wowfood/songlistspinner-desktop/actions/workflows/ci.yml)

SonglistSpinner Desktop is a Windows application for randomly selecting a song
from a StreamerSongList queue. It provides an interactive dashboard for the
streamer and a separate browser-source overlay for OBS or Streamlabs Desktop.

The application connects directly to StreamerSongList API v2 with a streamer
access token. Queue changes, play history, and Now Playing updates no longer
need Twitch chat commands or a separate chatbot connection.

> A StreamerSongList account, a configured song queue, and a streamer access
> token are required. The application connects to the production
> StreamerSongList API v2 and realtime event service.

## Features

- Random wheel selection from the current StreamerSongList queue
- Explicit **Mark Played**, **Leave in Queue**, and optional **Set Now Playing**
  winner actions
- Played-song history with configurable fields and time range
- Optional Now Playing panel with configurable content, position, width, and
  typography
- Local OBS/Streamlabs browser-source overlay synchronized with the desktop app
- Realtime queue and history updates with automatic reconnection
- Resizable played-history panel and wheel visibility controls
- Live 16:9 overlay preview while editing settings
- Streamer access tokens stored in Windows secure storage
- Persistent API, realtime, and overlay health indicators
- Update notifications linked to published GitHub Releases
- One portable, self-contained Windows executable with no installer, MSIX, or
  Microsoft Store dependency

## Requirements

- A 64-bit PC running Windows 10 version 1809 or newer, or Windows 11
- A [StreamerSongList](https://streamersonglist.com/) account and channel
- A StreamerSongList streamer access token created under **Settings > Access**
- An internet connection for StreamerSongList API and realtime event access;
  update checks also contact GitHub Releases
- OBS Studio or Streamlabs Desktop if the overlay will be shown on stream
- Microsoft Edge WebView2 Evergreen Runtime

WebView2 is already present on Windows 11 and most supported Windows 10
installations. If the application opens without displaying its interface,
install the [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).

The desktop app and OBS/Streamlabs must run on the same computer because the
overlay is served through `localhost`.

## Download and run

1. Open the [SonglistSpinner Desktop Releases](https://github.com/wowfood/songlistspinner-desktop/releases) page.
2. Open the latest release and download `SonglistSpinner.Desktop.exe`.
3. Optionally download `SonglistSpinner.Desktop.exe.sha256` to verify the file.
4. Place the executable in a normal user-writable folder, such as
   `Documents\SonglistSpinner`.
5. Double-click `SonglistSpinner.Desktop.exe`.

There is no installation process. The executable already contains .NET, MAUI,
the Windows App SDK runtime, and the application assets. Keep the executable in
the same location so it is easy to replace when an update is published.

The executable is currently unsigned, so Windows SmartScreen may display a
reputation warning. Confirm that the file came from this repository's Releases
page before choosing to run it.

### Optional checksum verification

Place the executable and checksum file in the same directory, open PowerShell
in that directory, and run:

```powershell
$expected = (Get-Content .\SonglistSpinner.Desktop.exe.sha256).Split()[0]
$actual = (Get-FileHash .\SonglistSpinner.Desktop.exe -Algorithm SHA256).Hash.ToLowerInvariant()
$actual -eq $expected
```

PowerShell returns `True` when the downloaded executable matches the release
checksum.

## First-run connection setup

The connection wizard opens automatically when the app cannot find a saved API
credential. It can also be reopened later from **Settings > Connection**.

### 1. Create a streamer access token

1. Sign in to StreamerSongList.
2. Open **Settings > Access**.
3. Create a streamer access token for the channel this installation will
   control.
4. Copy the token and keep it private.

Streamer tokens are recommended for a personal, single-channel installation.
See the [StreamerSongList authentication documentation](https://dev.streamersonglist.com/docs/authentication#streamer-access-tokens)
for the available credential types.

### 2. Enter the credential

1. Paste the token into **Streamer access token**.
2. Leave **Credential type** set to **Streamer token**.
3. Select **Continue**.

The token is stored using Windows secure storage. It is not saved beside the
executable or written to this repository.

The advanced User token and OAuth bearer options are available for development
or future multi-channel authentication scenarios. OAuth bearer credentials also
require their client ID.

### 3. Choose the channel

Paste the public StreamerSongList URL you normally share, or enter the streamer
name directly. Supported URL routes include:

| Route | Identity |
| --- | --- |
| `/t/name` | Twitch |
| `/y/name` | YouTube |
| `/k/name` | Kick |
| `/s/name` | StreamerSongList |

When entering only a name, choose the matching identity under **Identity
platform**. This option tells StreamerSongList which linked public identity to
use for channel lookup; it does not otherwise change spinner behavior.

For a streamer active on several platforms, select any linked identity that
resolves to the intended StreamerSongList channel. This installation controls
one resolved StreamerSongList channel rather than maintaining separate queues
for Twitch, YouTube, Kick, or other simulcast destinations.

### 4. Verify the services

Select **Connect and verify**. The wizard checks:

- API authentication and channel resolution
- Queue and play-history access
- Realtime StreamerSongList events
- The local OBS overlay server

If the API succeeds but an optional realtime or overlay check fails, the wizard
allows you to continue with warnings. The Dashboard health bar will continue to
show the affected service.

On the final page, copy the overlay URL or open its preview, then select **Open
dashboard**.

## OBS Studio setup

The app serves the overlay at:

```text
http://localhost:5150/overlay
```

You can copy this URL from the setup wizard, the navigation bar, or **Settings >
Advanced**.

1. Launch SonglistSpinner and leave it running.
2. Open the Dashboard and confirm that the intended channel has loaded.
3. In OBS, open the scene that should contain the spinner.
4. Under **Sources**, select **+**, then **Browser**.
5. Create a new browser source.
6. Leave **Local file** disabled and enter
   `http://localhost:5150/overlay` in the URL field.
7. Set the browser source width and height to the stream canvas resolution. For
   a 1080p canvas, use `1920` by `1080`.
8. Remove generated Custom CSS if it overrides the application background or
   layout.
9. Select **OK** and position the source in the scene.

The browser source is display-only. Perform spins and winner actions in the
desktop Dashboard. The overlay mirrors wheel movement, winner presentation,
history, Now Playing, panel width, and visibility changes automatically.

The Dashboard's **Overlay** health indicator shows **Ready** when the local
server is available and reports how many OBS/browser sources are connected.

## Streamlabs Desktop setup

1. Launch SonglistSpinner and load the channel on the Dashboard.
2. Open the required Streamlabs scene.
3. Select **+** under Sources, then add a **Browser Source**.
4. Use URL mode rather than a local file.
5. Enter `http://localhost:5150/overlay`.
6. Set the width and height to the stream canvas resolution.
7. Remove generated CSS if it overrides the configured appearance.
8. Save the source.

## Using the spinner

1. Open the Dashboard and wait for the queue to load.
2. Confirm that **API** is healthy and that the queued-song count is correct.
3. Select **SPIN**.
4. Wait for the wheel and winner presentation to finish.
5. Choose what should happen to the selected queue entry:

   - **Mark Played** moves it directly into StreamerSongList play history.
   - **Leave in Queue** closes the winner display without changing the queue.
   - **Set Now Playing** appears when the Now Playing workflow is enabled. It
     explicitly marks the previous Now Playing song as played before promoting
     the winner.

The wheel and played list refresh after the winner action. Realtime events also
refresh the app when the queue or history changes on StreamerSongList. Use the
Dashboard **Refresh** button as a manual fallback.

Additional Dashboard controls:

- Drag the divider beside the played list to resize it; the overlay follows the
  new width.
- Use the played-list collapse control in the Dashboard to show or hide that
  panel in both views.
- Use **Toggle Wheel** to show or hide the wheel in both views.
- Use **Change** to select another channel when the default-channel lock is not
  enabled.

Keep the app open on the Dashboard during a stream. Closing the app stops the
local overlay, and navigating away from the Dashboard pauses channel event
handling until the Dashboard is opened again.

## Configuration

Open **Settings** from the navigation bar. Changes are shown immediately in the
sample 16:9 preview, but the live OBS source is not changed until settings are
saved and the Dashboard loads them. Attempting to leave with unsaved changes
offers **Save and leave**, **Abandon changes**, or **Keep editing**.

### Connection

| Setting | Effect |
| --- | --- |
| Default StreamerSongList Name | Loads this channel automatically when the Dashboard opens. |
| Platform identity | Selects the Twitch, YouTube, Kick, or native StreamerSongList identity used to resolve the channel. |
| Hide “Change Streamer” | Prevents accidental channel changes when a default is configured. |
| Credential Type | Uses Streamer, User, or OAuth Bearer authorization. Streamer is recommended. |
| API Token | Replaces the credential stored in Windows secure storage. Leaving it blank keeps the existing token. |
| Save and test connection | Saves the connection settings, then verifies queue and history access. |

Use **Clear** followed by **Save Settings** to remove the stored credential. The
connection wizard will open the next time the Dashboard needs a credential.

### Spinner & Queue

| Setting | Effect |
| --- | --- |
| Prefer Mark Played | Highlights **Mark Played** as the suggested winner action. It never performs the action automatically. |
| Enable Now Playing workflow | Adds **Set Now Playing** to the winner popup and enables the Now Playing overlay panel. |
| Exclude already-played songs | Removes queue entries matching the selected history range from the wheel. |
| Play History Period | Supplies the played panel and the optional wheel-exclusion filter. |

History choices are Recent, Last 24 hours, Last 7 days, Last month, and All
time. With the current API v2 contract, **Recent** means the latest API page,
not a distinct streaming session. The current client loads at most the first
100 history entries.

### Overlay Layout

The played panel and Now Playing panel can display any ordered combination of:

- Artist
- Song title
- Requester
- Donation

Select a field chip to include or exclude it. Drag selected chips into the order
they should appear.

Played-panel options include:

- Left or right screen position
- Font family and CSS font size
- One to five lines per song

When the Now Playing workflow is enabled, its panel additionally supports:

- Six screen positions: top or bottom, aligned left, center, or right
- CSS width values such as `28rem`, `480px`, or `40vw`
- Independent font family and CSS font size

### Appearance

- **Background mode** can be a solid color or transparent. Transparent is the
  usual choice when placing the spinner over other scene content.
- **Wheel Colors** accepts one CSS color per line and repeats the palette when
  the queue contains more songs than colors.
- Color pickers configure text, wheel pointer, button background, button text,
  played-panel background and opacity, and played-song backgrounds.

### Advanced

- **Enable diagnostic output** writes a rolling support log under
  `%LOCALAPPDATA%\SonglistSpinner\logs`. Streamer access tokens are not logged.
- **Service Endpoints** shows the application version, active API address, and
  local overlay URL.
- **Open GitHub Releases** provides a permanent route to updates even after an
  update notification has been dismissed.

## Service health indicators

The Dashboard reports five pieces of runtime context:

- **API** — channel, queue, and history requests
- **Realtime** — StreamerSongList event connection and reconnection state
- **Overlay** — local server state and connected browser-source count
- **Channel** — the currently resolved channel name
- **Environment** — staging, production, or a custom API address

Hover over a health item for its most recent detail or error message.

## Updates

At startup, SonglistSpinner checks the latest published GitHub Release. If its
semantic version is newer than the running executable, a notification links to
that release's notes and downloads.

Dismissal applies only to that release version. A later release will show a new
notification. The application does not install updates automatically:

1. Open the linked release.
2. Download the new `SonglistSpinner.Desktop.exe`.
3. Close the running app.
4. Replace the previous executable with the new file.
5. Launch it normally. Existing local settings and secure credentials remain on
   that Windows account.

## Troubleshooting

### The queue does not load

- Confirm the StreamerSongList queue contains upcoming songs.
- Open **Settings > Connection** and select **Save and test connection**.
- Verify that the channel name and platform identity match the public
  StreamerSongList route.
- Check the Dashboard API health detail for the returned status and message.

### The token is rejected or has expired

Create a new streamer access token under StreamerSongList **Settings > Access**,
then replace it under **Settings > Connection** and select **Save and test
connection**. The application does not automatically renew manually created
streamer tokens.

### Realtime shows an error

The initial queue can still be loaded through the API, and **Refresh** remains
available. Check that the computer can connect to the configured WebSocket
endpoint. The events website root is a protected Centrifugo administration
page; the app connects anonymously to its `/connection/websocket` endpoint and
does not need that administrator login.

### The OBS or Streamlabs source is blank

- Keep SonglistSpinner running on the same computer as the streaming software.
- Use URL mode, not **Local file**.
- Confirm the source URL is exactly `http://localhost:5150/overlay`.
- Confirm port `5150` is not occupied by another application or another running
  SonglistSpinner instance. The overlay port is currently fixed and cannot be
  changed in Settings.
- Open the Dashboard and confirm the Overlay indicator reports **Ready** or a
  connected source.
- Refresh the browser source after restarting SonglistSpinner.

### Settings appear in preview but not in OBS

The settings preview intentionally uses an unsaved draft that is isolated from
the live overlay. Select **Save Settings** to push the saved configuration to
connected OBS or Streamlabs browser sources immediately.

### No update notification appears

Only a newer, published, non-prerelease GitHub Release produces a notification.
The same version will not notify, and a dismissed release stays dismissed. Use
the permanent **Releases** navigation link to check manually.

## Development

Source builds require:

- .NET 10 SDK, selected by `global.json`
- .NET MAUI Windows workload
- Windows development prerequisites for MAUI

From the repository root:

```powershell
dotnet restore SonglistSpinner.Desktop.sln
dotnet test SonglistSpinner.Desktop.sln
dotnet build SonglistSpinner.Desktop.sln -c Release
```

Run the desktop project during development with:

```powershell
dotnet run --project .\src\SonglistSpinner.Desktop\SonglistSpinner.Desktop.csproj
```

Create the verified single executable with:

```powershell
.\scripts\publish-single-file.ps1
```

The output is written to:

```text
artifacts\win-x64\SonglistSpinner.Desktop.exe
```

Development and automation can override connection values with:

| Environment variable | Purpose |
| --- | --- |
| `SONGLISTSPINNER_SSL_API_BASE_URL` | StreamerSongList API base address |
| `SONGLISTSPINNER_SSL_EVENTS_URL` | StreamerSongList events WebSocket address |
| `SONGLISTSPINNER_SSL_ACCESS_TOKEN` | Credential fallback when Windows secure storage is empty |
| `SONGLISTSPINNER_SSL_TOKEN_TYPE` | `streamer`, `user`, or `bearer` |
| `SONGLISTSPINNER_SSL_CLIENT_ID` | Client ID used with an OAuth bearer token |

Do not commit tokens, local environment files, or screenshots containing
credentials.

## Third-party software

The wheel renderer is `spin-wheel` 5.0.2, bundled into the executable so the UI
does not depend on a CDN at runtime. It is distributed under the MIT License;
the bundled license text is stored beside the script in
`src/SonglistSpinner.Desktop/wwwroot/spinner`.

## Additional documentation

- [API v2 integration](docs/API_V2.md)
- [Single-file Windows distribution](docs/SINGLE_FILE_DISTRIBUTION.md)
- [Release workflow and QA](docs/RELEASING.md)
- [Desktop migration baseline](docs/BASELINE.md)
