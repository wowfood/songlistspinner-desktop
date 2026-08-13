[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $ReleaseVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\SonglistSpinner.Desktop\SonglistSpinner.Desktop.csproj'
[xml] $project = Get-Content -LiteralPath $projectPath -Raw
$versionNode = $project.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')

if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "VersionPrefix is missing from $projectPath."
}

$declaredVersion = $versionNode.InnerText.Trim()
if ($declaredVersion -ne $ReleaseVersion) {
    throw "Release version $ReleaseVersion does not match the project version $declaredVersion. Update VersionPrefix before creating the tag."
}

Write-Host "Release version $ReleaseVersion matches the project version."
