# Release process

The Windows desktop application uses GitHub Releases as both its distribution
channel and its update-notification source. Releases are immutable and use a
`vMAJOR.MINOR.PATCH` tag generated from the desktop project's `VersionPrefix`.

## Branch workflow

The recommended repository layout is:

1. Make and test changes on `develop` or a short-lived branch based on it.
2. Open a pull request from `develop` into `main`.
3. Complete interactive QA using the pull request's CI artifact if needed.
4. Merge only when `VersionPrefix` identifies the release being published.

Pushes to `develop` and `main`, plus pull requests targeting either branch, run
the complete Windows validation job. A pull request targeting `main` also
verifies that its proposed `v<VersionPrefix>` tag does not already exist. Only
a successful push to `main` that GitHub associates with a merged
`develop`-to-`main` pull request runs the release job, so opening or updating a
pull request cannot publish a release and a direct push cannot publish an
artifact even if repository protections are later loosened.

## Versioning requirement

Before each release merge, update these values in
`src/SonglistSpinner.Desktop/SonglistSpinner.Desktop.csproj`:

- `VersionPrefix` to the next `MAJOR.MINOR.PATCH` version.
- `ApplicationVersion` to a higher positive Windows build number.

Release `v1.1.0` already exists. Every later release merge must increment
`VersionPrefix`. If the generated tag already exists, the `develop` to `main`
pull request fails validation before it can be merged. The release job repeats
the check defensively and never replaces an existing executable.

## Automated validation and publishing

The `Windows CI` workflow:

1. Reads and validates `VersionPrefix`.
2. For a `main` promotion, confirms that the generated release tag is unused.
3. Installs the .NET 10 SDK and MAUI Windows workload.
4. Restores, format-checks, tests, and builds the solution in Release mode.
5. Creates the verified unpackaged, self-contained `win-x64` executable.
6. Launches it and checks the local OBS overlay endpoint.
7. Creates a SHA-256 checksum.
8. Retains both files as a workflow artifact for 14 days.
9. On `main` only, creates `v<VersionPrefix>`, generates release notes, marks
   the release as latest, and attaches:
   - `SonglistSpinner.Desktop.exe`
   - `SonglistSpinner.Desktop.exe.sha256`

The executable remains a single-file application. The checksum is a separate
release download used to verify the executable; users do not need to keep it
beside the application.

## Local candidate build

To create a candidate using the checked-in version:

```powershell
.\scripts\publish-single-file.ps1
```

To test a proposed semantic version and Windows build number explicitly:

```powershell
.\scripts\publish-single-file.ps1 -ReleaseVersion 1.2.0 -BuildNumber 3
```

The script accepts only `MAJOR.MINOR.PATCH` release versions and build numbers
from 1 through 65535. It verifies both the one-file layout and the executable's
embedded product version before succeeding.

## Release QA

Before merging `develop` into `main`:

- Confirm the pull request workflow and all tests passed.
- Download and launch the CI artifact on a clean Windows account or test
  machine; automated startup and overlay smoke testing do not replace
  interactive QA.
- Test connection setup without exposing the streamer token in logs or
  screenshots.
- Test queue loading, spinning, all winner actions, history refresh, and the
  OBS overlay at `http://localhost:5150/overlay`.
- Confirm the version displayed under Settings > Advanced is the intended
  release version.
- Expect an unsigned build to show Windows SmartScreen reputation warnings.
  Code signing can be added later when a suitable certificate and protected
  signing secret are available.
- Do not switch the default API URL from staging until the production v2 API is
  officially available and separately validated.

After the merge, confirm the release contains the executable and checksum and
that the update notification in an older build links to the new release.
