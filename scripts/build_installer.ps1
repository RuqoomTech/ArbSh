param(
    [string]$Version = "",
    [string]$IsccPath = "",
    [string]$SignToolName = "",
    [string]$SignToolCommand = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = [xml](Get-Content -Raw -LiteralPath (Join-Path $root "Directory.Build.props"))
    $Version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
}
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$') {
    throw "Version must look like 1.2.3 or 1.2.3-label: $Version"
}
if ([string]::IsNullOrWhiteSpace($SignToolName) -ne
    [string]::IsNullOrWhiteSpace($SignToolCommand)) {
    throw "Pass both -SignToolName and -SignToolCommand, or neither."
}
if (-not [string]::IsNullOrWhiteSpace($SignToolName) -and
    $SignToolName -notmatch "^[A-Za-z0-9_-]+$") {
    throw "SignToolName may contain only letters, digits, underscore, and hyphen."
}

$payload = Join-Path $root "dist\payload"
$commandDirectory = Join-Path $payload "bin"
$terminalDirectory = Join-Path $payload "terminal"

if (-not $SkipPublish) {
    if (Test-Path -LiteralPath $payload) {
        Remove-Item -LiteralPath $payload -Recurse -Force
    }

    # arbsh ملف واحد مستقل حتى يبقى مجلد PATH خاليًا من مكتبات .NET.
    $publishes = @(
        @{
            Project = "src_csharp\ArbSh.Console\ArbSh.Console.csproj"
            Output = $commandDirectory
            Extra = @("-p:PublishSingleFile=true")
        },
        @{
            Project = "src_csharp\ArbSh.Terminal\ArbSh.Terminal.csproj"
            Output = $terminalDirectory
            Extra = @()
        }
    )
    foreach ($publish in $publishes) {
        & dotnet publish (Join-Path $root $publish.Project) `
            -c Release -r win-x64 --self-contained true `
            "-p:Version=$Version" -p:DebugType=none `
            -o $publish.Output @($publish.Extra)
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $($publish.Project) with exit code $LASTEXITCODE."
        }
    }
}

foreach ($required in @(
    (Join-Path $commandDirectory "arbsh.exe"),
    (Join-Path $terminalDirectory "ArbSh.Terminal.exe")
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required ArbSh installer input is missing: $required"
    }
}
$unexpected = @(Get-ChildItem -LiteralPath $commandDirectory | Where-Object { $_.Name -ne "arbsh.exe" })
if ($unexpected.Count -ne 0) {
    throw "The PATH directory must contain only arbsh.exe; found: $($unexpected.Name -join ', ')"
}

$reported = (& (Join-Path $commandDirectory "arbsh.exe") --version | Select-Object -First 1)
if ($LASTEXITCODE -ne 0 -or $reported -cne "arbsh $Version") {
    throw "Published arbsh reports '$reported', expected 'arbsh $Version'."
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $IsccPath = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($IsccPath) -or
    -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw "Inno Setup 6 compiler was not found. Pass -IsccPath explicitly."
}

$isccArguments = @(
    "/DMyAppVersion=$Version",
    "/DArbShCommandDir=$commandDirectory",
    "/DArbShTerminalDir=$terminalDirectory"
)
if (-not [string]::IsNullOrWhiteSpace($SignToolName)) {
    $isccArguments += "/DInstallerSignTool=$SignToolName"
    $isccArguments += "/S$SignToolName=$SignToolCommand"
}
Push-Location $root
try {
    & $IsccPath @isccArguments (Join-Path $root "setup.iss")
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

$installer = Join-Path $root "dist\installer\arbsh-setup-$Version-x64.exe"
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "ArbSh installer was not produced at $installer"
}
$installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installer).Hash
$checksum = $installer + ".sha256"
[IO.File]::WriteAllText(
    $checksum,
    "$installerHash *$([IO.Path]::GetFileName($installer))`n",
    [Text.Encoding]::ASCII)
Write-Output $installer
Write-Output $checksum
