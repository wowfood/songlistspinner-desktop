# Desktop baseline

This repository was established as a clean, desktop-only continuation of ServerSpinner at source commit `56d51a4bc2e1f13b8fe626c02989a6e4e6eae37d`.

## Included

- The Windows .NET MAUI Blazor Hybrid desktop application
- Shared spinner contracts, models, and queue logic
- Core unit tests

## Excluded

- Blazor WebAssembly client
- Azure Functions backend
- Database migrations and cloud configuration
- Android, iOS, and Mac Catalyst targets
- MSIX certificate, certificate-generation script, and Inno Setup installer
- Bundled WebView2 fixed-runtime binaries
- Previous repository history, including the previously committed Twitch credential

## Baseline changes

- Repository and solution identity changed to `SonglistSpinner.Desktop`.
- Shared namespaces changed from `ServerSpinner.Core` to `SonglistSpinner.Core`.
- Release builds are unpackaged, `win-x64`, and self-contained.
- The legacy Twitch OAuth and chatbot-command integration has been removed.

## Phase 1 distribution status

- The MAUI Windows host is retained for the desktop release.
- Release publishing produces one unpackaged, self-contained `win-x64` executable.
- Blazor static assets and the Windows App SDK runtime are embedded in the executable.
- The repeatable publish command verifies that no sidecar files remain.

## Stage 3 release hardening status

- Application, assembly, and publish version metadata share one semantic version.
- A repository SDK policy keeps local and hosted builds on the .NET 10 toolchain family.
- Windows CI runs tests, a Release build, and verified single-file publishing.
- Semantic version tags create draft releases for manual review and publication.
- Release automation does not change the staging API default.

## Code and product cleanup status

- Removed the unused server-era synchronization abstraction, request DTOs, and no-op desktop service.
- Removed unreachable template pages, layouts, sample assets, and stock .NET artwork.
- Replaced the template icon and splash mark with SonglistSpinner artwork.
- Corrected the Windows application identity and the played-song counter identifier.
- Removed the misleading local history reset action; API play history remains the source of truth.

## Release automation and QA status

- CI enforces formatting in addition to tests and the Release build.
- The published executable is launched and its local OBS overlay is smoke-tested.
- Every candidate receives a SHA-256 checksum alongside the single executable.
- Release tags must match the version declared in the desktop project.
- Dependabot checks NuGet packages and GitHub Actions weekly.

## API v2 migration status

Completed in the next baseline:

1. Modelled queue and play-history transport contracts from the v2 staging reference.
2. Replaced the hard-coded v1 client with a configurable typed client.
3. Added request, mapping, authentication, date-filter, and error-response tests.
4. Added a compatibility boundary that maps v2 DTOs into the existing wheel models.
5. Added Windows secure storage for StreamerSongList credentials.
6. Replaced Twitch `!setSong` and `!setPlayed` messages with direct authenticated queue API operations.

Remaining before a production release:

1. Confirm the production v2 base URL and change the default from staging when StreamerSongList promotes the contract.
2. Add cursor traversal if more than 100 play-history entries must be loaded.
3. Consider OAuth with PKCE only if the application later needs a multi-user sign-in experience instead of manually entered streamer tokens.
