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

## Next migration phase

1. Model the new StreamerSongList API contract from the staging API reference.
2. Replace the hard-coded v1 URL in `HttpApiService` with configuration and a typed client.
3. Add contract and HTTP fixture tests for queue retrieval, authentication, pagination, and error handling as required by the new API.
4. Introduce a compatibility boundary so UI components depend on internal queue models rather than transport DTOs.
5. Replace Twitch's confidential-client flow with a desktop-appropriate public-client flow before distribution.
6. Decide whether to retain Windows-only MAUI or migrate the host to WPF after the API boundary is covered by tests.

