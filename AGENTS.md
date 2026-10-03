# AGENTS.md

Songlist Spinner is a Windows desktop app (.NET 10 MAUI Blazor Hybrid) that loads a
StreamerSongList queue, spins a wheel to pick the next song, and serves an OBS browser-source
overlay from a local HTTP server. User-facing documentation is in `README.md`.

## Project map

| Path | What lives there |
| --- | --- |
| `src/SonglistSpinner.Core` | MAUI-free domain, one folder (and namespace) per feature: `StreamerSongList` (client contract, channel, ids, queue and history models; the API v2 and Centrifugo implementation in `StreamerSongList/Api/V2`), `Settings` (persisted `SettingsDto`, its normaliser and converter, `SpinnerConfig` and defaults), `Songs` (song fields and display text), `PlayedSongs` (played-song list, played-song exclusion), `Winner` (winner dialog content, Now Playing promotion), `Updates` (GitHub release check) |
| `src/SonglistSpinner.Application` | MAUI-free app services: `ChannelLoader` (resolve a channel and start the session), `StreamerSessionService` (queue session, realtime refresh), `FetchQueueAndHistoryAsync` (the one queue-plus-history read), `WheelSpinService` and `WinnerActionService` (a spin and the winner's outcome), `ApiCredentialTest` (test a credential, restore the previous one on failure), `OverlayStateService` (overlay state and SSE), `LocalOverlayServer` (the OBS overlay's localhost HTTP server; the Desktop `wwwroot` overlay files are embedded in this assembly for it), `EnvironmentOverrides` (the `SONGLISTSPINNER_SSL_*` startup variables), `PreferencesSettingsService` and `SecureStorageStreamerSongListCredentialStore` (saved settings and API credential, over `IKeyValueStore` and `ISecretStore`), `SettingsDraftTracker` and `SettingsViewModel` (the Settings page draft), `ApplicationUpdateService` (the newer release to offer, minus the one the user dismissed) |
| `src/SonglistSpinner.Desktop` | MAUI host: Razor pages (`Components/Pages`), MAUI-backed services (`Services`), `MauiProgram.cs` composing the feature registrations |
| `src/SonglistSpinner.Desktop/wwwroot` | Wheel and overlay JavaScript, CSS, `overlay/Overlay.html`. `spinner/SongSpinner.interop.js` is the one `window.SpinnerInterop`, used by the app and (embedded in Application, served by `LocalOverlayServer`) by the overlay. `lib/` and `spinner/spin-wheel-iife.js` are vendored; don't edit them |
| `tests/SonglistSpinner.*.Tests` | xUnit v3 tests for Core and Application. Folders mirror `src` |
| `tests/SonglistSpinner.Testing` | Helpers the test projects share (a fake clock that reports its timers, `wwwroot` reader). Holds no tests |
| `tests/SonglistSpinner.StreamerSongListSimulator` | In-memory StreamerSongList API v2 and Centrifugo event service, for integration tests and manual runs. Holds no tests |
| `tests/SonglistSpinner.IntegrationTests` | xUnit v3 tests of the real API client, event source and session services against the simulator over loopback HTTP and WebSocket, and of `LocalOverlayServer` on a free localhost port (never 5150). Folders mirror `src`; `Simulator/` tests the simulator itself |
| `tests/SonglistSpinner.EndToEndTests` | Opt-in Playwright tests that start the built Desktop app against the simulator on a test profile and drive its user workflows. They skip unless `SONGLISTSPINNER_E2E=1` |
| `tests/JavaScript` | `node:test` tests for the wheel, overlay and Settings scripts |
| `scripts/` | Single-file publish, smoke test, release checks (used by CI) |
| `docs/` | API v2 notes, release process, single-file distribution |

## Prerequisites

- The .NET SDK pinned by `global.json`, plus the `maui-windows` workload
  (`dotnet workload install maui-windows`). The Desktop project builds only on Windows.
- Node.js 24 for the JavaScript tests.

## Commands

Run from the repository root. CI (`.github/workflows/ci.yml`) runs the same sequence.

```powershell
dotnet restore SonglistSpinner.Desktop.sln
dotnet format SonglistSpinner.Desktop.sln --verify-no-changes --severity warn --no-restore
dotnet test --solution SonglistSpinner.Desktop.sln -c Release
node --test "tests/JavaScript/**/*.test.cjs"
dotnet build SonglistSpinner.Desktop.sln -c Release
```

`dotnet test` runs on Microsoft.Testing.Platform (set in `global.json`), so it takes the target as
an option and filters with MTP flags, not VSTest's `--filter`. Narrow while iterating:

