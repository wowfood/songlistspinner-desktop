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
3. Decide whether to retain Windows-only MAUI or migrate the host to WPF.
4. Consider OAuth with PKCE only if the application later needs a multi-user sign-in experience instead of manually entered streamer tokens.
