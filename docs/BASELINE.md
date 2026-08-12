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
- Twitch's legacy client secret is read only from `SONGLISTSPINNER_TWITCH_CLIENT_SECRET`; it is not stored in source control.

## API v2 migration status

Completed in the next baseline:

1. Modelled queue and play-history transport contracts from the v2 staging reference.
2. Replaced the hard-coded v1 client with a configurable typed client.
3. Added request, mapping, authentication, date-filter, and error-response tests.
4. Added a compatibility boundary that maps v2 DTOs into the existing wheel models.
5. Added separate secure storage for StreamerSongList credentials and removed the old Twitch-token crossover.

Remaining before a production release:

1. Register the desktop application as a public StreamerSongList OAuth client and implement authorization code flow with PKCE.
2. Confirm the production v2 base URL and change the default from staging when StreamerSongList promotes the contract.
3. Add cursor traversal if more than 200 play-history entries must be loaded.
4. Replace Twitch's confidential-client flow with a desktop-appropriate public-client flow.
5. Decide whether to retain Windows-only MAUI or migrate the host to WPF.
