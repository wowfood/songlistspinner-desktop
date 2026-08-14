[CmdletBinding()]
param(
    [string] $ExecutablePath,

    [ValidateRange(5, 120)]
    [int] $TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $repositoryRoot 'artifacts\win-x64\SonglistSpinner.Desktop.exe'
}

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
if ([IO.Path]::GetExtension($resolvedExecutable) -ne '.exe') {
    throw "Smoke testing requires a Windows executable: $resolvedExecutable"
}

$overlayUri = [Uri] 'http://localhost:5150/overlay'
$probeClient = [Net.Sockets.TcpClient]::new()
try {
    $probeTask = $probeClient.ConnectAsync($overlayUri.Host, $overlayUri.Port)
    if ($probeTask.Wait(500) -and $probeClient.Connected) {
        throw "Port $($overlayUri.Port) is already in use. Close the running app before smoke testing."
    }
}
catch [AggregateException] {
    # A refused connection is expected before the test process starts.
}
finally {
    $probeClient.Dispose()
}

$process = Start-Process -FilePath $resolvedExecutable -PassThru -WindowStyle Hidden
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$lastFailure = 'The overlay endpoint did not respond.'

try {
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) {
            throw "The executable exited before its overlay became ready (exit code $($process.ExitCode))."
        }

        try {
            $response = Invoke-WebRequest -Uri $overlayUri -TimeoutSec 2 -UseBasicParsing
            if ($response.StatusCode -eq 200 -and
                $response.Content.Contains('<title>Overlay') -and
                $response.Content.Contains('SonglistSpinner') -and
                $response.Content.Contains('id="nowPlaying"') -and
                -not $response.Content.Contains('id="collapseBtn"')) {
                Write-Host "Smoke test passed: $overlayUri returned the SonglistSpinner overlay."
                return
            }

            $lastFailure = "The overlay response was not the expected SonglistSpinner page."
        }
        catch {
            $lastFailure = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 500
    }

    throw "The executable did not become ready within $TimeoutSeconds seconds. Last error: $lastFailure"
}
finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }

    $process.Dispose()
}