```powershell
dotnet test --project tests/SonglistSpinner.Core.Tests
dotnet test --project tests/SonglistSpinner.Core.Tests --filter-class SonglistSpinner.Core.Tests.Winner.NowPlayingTransitionServiceTests
```

`--filter-method` narrows to one test. Run `dotnet format SonglistSpinner.Desktop.sln` without
`--verify-no-changes` to apply fixes. The JavaScript command needs the quoted glob; Node 24
rejects a bare directory.

The integration tests are part of the default `dotnet test --solution` run (the project takes about two
seconds), so CI runs them with everything else. On their own:

```powershell
dotnet test --project tests/SonglistSpinner.IntegrationTests
```

### End-to-end tests

The end-to-end tests open real SonglistSpinner windows, so they are opt-in: in the default run (and CI) every
one reports as skipped. Run them from a desktop session and leave the windows alone until they finish (the full
suite, 136 tests, takes about ten minutes, mostly app launches):

```powershell
./scripts/run-e2e.ps1
./scripts/run-e2e.ps1 -Filter SonglistSpinner.EndToEndTests.WinnerActionTests
```

`-Filter` takes one class. For several classes or one test, call `dotnet test` with the opt-in set (MTP flags
repeat): `$env:SONGLISTSPINNER_E2E='1'; dotnet test --project tests/SonglistSpinner.EndToEndTests -c Release
--filter-method SonglistSpinner.EndToEndTests.SpinTests.<method>` (build the Desktop app first, as the script
does). The suites, one class per feature or workflow:

| Area | Classes |
| --- | --- |
| Startup and setup | `FirstRunSetupTests`, `ChannelSetupTests`, `DefaultChannelTests`, `UpdateBannerTests` |
| Channel, health and API errors | `ChannelLoadingTests`, `ServiceHealthTests`, `ApiErrorStateTests`, `RealtimeQueueTests`, `RealtimeReconnectTests` |
| Spin and winner | `SpinTests`, `WinnerDialogTests`, `WinnerActionTests`, `WinnerActionOutcomeTests`, `NowPlayingCompletionTests` |
| Played list and Now Playing | `PlayedSongsListTests`, `PlayedListFormattingTests`, `PlayedListLayoutTests`, `PlayHistoryPeriodTests`, `PlayedSongExclusionTests`, `NowPlayingDisplayTests` |
| Settings | `SettingsDraftTests`, `SettingsResetTests`, `SettingsPersistenceTests`, `ConnectionSettingsTests`, `AdvancedSettingsTests`, `SettingsPreviewTests` |
| OBS overlay (all but `OverlayEventsTests` need Edge, below) | `ObsBrowserSourceTests`, `OverlayEventsTests`, `OverlayWheelTests`, `OverlayWinnerRevealTests`, `OverlayPanelTests`, `OverlayLayoutSyncTests`, `OverlayThemeTests` |
| Accessibility | `AccessibleStateTests` |

The script builds the Desktop app (Release unless `-Configuration Debug`) and runs the tests with
`SONGLISTSPINNER_E2E=1`. The app under test is the built executable, started against an in-process simulator on a
temporary test profile (see below), a free overlay port and an update URL on the simulator; Playwright drives its
WebView through `ConnectOverCDPAsync`. Every port is picked free per run, so several runs can share the machine.

- **One app per test class.** A class takes `IClassFixture<SharedApp>` and each test calls
  `EndToEnd.SkipUnlessEnabled()` then `sharedApp.BeginTestAsync(...)`. The first test launches the app; later ones
  get it reset (winner dialog dismissed, channel unloaded, a new Dashboard, the simulator emptied). When the
  previous test left state a reset cannot undo (saved settings or credential, another page or a dialog, a
  collapsed or resized overlay list), the app is relaunched on a fresh profile instead; the test output says
  which. A test that needs a first run or a restart starts its own `AppScenario` (see `SettingsPersistenceTests`).
- **Each test seeds its own data** with `ChannelSeed` and the songs in `SongCatalog`, and asserts exact values
  derived from them. `ApiCalls` matches the simulator's request log for the calls an action must make.
- **Page objects** in `Pages/` (`DashboardPage`, `WinnerDialog`, `PlayedListPanel`, `HealthBar`, `SettingsPage`
  and one class per Settings section, `SetupWizard`, `UpdateBanner`, `OverlayPage`) expose user actions and
  retrying expectations. They are `partial`, so a new test file can add members in its own file. Select elements
  by their ids and accessible names.
