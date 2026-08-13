[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $ReleaseVersion = '1.1.0',

    [ValidateRange(1, 65535)]
    [int] $BuildNumber = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$outputDirectory = [IO.Path]::GetFullPath((Join-Path $artifactRoot 'win-x64'))
$expectedPrefix = $artifactRoot + [IO.Path]::DirectorySeparatorChar

if (-not $outputDirectory.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe output directory: $outputDirectory"
}

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $outputDirectory | Out-Null

$project = Join-Path $repositoryRoot 'src\SonglistSpinner.Desktop\SonglistSpinner.Desktop.csproj'

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $outputDirectory `
    -p:Version=$ReleaseVersion `
    -p:ApplicationDisplayVersion=$ReleaseVersion `
    -p:ApplicationVersion=$BuildNumber

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedFiles = @(Get-ChildItem -LiteralPath $outputDirectory -Recurse -File)
$executable = $publishedFiles | Where-Object { $_.Name -eq 'SonglistSpinner.Desktop.exe' }

if ($publishedFiles.Count -ne 1 -or $null -eq $executable) {
    $publishedNames = ($publishedFiles | ForEach-Object { $_.FullName.Substring($outputDirectory.Length + 1) }) -join ', '
    throw "Single-file verification failed. Expected only SonglistSpinner.Desktop.exe, found: $publishedNames"
}

$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable.FullName)
$productVersion = $versionInfo.ProductVersion.Split('+')[0]

if ($productVersion -ne $ReleaseVersion) {
    throw "Version verification failed. Expected $ReleaseVersion, found $productVersion."
}

$sizeMiB = [Math]::Round($executable.Length / 1MB, 1)
Write-Host "Single executable created: $($executable.FullName) (version $productVersion, build $BuildNumber, $sizeMiB MiB)"
