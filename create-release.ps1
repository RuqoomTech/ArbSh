#Requires -Version 5.1
<#
.SYNOPSIS
  Creates release artifacts for ArbSh.

.DESCRIPTION
  This script can:
  1) Update docs/CHANGELOG.md (optional)
  2) Publish ArbSh.Console and create a release zip
  3) Optionally build the standalone Inno Setup installer through
     scripts/build_installer.ps1 (dist/installer/arbsh-setup-<version>-x64.exe).
     It installs the arbsh command on PATH, the terminal, Start menu entries,
     and an optional Explorer "open here" entry that passes --working-dir.

.PARAMETER Version
  Release version string (e.g. 0.9.0).

.PARAMETER RuntimeID
  Target runtime identifier (default: win-x64).

.PARAMETER TargetFramework
  Target framework (default: net10.0).

.PARAMETER CreateInstaller
  When set, also builds the standalone installer.

.PARAMETER SkipChangelogUpdate
  When set, skips changelog modification.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$RuntimeID = "win-x64",
    [string]$TargetFramework = "net10.0",

    [switch]$CreateInstaller,
    [switch]$SkipChangelogUpdate
)

$ErrorActionPreference = "Stop"

# --- Paths ---
$ProjectRoot = $PSScriptRoot
$ReleasesDir = Join-Path $ProjectRoot "releases"
$ChangelogPath = Join-Path $ProjectRoot "docs\CHANGELOG.md"

$ConsoleProject = Join-Path $ProjectRoot "src_csharp\ArbSh.Console\ArbSh.Console.csproj"

$ConsolePublishDir = Join-Path $ProjectRoot "src_csharp\ArbSh.Console\bin\Release\$TargetFramework\$RuntimeID\publish"

$ConsoleZipName = "ArbSh-v$Version-$RuntimeID.zip"
$ConsoleZipPath = Join-Path $ReleasesDir $ConsoleZipName

$InstallerPackagePath = Join-Path $ProjectRoot "dist\installer\arbsh-setup-$Version-x64.exe"

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message"
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -Path $Path -PathType Container)) {
        New-Item -Path $Path -ItemType Directory -Force | Out-Null
    }
}

function Update-Changelog {
    if ($SkipChangelogUpdate) {
        Write-Step "Skipping CHANGELOG update."
        return
    }

    if (-not (Test-Path -Path $ChangelogPath -PathType Leaf)) {
        Write-Warning "Changelog not found at: $ChangelogPath"
        return
    }

    Write-Step "Updating changelog header for version $Version"
    $content = Get-Content -Path $ChangelogPath -Raw
    $currentDate = Get-Date -Format "yyyy-MM-dd"
    $pattern = '(?m)^## \[Unreleased\](?:\s*-\s*YYYY-MM-DD)?\s*$'

    if ($content -notmatch $pattern) {
        Write-Warning "Could not find '## [Unreleased]' section. Skipping changelog update."
        return
    }

    if ($content -match ("(?m)^## \[" + [regex]::Escape($Version) + "\]\s*-\s*\d{4}-\d{2}-\d{2}\s*$")) {
        Write-Warning "Version $Version already exists in changelog. Skipping update."
        return
    }

    $replacement = @"
## [Unreleased]

### Added
- (Future changes go here)

## [$Version] - $currentDate
"@

    $updated = [regex]::Replace($content, $pattern, $replacement, 1)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($ChangelogPath, $updated, $utf8NoBom)
}

function Invoke-DotNetPublish {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    if (-not (Test-Path -Path $ProjectPath -PathType Leaf)) {
        throw "Project file not found: $ProjectPath"
    }

    Write-Step "Publishing: $ProjectPath"
    & dotnet publish $ProjectPath -c Release -r $RuntimeID --framework $TargetFramework --self-contained true -p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $ProjectPath (exit code $LASTEXITCODE)."
    }
}

function Build-ConsoleReleaseZip {
    Write-Step "Publishing ArbSh.Console"
    Invoke-DotNetPublish -ProjectPath $ConsoleProject

    if (-not (Test-Path -Path $ConsolePublishDir -PathType Container)) {
        throw "Console publish directory not found: $ConsolePublishDir"
    }

    Write-Step "Creating console zip: $ConsoleZipPath"
    if (Test-Path -Path $ConsoleZipPath) {
        Remove-Item -Path $ConsoleZipPath -Force
    }

    Compress-Archive -Path (Join-Path $ConsolePublishDir '*') -DestinationPath $ConsoleZipPath -Force
}

function Build-InstallerPackage {
    Write-Step "Building the standalone Inno Setup installer"
    & (Join-Path $ProjectRoot "scripts/build_installer.ps1") -Version $Version
}

Write-Step "Starting release process for version $Version"
Ensure-Directory -Path $ReleasesDir

Update-Changelog
Build-ConsoleReleaseZip

if ($CreateInstaller) {
    Build-InstallerPackage
} else {
    Write-Step "Installer packaging skipped. Use -CreateInstaller to enable."
}

Write-Step "Release process completed."
Write-Host "Console artifact: $ConsoleZipPath"
if ($CreateInstaller) {
    Write-Host "Installer package: $InstallerPackagePath"
}