- **The wheel** is a canvas, so `WheelProbe` records the labels each page passes to `SpinnerInterop.createWheel`;
  assert them with `ExpectWheelLabelsAsync`.
- **The OBS overlay** is opened by `AppScenario.OpenOverlayAsync()` in headless Microsoft Edge (Playwright's
  `msedge` channel, the system browser), so no Playwright browser download is needed. Without Edge, run
  `pwsh tests/SonglistSpinner.EndToEndTests/bin/Release/net10.0/playwright.ps1 install chromium` and drop the
  channel in `OverlayBrowser`.

When a class ends, pass or fail, its app and WebView2 processes are killed and its profile is deleted. Wait on
conditions (Playwright expectations, the simulator's request log), never on time.

### Coverage

Coverage is a diagnostic, not a target. Use it to find behaviour no test exercises, then decide whether that
behaviour deserves a test; never write a test to move the number, and judge a test by what it would catch.

```powershell
dotnet test --solution SonglistSpinner.Desktop.sln -c Release --coverage --coverage-output-format cobertura --coverage-settings tests/coverage.settings.xml
```

Each test project writes its own `TestResults/<guid>.cobertura.xml` at the repository root (ignored by git).
Core is exercised by all three .NET test projects and Application by two, so read the reports together, taking
the higher hit count for each line, rather than one project's figure. `tests/coverage.settings.xml` names the
two measured assemblies; without it the report is empty. Desktop (Razor pages, MAUI services) is not measured,
because no test project references it, and the JavaScript has no coverage tool.

## StreamerSongList simulator

`tests/SonglistSpinner.StreamerSongListSimulator` stands in for the StreamerSongList REST API
(`/streamers`, `/queue`, `/play_history`, `/queue/played`, `/queue/{id}/play`) and the Centrifugo event
WebSocket (`/connection/websocket`), holding its channels in memory. It never contacts the real service.
It encodes the wire format itself rather than referencing Core, so a client-side change to that format
fails the integration tests; keep it in step with `docs/API_V2.md` and the client's transport models.

- In a test: `await using var simulator = await StreamerSongListSimulator.StartAsync(...)` listens on
  127.0.0.1 with a free port (`ApiBaseAddress`, `EventsEndpoint`, `AccessToken`). Seed with `AddChannel`
  and the channel's `RequestSongAsync`/`AddPlayedSongAsync`/`SetNowPlayingAsync`/`MarkPlayedAsync` (each
  publishes its change to subscribed sockets); inject faults with `FailNextRequests`, `DropNextRequests`
  (abort the connection), `HoldNextRequest`, `DropEventConnections` and `RejectEventConnections`; inspect with
  `Requests`, `RequestsTo`, `WaitForRequestAsync` (the next match) and `WaitForFirstRequestAsync` (the first
  match already logged, or the next). `LatestRelease` sets the GitHub-style release served at
  `_simulator/releases/latest` for the app's update check. `Reset()` empties a running simulator, so one
  simulator, and the app pointed at it, can serve several tests.
- By hand: `dotnet run --project tests/SonglistSpinner.StreamerSongListSimulator -- --port 5199 --seed demo`
  (`--token` sets the accepted token, default `simulator-token`; `--seed none` starts empty). The demo
  channel is `demo` on Twitch, streamer 1001. `POST /_simulator/requests?streamer_id=1001&artist=..&title=..`
  adds a viewer request and `POST /_simulator/events/drop` drops the event sockets.

Point the app at the running simulator by starting it from a shell with these variables
(`EnvironmentOverrides` reads them once at startup):

```powershell
$env:SONGLISTSPINNER_SSL_API_BASE_URL = "http://127.0.0.1:5199/"
$env:SONGLISTSPINNER_SSL_EVENTS_URL = "ws://127.0.0.1:5199/connection/websocket"
$env:SONGLISTSPINNER_SSL_ACCESS_TOKEN = "simulator-token"
$env:SONGLISTSPINNER_SSL_TOKEN_TYPE = "streamer"
```

The two URLs replace the production endpoints. The token is only a fallback: a token saved in Settings
(Windows secure storage) takes precedence and would be sent to the simulator, which rejects it, so clear
the saved token first, or keep your own profile out of it with a test profile:

```powershell
$env:SONGLISTSPINNER_PROFILE_DIR = "$env:TEMP\songlistspinner-test"   # a full path
$env:SONGLISTSPINNER_OVERLAY_PORT = "5151"                           # leaves 5150 to a running app
$env:SONGLISTSPINNER_UPDATE_RELEASE_URL = "http://127.0.0.1:5199/_simulator/no-releases"
```

