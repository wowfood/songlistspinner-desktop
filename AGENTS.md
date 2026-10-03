# AGENTS.md

Songlist Spinner is a Windows desktop app (.NET 10 MAUI Blazor Hybrid) that loads a
StreamerSongList queue, spins a wheel to pick the next song, and serves an OBS browser-source
overlay from a local HTTP server. User-facing documentation is in `README.md`.

## Project map

| Path | What lives there |
| --- | --- |
| `src/SonglistSpinner.Core` | MAUI-free domain: StreamerSongList API v2 and Centrifugo client (`Api/V2`), settings DTO and normaliser (`Data`), models, services |
| `src/SonglistSpinner.Application` | MAUI-free app services: `StreamerSessionService` (queue session, realtime refresh), `OverlayStateService` (overlay state and SSE) |
| `src/SonglistSpinner.Desktop` | MAUI host: Razor pages (`Components/Pages`), MAUI-backed services (`Services`), `MauiProgram.cs` DI |
| `src/SonglistSpinner.Desktop/wwwroot` | Wheel and overlay JavaScript, CSS, `overlay/Overlay.html`. `spinner/SongSpinner.interop.js` is the one `window.SpinnerInterop`, used by the app and (embedded, served by `LocalOverlayServer`) by the overlay. `lib/` and `spinner/spin-wheel-iife.js` are vendored; don't edit them |
| `tests/SonglistSpinner.*.Tests` | xUnit v3 tests for Core and Application. Folders mirror `src` |
| `tests/JavaScript` | `node:test` tests for the wheel scripts |
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
dotnet test --project tests/SonglistSpinner.Core.Tests --filter-class SonglistSpinner.Core.Tests.Services.NowPlayingTransitionServiceTests
```

`--filter-method` narrows to one test. Run `dotnet format SonglistSpinner.Desktop.sln` without
`--verify-no-changes` to apply fixes. The JavaScript command needs the quoted glob; Node 24
rejects a bare directory.

## Conventions

- Code style is in `.editorconfig`; shared build settings and analyzers are in
  `Directory.Build.props`.
- Test methods are named `Given_X_When_Y_Then_Z`, with Arrange, Act and Assert separated by blank
  lines. A test class is `<TypeUnderTest>Tests`, in the folder that mirrors the type's `src` path.
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
  migration with a round-trip test. Keep `SettingsResetPlan`'s field table and its test in step.
- Default settings live once, in `SpinnerDefaults` (Core). `SettingsDto` and the `Spinner*Config` models
  both start from it, and `SettingsDtoTests` locks the values a user with no saved settings gets.
- JavaScript contracts are guarded by tests that read `wwwroot` from the source tree, so they need the
  repository checkout: `OverlayEventNames`, `SpinnerSettingValues` and `SpinnerInteropMethods` must
  match `SongSpinner.contracts.js` and the `SpinnerInterop` exports. Pass `[JSInvokable]` names to
  JavaScript with `nameof` instead of writing them in the script.
- Fix defects test-first.

## Branches and releases

Feature branches merge into `develop`; `develop` is promoted to `main` by pull request, and only
that promotion publishes a release. Before a release merge, bump `VersionPrefix` and
`ApplicationVersion` in `src/SonglistSpinner.Desktop/SonglistSpinner.Desktop.csproj`. The full
process is in `docs/RELEASING.md`.

Don't commit `bin/`, `obj/`, `artifacts/`, `TestResults/`, logs, IDE state or credentials.
