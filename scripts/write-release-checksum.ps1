[CmdletBinding()]
param(
    [string] $ExecutablePath,
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $artifactRoot 'win-x64\SonglistSpinner.Desktop.exe'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $artifactRoot 'release\SonglistSpinner.Desktop.exe.sha256'
}

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$artifactPrefix = $artifactRoot + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe checksum output path: $resolvedOutput"
}

$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$hash = (Get-FileHash -LiteralPath $resolvedExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = "$hash  $([IO.Path]::GetFileName($resolvedExecutable))"
Set-Content -LiteralPath $resolvedOutput -Value $checksumLine -Encoding utf8NoBOM

Write-Host "SHA-256 checksum created: $resolvedOutput"
