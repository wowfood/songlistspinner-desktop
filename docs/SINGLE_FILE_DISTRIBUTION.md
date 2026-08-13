# Single-file Windows distribution

The supported Phase 1 release artifact is one unpackaged, self-contained,
64-bit Windows executable. It includes the application, .NET runtime, Windows
App SDK runtime, and Blazor Hybrid static assets.

## Build

From the repository root in PowerShell:

```powershell
.\scripts\publish-single-file.ps1
```

Optional release metadata can be supplied explicitly:

```powershell
.\scripts\publish-single-file.ps1 -ReleaseVersion 1.2.0 -BuildNumber 3
```

The script creates:

```text
artifacts\win-x64\SonglistSpinner.Desktop.exe
```

It clears only that output directory before publishing and fails if anything
other than the expected executable remains in the publish layout.
It also verifies that the executable's product version matches the requested
release version.

## End-user requirements

- 64-bit Windows 10 version 1809 or newer, or Windows 11.
- Microsoft Edge WebView2 Evergreen Runtime. It is included with Windows 11
  and most supported Windows 10 installations. If it is absent, install it
  once from [Microsoft's WebView2 download page](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).

The end user does not need to install .NET, the Windows App SDK, MAUI, MSIX, or
the application itself. The executable can be placed in any normal user-writable
folder and launched directly.

## Runtime extraction and data

This is one file for distribution, but it is not a zero-extraction binary.
The .NET host extracts its bundled native and content files into the current
user's temporary `.net` cache on first launch. WebView2 browser data is stored
under `%LOCALAPPDATA%\SonglistSpinner\WebView2`, rather than beside the executable.

These caches can be recreated and are not part of the distributed application.

## Why custom build targets are present

MAUI converts Blazor files to `MauiAsset` items, while the Static Web Assets SDK
normally copies package assets after the .NET single-file bundler has completed.
`Directory.Build.targets` adds all resolved Blazor assets to the bundle and
suppresses those redundant copies.

Windows App SDK 1.5 also omits `PublishSingleFile` from one incremental manifest
target's inputs. The build invalidates that generated manifest before publishing
so a normal build cannot leave a stale, non-redirecting manifest that crashes the
single-file executable at startup.
