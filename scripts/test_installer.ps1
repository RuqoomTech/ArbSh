param(
    [Parameter(Mandatory = $true)]
    [string]$Installer,

    [Parameter(Mandatory = $true)]
    [string]$PreviousInstaller,

    [string]$InstallDirectory = ""
)

# بوابة دورة حياة مثبت أربش المستقل لكل مستخدم: تثبيت إصدار سابق، ثم ترقية،
# ثم إصلاح بعد حذف ملفات، ثم إزالة لا تترك ملفات أو قيود أو مدخل PATH.

$ErrorActionPreference = "Stop"

function Assert-Checksum {
    param([string]$Path)
    $checksumPath = $Path + ".sha256"
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Installer checksum file was not found: $checksumPath"
    }
    $line = [IO.File]::ReadAllText($checksumPath, [Text.Encoding]::ASCII).Trim()
    if ($line -notmatch '^([0-9A-Fa-f]{64}) \*(.+)$') {
        throw "Installer checksum file has an invalid format: $checksumPath"
    }
    if ($Matches[2] -cne [IO.Path]::GetFileName($Path)) {
        throw "Installer checksum names the wrong file: $checksumPath"
    }
    if ($Matches[1] -ine (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash) {
        throw "Installer SHA-256 verification failed: $Path"
    }
}

function Get-InstallerVersion {
    param([string]$Path)
    if ([IO.Path]::GetFileName($Path) -notmatch '^arbsh-setup-(.+)-x64\.exe$') {
        throw "Installer name does not carry a version: $Path"
    }
    return $Matches[1]
}

$Installer = (Resolve-Path -LiteralPath $Installer).Path
$PreviousInstaller = (Resolve-Path -LiteralPath $PreviousInstaller).Path
Assert-Checksum $Installer
Assert-Checksum $PreviousInstaller
$version = Get-InstallerVersion $Installer
$previousVersion = Get-InstallerVersion $PreviousInstaller
if ($version -eq $previousVersion) {
    throw "The upgrade check needs two different versions; both are $version."
}

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = Join-Path $env:LOCALAPPDATA (
        "Temp\أربش مثبت مسار طويل " + ('x' * 72) + " " + [Guid]::NewGuid().ToString("N"))
}
$InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
if (Test-Path -LiteralPath $InstallDirectory) {
    throw "Installer test directory already exists: $InstallDirectory"
}

$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{37388BF3-5853-4720-90FD-AB45B9B9E7A7}_is1"
$markerKey = "HKCU:\Software\BaaEcosystem\ArbSh"
$contextKeys = @(
    "HKCU:\Software\Classes\Directory\Background\shell\ArbSh",
    "HKCU:\Software\Classes\Directory\shell\ArbSh",
    "HKCU:\Software\Classes\Drive\shell\ArbSh"
)
# لا يستبدل الاختبار تثبيتًا حقيقيًا ولا قائمة سياق موجودة ثم يحذفها عند الإزالة.
foreach ($key in @($uninstallKey, $markerKey) + $contextKeys) {
    if (Test-Path -LiteralPath $key) {
        throw "Refusing to run over an existing current-user ArbSh registration: $key"
    }
}

$bin = Join-Path $InstallDirectory "bin"
$arbsh = Join-Path $bin "arbsh.exe"
$terminal = Join-Path $InstallDirectory "terminal\ArbSh.Terminal.exe"
$uninstaller = Join-Path $InstallDirectory "unins000.exe"
$userPathBefore = [Environment]::GetEnvironmentVariable("Path", "User")
$installed = $false

function Invoke-Setup {
    param([string]$Path)
    $process = Start-Process -FilePath $Path -ArgumentList @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-",
        "/CURRENTUSER", "/DIR=`"$InstallDirectory`""
    ) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Installer $([IO.Path]::GetFileName($Path)) failed with exit code $($process.ExitCode)."
    }
}

function Get-PathMatches {
    $value = [Environment]::GetEnvironmentVariable("Path", "User")
    return @($value -split ";" | Where-Object {
        $_.Trim().Trim('"').TrimEnd('\') -ieq $bin.TrimEnd('\')
    })
}

function Assert-Installed {
    param([string]$ExpectedVersion, [string]$Stage)
    foreach ($file in @($arbsh, $terminal, $uninstaller)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "${Stage}: missing installed file $file"
        }
    }
    $binFiles = @(Get-ChildItem -LiteralPath $bin | ForEach-Object Name)
    if ($binFiles.Count -ne 1 -or $binFiles[0] -ne "arbsh.exe") {
        throw "${Stage}: the PATH directory must hold only arbsh.exe, found: $($binFiles -join ', ')"
    }
    $reported = @(& $arbsh --version)
    if ($LASTEXITCODE -ne 0 -or $reported[0] -cne "arbsh $ExpectedVersion" -or $reported[1] -cne "arbsh-host-v1") {
        throw "${Stage}: installed arbsh reported '$($reported -join ' / ')', expected arbsh $ExpectedVersion."
    }
    $marker = Get-ItemProperty -LiteralPath $markerKey
    if ($marker.Version -cne $ExpectedVersion -or $marker.InstallLocation -ine $InstallDirectory) {
        throw "${Stage}: installer marker records $($marker.Version) at $($marker.InstallLocation)."
    }
    if ((Get-ItemPropertyValue -LiteralPath $markerKey -Name PathOwned) -ne 1) {
        throw "${Stage}: installer did not record PATH ownership."
    }
    if ((Get-PathMatches).Count -ne 1) {
        throw "${Stage}: the arbsh PATH entry is not present exactly once."
    }
    $display = Get-ItemPropertyValue -LiteralPath $uninstallKey -Name DisplayVersion
    if ($display -cne $ExpectedVersion) {
        throw "${Stage}: Apps & features shows version $display, expected $ExpectedVersion."
    }
    foreach ($key in $contextKeys) {
        $command = (Get-ItemProperty -LiteralPath (Join-Path $key "command"))."(default)"
        if ($command -notlike "`"$terminal`" --working-dir `"%*`"") {
            throw "${Stage}: context menu $key runs '$command'."
        }
    }
}

