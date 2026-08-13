# Release process

The release pipeline validates and drafts releases for the Windows desktop
application. The production StreamerSongList API cutover remains a separate,
intentionally deferred phase.

## Continuous integration

Every push and pull request to `main` runs the `Windows CI` workflow on a fixed
Windows Server 2022 runner. It:

1. Installs the current .NET 10 SDK and MAUI Windows workload.
2. Restores the solution.
3. Verifies that committed C# formatting is clean.
4. Runs all tests in Release configuration.
5. Builds the complete solution in Release configuration.
6. Runs the same verified single-file publish script used locally.
7. Launches the executable and checks its local OBS overlay endpoint.
8. Produces a SHA-256 checksum and retains both release assets for 14 days.

The application continues to target .NET 8. The repository-root `global.json`
pins builds to the .NET 10 SDK family while allowing compatible feature-band
and servicing updates. The newer SDK remains able to target .NET 8 and provides
a consistent MAUI toolchain locally and on hosted runners.

## Local candidate build

The checked-in application version is `1.1.0`. To create a candidate with an
explicit semantic version and Windows build number:

```powershell
.\scripts\publish-single-file.ps1 -ReleaseVersion 1.2.0 -BuildNumber 3
```

The script accepts only `MAJOR.MINOR.PATCH` release versions and Windows build
numbers from 1 through 65535. It verifies both the one-file layout and the
embedded product version before succeeding.

## Draft a GitHub release

After the intended commit has passed `Windows CI`, update `VersionPrefix` in the
desktop project (and increment `ApplicationVersion` for the next checked-in
Windows build), commit that change, and create an annotated tag whose value
matches `VersionPrefix`:

```powershell
git tag -a v1.2.0 -m "SonglistSpinner Desktop 1.2.0"
git push origin v1.2.0
```

The `Draft Windows release` workflow rejects a tag that differs from the project
version. It repeats formatting, tests, build, version verification, launch smoke
testing, and checksum generation before creating a draft GitHub release with
generated notes. It does not publish the release automatically. Review the notes
and attached assets, then publish the draft from GitHub when ready.

Re-running a tag workflow replaces the executable only while the release is
still a draft. It refuses to alter an already published release.

## Release checks

Before publishing the draft:

- Confirm the workflow and all tests passed.
- Confirm the release contains `SonglistSpinner.Desktop.exe` and its `.sha256` file.
- Verify the downloaded executable against the SHA-256 checksum.
- Launch the executable on a clean Windows account or test machine; CI smoke
  testing verifies startup and the overlay but does not replace interactive QA.
- Test saving a streamer access token without exposing it in logs or screenshots.
- Test queue loading, spinning, direct mark-as-played, history refresh, and the
  OBS overlay at `http://localhost:5150/overlay`.
- Expect an unsigned build to show Windows SmartScreen reputation warnings.
  Code signing can be added later when a suitable certificate and protected
  signing secret are available.
- Do not switch the default API URL from staging until the production v2 API is
  officially available and separately validated.
