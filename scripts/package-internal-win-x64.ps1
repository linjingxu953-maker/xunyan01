param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$IconPngPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$env:AVALONIA_TELEMETRY_OPTOUT = "1"

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-PathUnder([string]$Path, [string]$Root) {
    $trimChars = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $fullPath = Get-FullPath $Path
    $fullRoot = (Get-FullPath $Root).TrimEnd($trimChars)
    $rootWithSeparator = $fullRoot + [System.IO.Path]::DirectorySeparatorChar

    if ($fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath
    }

    throw "Refusing to operate outside expected root. Path='$fullPath' Root='$fullRoot'"
}

function Remove-SafeDirectory([string]$Path, [string]$AllowedRoot) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $safePath = Assert-PathUnder $Path $AllowedRoot
    Remove-Item -LiteralPath $safePath -Recurse -Force
}

function Write-PngIconFile([string]$SourcePngPath, [string]$DestinationIcoPath) {
    Add-Type -AssemblyName System.Drawing

    $source = $null
    $bitmap = $null
    $graphics = $null
    $memory = $null

    try {
        $source = [System.Drawing.Image]::FromFile($SourcePngPath)
        $bitmap = New-Object System.Drawing.Bitmap -ArgumentList 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($source, 0, 0, 256, 256)

        $memory = New-Object System.IO.MemoryStream
        $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $memory.ToArray()

        $directory = Split-Path -Parent $DestinationIcoPath
        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
        }

        $stream = [System.IO.File]::Create($DestinationIcoPath)
        $writer = $null
        try {
            $writer = New-Object System.IO.BinaryWriter -ArgumentList $stream
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]1)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$pngBytes.Length)
            $writer.Write([UInt32]22)
            $writer.Write($pngBytes)
        }
        finally {
            if ($writer -ne $null) {
                $writer.Dispose()
            }
            else {
                $stream.Dispose()
            }
        }
    }
    finally {
        if ($memory -ne $null) { $memory.Dispose() }
        if ($graphics -ne $null) { $graphics.Dispose() }
        if ($bitmap -ne $null) { $bitmap.Dispose() }
        if ($source -ne $null) { $source.Dispose() }
    }
}

function Compress-ArchiveWithRetry([string]$SourcePath, [string]$DestinationPath) {
    $lastError = $null

    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            if (Test-Path -LiteralPath $DestinationPath) {
                Remove-Item -LiteralPath $DestinationPath -Force
            }

            Compress-Archive -LiteralPath $SourcePath -DestinationPath $DestinationPath -Force
            return
        }
        catch {
            $lastError = $_
            if ($attempt -eq 6) {
                break
            }

            Start-Sleep -Seconds ([Math]::Min($attempt * 2, 10))
        }
    }

    throw $lastError
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$artifactsRoot = Join-Path $repoRoot "artifacts\internal-test"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageName = "DesktopMascot-InternalTest-$Runtime-$timestamp"
$packageRoot = Join-Path $artifactsRoot $packageName
$appDir = Join-Path $packageRoot "app"
$zipPath = Join-Path $artifactsRoot "$packageName.zip"
$projectPath = Join-Path $repoRoot "src\DesktopMascot.App\DesktopMascot.App.csproj"
$assetsSource = Join-Path $repoRoot "assets"

if ([string]::IsNullOrWhiteSpace($IconPngPath)) {
    $IconPngPath = Join-Path $repoRoot "assets\icons\desktop-icon.png"
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "App project not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $assetsSource)) {
    throw "Assets directory not found: $assetsSource"
}

if (-not (Test-Path -LiteralPath $IconPngPath)) {
    throw "Shortcut icon PNG not found: $IconPngPath"
}

New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
Remove-SafeDirectory -Path $packageRoot -AllowedRoot $artifactsRoot
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $appDir | Out-Null

& dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --no-restore `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $appDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

try {
    & dotnet build-server shutdown | Out-Null
}
catch {
    Write-Warning "dotnet build-server shutdown failed; continuing package creation."
}
Start-Sleep -Milliseconds 750

$assetsTarget = Join-Path $appDir "assets"
New-Item -ItemType Directory -Force -Path $assetsTarget | Out-Null
Get-ChildItem -LiteralPath $assetsSource -Force | Copy-Item -Destination $assetsTarget -Recurse -Force

$iconPngTarget = Join-Path $appDir "DesktopMascotIcon.png"
$iconIcoTarget = Join-Path $appDir "DesktopMascot.ico"
Copy-Item -LiteralPath $IconPngPath -Destination $iconPngTarget -Force
Write-PngIconFile -SourcePngPath $IconPngPath -DestinationIcoPath $iconIcoTarget

$installScript = @'
param(
    [string]$InstallDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-XunyanShortcutFileName {
    return [string]::Concat(
        [char]0x5BFB,
        [char]0x7814,
        "01",
        [char]0x684C,
        [char]0x9762,
        [char]0x52A9,
        [char]0x624B,
        ".lnk")
}

$sourceAppDir = Join-Path $PSScriptRoot "app"
if (-not (Test-Path -LiteralPath $sourceAppDir)) {
    throw "app directory not found next to install.ps1"
}

if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $InstallDir = Join-Path $localAppData "Programs\DesktopMascot"
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Get-ChildItem -LiteralPath $sourceAppDir -Force | Copy-Item -Destination $InstallDir -Recurse -Force

$exePath = Join-Path $InstallDir "DesktopMascot.App.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "DesktopMascot.App.exe was not installed correctly."
}

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shortcutPath = Join-Path $desktop (Get-XunyanShortcutFileName)
$iconPath = Join-Path $InstallDir "DesktopMascot.ico"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = [System.IO.Path]::GetFileNameWithoutExtension($shortcutPath)
if (Test-Path -LiteralPath $iconPath) {
    $shortcut.IconLocation = $iconPath
}
$shortcut.Save()

Write-Output "Installed 寻研01 to: $InstallDir"
Write-Output "Desktop shortcut created: $shortcutPath"
Write-Output "No API Key is bundled. Configure Provider/API Key on first run."
'@

$uninstallScript = @'
param(
    [switch]$RemoveUserData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-XunyanShortcutFileName {
    return [string]::Concat(
        [char]0x5BFB,
        [char]0x7814,
        "01",
        [char]0x684C,
        [char]0x9762,
        [char]0x52A9,
        [char]0x624B,
        ".lnk")
}

function Remove-SafeDirectory([string]$Path, [string]$AllowedRoot) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $trimChars = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = ([System.IO.Path]::GetFullPath($AllowedRoot)).TrimEnd($trimChars)
    $rootWithSeparator = $fullRoot + [System.IO.Path]::DirectorySeparatorChar

    if (-not ($fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to remove outside expected root. Path='$fullPath' Root='$fullRoot'"
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$shortcutPath = Join-Path $desktop (Get-XunyanShortcutFileName)
if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$programsRoot = Join-Path $localAppData "Programs"
$installDir = Join-Path $programsRoot "DesktopMascot"
Remove-SafeDirectory -Path $installDir -AllowedRoot $programsRoot

if ($RemoveUserData) {
    $appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    Remove-SafeDirectory -Path (Join-Path $appData "DesktopMascot") -AllowedRoot $appData
    Remove-SafeDirectory -Path (Join-Path $appData "DesktopAIMascot") -AllowedRoot $appData
}

Write-Output "寻研01 app files removed."
if ($RemoveUserData) {
    Write-Output "User data removed."
}
else {
    Write-Output "User data kept. Re-run with -RemoveUserData to remove AppData settings/memory/logs."
}
'@

$readme = @'
# 寻研01 Internal Test Package

## Install

1. Extract the whole zip.
2. Open PowerShell in the extracted folder and run:

   powershell -ExecutionPolicy Bypass -File .\install.ps1

3. The installer copies the app to:

   %LOCALAPPDATA%\Programs\DesktopMascot

4. It creates a desktop shortcut named Xunyan01 Desktop Assistant in Chinese, using DesktopMascot.ico generated from assets/icons/desktop-icon.png.

## Uninstall

Run this from the extracted folder:

   powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

By default it removes only app files and the desktop shortcut. User settings, memory, and logs are kept.

To remove user data too:

   powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -RemoveUserData

## API Key

This package does not contain any API Key. Configure each tester's Provider/API Key on first run.
API Keys are encrypted for the current Windows user and cannot be shared by copying config files.
'@

Set-Content -LiteralPath (Join-Path $packageRoot "install.ps1") -Value $installScript -Encoding UTF8
Set-Content -LiteralPath (Join-Path $packageRoot "uninstall.ps1") -Value $uninstallScript -Encoding UTF8
Set-Content -LiteralPath (Join-Path $packageRoot "README-INTERNAL-TEST.md") -Value $readme -Encoding UTF8

$unexpectedSettings = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter "app_settings.json" -ErrorAction SilentlyContinue)
if ($unexpectedSettings.Count -gt 0) {
    throw "Package unexpectedly contains app_settings.json"
}

Compress-ArchiveWithRetry -SourcePath $packageRoot -DestinationPath $zipPath

$zipItem = Get-Item -LiteralPath $zipPath
$appExe = Join-Path $appDir "DesktopMascot.App.exe"
$iconIco = Get-Item -LiteralPath $iconIcoTarget

[pscustomobject]@{
    PackageRoot = $packageRoot
    ZipPath = $zipPath
    ZipSizeMB = [Math]::Round($zipItem.Length / 1MB, 2)
    AppExe = $appExe
    ShortcutIcon = $iconIco.FullName
    ShortcutIconSizeKB = [Math]::Round($iconIco.Length / 1KB, 2)
}
