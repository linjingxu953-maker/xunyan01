param(
    [string]$ZipPath = "",
    [string]$OutputDirectory = ""
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

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$artifactsRoot = Join-Path $repoRoot "artifacts\internal-test"

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $latestZip = Get-ChildItem -LiteralPath $artifactsRoot -Filter "DesktopMascot-InternalTest-win-x64-*.zip" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestZip) {
        throw "No internal-test zip package found under $artifactsRoot. Run scripts\package-internal-win-x64.ps1 first."
    }

    $ZipPath = $latestZip.FullName
}

$zipItem = Get-Item -LiteralPath $ZipPath
$baseName = [System.IO.Path]::GetFileNameWithoutExtension($zipItem.Name)
$exeName = "$baseName-Setup.exe"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = $artifactsRoot
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$finalExePath = Join-Path $OutputDirectory $exeName

$tmpRoot = "C:\tmp"
if (-not (Test-Path -LiteralPath $tmpRoot)) {
    New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
}

$stageRoot = Join-Path $tmpRoot ("DesktopMascotSelfExtract-" + (Get-Date -Format "yyyyMMddHHmmss"))
$stageRoot = Assert-PathUnder $stageRoot $tmpRoot
Remove-SafeDirectory -Path $stageRoot -AllowedRoot $tmpRoot
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

$projectPath = Join-Path $stageRoot "DesktopMascotSetup.csproj"
$programPath = Join-Path $stageRoot "Program.cs"
$payloadPath = Join-Path $stageRoot "payload.zip"
$publishDir = Join-Path $stageRoot "publish"
$iconSource = Join-Path $artifactsRoot "$baseName\app\DesktopMascot.ico"
$iconPath = Join-Path $stageRoot "DesktopMascot.ico"

Copy-Item -LiteralPath $zipItem.FullName -Destination $payloadPath -Force

$applicationIconLine = ""
if (Test-Path -LiteralPath $iconSource) {
    Copy-Item -LiteralPath $iconSource -Destination $iconPath -Force
    $applicationIconLine = "    <ApplicationIcon>DesktopMascot.ico</ApplicationIcon>"
}

$projectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <AssemblyName>$baseName-Setup</AssemblyName>
    <DebugType>None</DebugType>
    <DebugSymbols>false</DebugSymbols>
$applicationIconLine
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="payload.zip" LogicalName="payload.zip" />
  </ItemGroup>
</Project>
"@

$programSource = @'
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("DesktopMascot internal-test setup");

            var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
            if (resourceStream is null)
            {
                throw new InvalidOperationException("Embedded payload.zip was not found.");
            }

            var extractRoot = Path.Combine(Path.GetTempPath(), "DesktopMascotSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractRoot);

            var zipPath = Path.Combine(extractRoot, "payload.zip");
            using (resourceStream)
            using (var file = File.Create(zipPath))
            {
                resourceStream.CopyTo(file);
            }

            Console.WriteLine("Extracting package...");
            ZipFile.ExtractToDirectory(zipPath, extractRoot, overwriteFiles: true);

            var installScript = Directory
                .EnumerateFiles(extractRoot, "install.ps1", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();

            if (installScript is null)
            {
                throw new FileNotFoundException("install.ps1 was not found inside the package.");
            }

            Console.WriteLine("Running installer...");
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(installScript);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start PowerShell installer.");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("install.ps1 failed with exit code " + process.ExitCode + ".");
            }

            Console.WriteLine("DesktopMascot setup finished.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("DesktopMascot setup failed:");
            Console.Error.WriteLine(ex);
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }
            return 1;
        }
    }
}
'@

Set-Content -LiteralPath $projectPath -Value $projectXml -Encoding UTF8
Set-Content -LiteralPath $programPath -Value $programSource -Encoding UTF8

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:RestoreIgnoreFailedSources=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish for setup exe failed with exit code $LASTEXITCODE"
}

$stageExePath = Join-Path $publishDir $exeName
if (-not (Test-Path -LiteralPath $stageExePath)) {
    $candidate = Get-ChildItem -LiteralPath $publishDir -Filter "*.exe" -File | Select-Object -First 1
    if ($null -eq $candidate) {
        throw "Setup exe was not created in $publishDir"
    }
    $stageExePath = $candidate.FullName
}

Copy-Item -LiteralPath $stageExePath -Destination $finalExePath -Force
$finalExe = Get-Item -LiteralPath $finalExePath

[pscustomobject]@{
    ExePath = $finalExe.FullName
    ExeSizeMB = [Math]::Round($finalExe.Length / 1MB, 2)
    SourceZip = $zipItem.FullName
    SourceZipSizeMB = [Math]::Round($zipItem.Length / 1MB, 2)
    HasCustomExeIcon = [bool](Test-Path -LiteralPath $iconSource)
    StagingDirectory = $stageRoot
}