With `SONGLISTSPINNER_PROFILE_DIR` set, the app keeps its settings (`preferences.json`), API credential
(`secrets.json`, plain text: test credentials only), logs and WebView2 data in that folder instead of MAUI
preferences, Windows secure storage and `%LOCALAPPDATA%\SonglistSpinner`; `MauiProgram` holds the one switch.
`SONGLISTSPINNER_WEBVIEW_ARGS` passes extra browser arguments to the WebView, but only with a test profile; the
end-to-end tests open the DevTools port with it, because the WebView ignores WebView2's own
`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` in this app. Windows' shared Direct3D shader cache
(`%LOCALAPPDATA%\D3DSCache`) is still written, as by any app that draws with the GPU.

## Conventions

- Code style is in `.editorconfig`; shared build settings and analyzers are in
  `Directory.Build.props`, which treats warnings as errors.
- Test methods are named `Given_X_When_Y_Then_Z`, with Arrange, Act and Assert separated by blank
  lines. A test class is `<TypeUnderTest>Tests`, in the folder that mirrors the type's `src` path.
  Core is organised by feature, not by layer: put a new Core type in the feature folder it belongs to
  (Application and Desktop keep their `Services` and `Components` folders).
- Test projects need `<OutputType>Exe</OutputType>` (xunit.v3 on Microsoft.Testing.Platform).
- Production code takes the clock and randomness as dependencies: `TimeProvider` instead of
  `DateTime.Now`/`UtcNow` or a bare `Task.Delay`, and the injected `Random` for winner picks. Tests use
  `FakeTimeProvider` and advance it; they never wait on the wall clock. When a background task owns the
  delay, wait for its timer first (`TimerTrackingTimeProvider` in each test project), then advance.
- Log through an injected `ILogger<T>` with message templates (never `$"..."` or `Trace`), and pass the
  exception rather than `ex.Message`. Log transitions and failures, not UI status text. Never log
  tokens, request URIs with their query, or whole request objects. With Settings debug mode on,
  `DiagnosticFileLoggerProvider` writes app entries from `Debug` and framework entries from `Warning`
  to `%LOCALAPPDATA%\SonglistSpinner\logs\songlistspinner.log`.
- Persisted settings are a contract. `SettingsDto` is serialised as JSON into MAUI Preferences, so
  renaming a persisted type or property must keep the wire name (`[JsonPropertyName]`) or ship a
  migration with a round-trip test. Keep `SettingsResetPlan`'s field table and its test in step. Saved
  settings JSON is embedded under `tests/SonglistSpinner.Core.Tests/Settings/Fixtures`, named for the
  version that wrote it (release 1.2.0, and develop at 4f36e18), and must keep loading; add a fixture for
  a new release's format, never regenerate an old one.
- Default settings live once, in `SpinnerDefaults` (Core). `SettingsDto` and the `Spinner*Config` models
  both start from it, and `SettingsDtoTests` locks the values a user with no saved settings gets.
- JavaScript contracts are guarded by tests that read `wwwroot` from the source tree, so they need the
  repository checkout: `OverlayEventNames`, `SpinnerSettingValues` and `SpinnerInteropMethods` must
  match `SongSpinner.contracts.js` and the `SpinnerInterop` exports. Pass `[JSInvokable]` names to
  JavaScript with `nameof` instead of writing them in the script.
- Register services with one `Add<Feature>()` extension per feature, in a `<Feature>ServiceCollectionExtensions`
  file beside the feature's types (for example `AddStreamerSession` in Application, `AddStreamerSongList` in
  Desktop); `MauiProgram` only composes them. Inject MAUI platform APIs (`IPreferences`, `ISecureStorage`,
  `IClipboard`, `ILauncher`) and `HttpClient` (through `AddHttpClient`) instead of using the static APIs or
  `new HttpClient`. Application code reads saved values through `IKeyValueStore` and `ISecretStore`, which
  Desktop implements over `IPreferences` and `ISecureStorage`. Environment variables are read once, into
  `EnvironmentOverrides`.
- Fix defects test-first.

## Branches and releases

Feature branches merge into `develop`; `develop` is promoted to `main` by pull request, and only
that promotion publishes a release. Before a release merge, bump `VersionPrefix` and
`ApplicationVersion` in `src/SonglistSpinner.Desktop/SonglistSpinner.Desktop.csproj`. The full
process is in `docs/RELEASING.md`.

Don't commit `bin/`, `obj/`, `artifacts/`, `TestResults/`, logs, IDE state or credentials.
