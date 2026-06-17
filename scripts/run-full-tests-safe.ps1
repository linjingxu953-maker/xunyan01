param(
    [string]$Solution = "DesktopMascot.sln",
    [int]$HangTimeoutSeconds = 90,
    [string]$ArtifactsPath = "",
    [switch]$NoRestore,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $ArtifactsPath = Join-Path "TestResults" "artifacts-safe-full-$timestamp"
}

$dotnetArgs = @(
    "test",
    $Solution,
    "-v",
    "quiet",
    "--logger",
    "console;verbosity=minimal",
    "--blame-hang",
    "--blame-hang-timeout",
    "$($HangTimeoutSeconds)s",
    "--blame-hang-dump-type",
    "none",
    "--artifacts-path",
    $ArtifactsPath
)

if ($NoRestore) {
    $dotnetArgs += "--no-restore"
}

if ($NoBuild) {
    $dotnetArgs += "--no-build"
}

Push-Location $repoRoot
try {
    $env:AVALONIA_TELEMETRY_OPTOUT = "1"

    Write-Host "Running safe full test command:"
    Write-Host "  AVALONIA_TELEMETRY_OPTOUT=1"
    Write-Host "  dotnet $($dotnetArgs -join ' ')"
    Write-Host ""

    & dotnet @dotnetArgs
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Host ""
        Write-Host "Safe full test run failed with exit code $exitCode."
        Write-Host "Artifacts path: $ArtifactsPath"
        exit $exitCode
    }

    Write-Host ""
    Write-Host "Safe full test run completed."
    Write-Host "Artifacts path: $ArtifactsPath"
}
finally {
    Pop-Location
}
