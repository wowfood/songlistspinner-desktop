<#
.SYNOPSIS
Builds the Desktop app and runs the opt-in end-to-end tests against it.

.DESCRIPTION
Each test opens a real SonglistSpinner window, so run this from a desktop session and leave the windows alone
while it runs. The app under test uses a temporary profile, a free overlay port, the in-process StreamerSongList
simulator and a local update URL: it never touches your settings, saved credential, logs or running app, the
real StreamerSongList service or GitHub.

.PARAMETER Configuration
The build configuration of the app and the tests. Release by default.

.PARAMETER Filter
Optional --filter-class value, for example SonglistSpinner.EndToEndTests.WinnerActionTests.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Filter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

dotnet build (Join-Path $repositoryRoot 'src\SonglistSpinner.Desktop\SonglistSpinner.Desktop.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Building the Desktop app failed with exit code $LASTEXITCODE." }

$testArguments = @(
    'test',
    '--project', (Join-Path $repositoryRoot 'tests\SonglistSpinner.EndToEndTests'),
    '-c', $Configuration
)
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $testArguments += @('--filter-class', $Filter) }

$previousOptIn = $env:SONGLISTSPINNER_E2E
$previousConfiguration = $env:SONGLISTSPINNER_E2E_CONFIGURATION
try {
    $env:SONGLISTSPINNER_E2E = '1'
    $env:SONGLISTSPINNER_E2E_CONFIGURATION = $Configuration
    dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { throw "The end-to-end tests failed with exit code $LASTEXITCODE." }
}
finally {
    $env:SONGLISTSPINNER_E2E = $previousOptIn
    $env:SONGLISTSPINNER_E2E_CONFIGURATION = $previousConfiguration
}