try {
    Invoke-Setup $PreviousInstaller
    $installed = $true
    Assert-Installed $previousVersion "install"

    $staleFiles = @(
        (Join-Path $InstallDirectory "terminal\removed-by-upgrade.tmp"),
        (Join-Path $InstallDirectory "docs\removed-by-upgrade.tmp")
    )
    foreach ($stale in $staleFiles) { [IO.File]::WriteAllText($stale, "stale") }
    Invoke-Setup $Installer
    Assert-Installed $version "upgrade"
    foreach ($stale in $staleFiles) {
        if (Test-Path -LiteralPath $stale) { throw "upgrade: obsolete payload file survived: $stale" }
    }

    Remove-Item -LiteralPath $arbsh, $terminal -Force
    Invoke-Setup $Installer
    Assert-Installed $version "repair"

    # الأمر المثبت يعمل عبر PATH وحده، ويحافظ على النص العربي ورمز الخروج.
    $oldPath = $env:PATH
    try {
        $env:PATH = "$bin;$env:SystemRoot\System32;$env:SystemRoot"
        $resolved = (Get-Command arbsh -CommandType Application).Source
        if ($resolved -ine $arbsh) { throw "PATH resolves arbsh to $resolved." }
        $greeting = -join [char[]](0x0645, 0x0631, 0x062D, 0x0628, 0x0627)
        $print = -join [char[]](0x0627, 0x0637, 0x0628, 0x0639)
        $output = [Console]::OutputEncoding
        [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
        try { $printed = @(& arbsh --command "$print $greeting") }
        finally { [Console]::OutputEncoding = $output }
        if ($LASTEXITCODE -ne 0 -or $printed.Count -ne 1 -or $printed[0] -cne $greeting) {
            throw "Installed arbsh --command printed '$($printed -join '|')' with exit code $LASTEXITCODE."
        }
        & arbsh --command "arbsh-missing-program-$([Guid]::NewGuid().ToString('N'))" 2>$null
        if ($LASTEXITCODE -ne 127) { throw "Installed arbsh returned $LASTEXITCODE for a missing program, expected 127." }
    }
    finally {
        $env:PATH = $oldPath
    }

    # الطرفية الرسومية تبدأ وتبقى حية؛ يكفي هذا لإثبات اكتمال حمولتها.
    $gui = Start-Process -FilePath $terminal -ArgumentList @("--working-dir", "`"$InstallDirectory`"") -PassThru
    try {
        Start-Sleep -Seconds 5
        if ($gui.HasExited) { throw "Installed ArbSh.Terminal exited early with code $($gui.ExitCode)." }
    }
    finally {
        if (-not $gui.HasExited) { $gui.Kill(); $gui.WaitForExit() }
    }
}
finally {
    if ($installed -and (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
        $process = Start-Process -FilePath $uninstaller -ArgumentList @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"
        ) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "ArbSh uninstaller failed with exit code $($process.ExitCode)."
        }
        for ($attempt = 0; $attempt -lt 150 -and (Test-Path -LiteralPath $InstallDirectory); $attempt++) {
            Start-Sleep -Milliseconds 200
        }
    }
}

if (Test-Path -LiteralPath $InstallDirectory) { throw "Uninstall left installation files in $InstallDirectory." }
foreach ($key in @($uninstallKey, $markerKey) + $contextKeys) {
    if (Test-Path -LiteralPath $key) { throw "Uninstall left registry key $key." }
}
if ((Get-PathMatches).Count -ne 0) { throw "Uninstall left the arbsh PATH entry." }
$userPathAfter = [Environment]::GetEnvironmentVariable("Path", "User")
# المساعد المشترك يحذف المدخلات الفارغة عند الإزالة، فتُقارن المدخلات لا النص الخام.
$entries = { param($value) @("$value" -split ";" | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ";" }
if ((& $entries $userPathAfter) -cne (& $entries $userPathBefore)) {
    throw "Uninstall changed the user PATH beyond its own entry.`nBefore: $userPathBefore`nAfter:  $userPathAfter"
}

Write-Output "ArbSh installer lifecycle passed: install $previousVersion, upgrade to $version, repair, uninstall."
# الفشل يُعلن بـthrow؛ لا يتسرب رمز آخر أمر أصلي (127 المقصود أعلاه) إلى المستدعي.
exit 0
