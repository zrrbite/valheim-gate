<#
.SYNOPSIS
    Installs the ICanShowYouTheWorld Valheim mod on this Windows machine.

.DESCRIPTION
    Self-contained, pull-based installer: run it on the Windows box after
    pulling the repo. It finds the Steam install, patches THIS machine's own
    assembly_valheim.dll with the bundled Patcher, and copies both DLLs into
    the game's Managed folder.

    Patching always starts from the vanilla backup, never from whatever is
    currently installed — patching an already-patched assembly would inject
    the mod's entry point twice.

.PARAMETER Restore
    Put the vanilla assembly back and remove the mod DLL.

.PARAMETER ManagedPath
    Override auto-detection, e.g.
    "D:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed"

.EXAMPLE
    .\Install-Mod.ps1
    .\Install-Mod.ps1 -Restore
#>
[CmdletBinding()]
param(
    [switch]$Restore,
    [string]$ManagedPath
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$patcherDir = Join-Path $here 'patcher'

# Game version these binaries were built and tested against (Steam buildid
# observed on the Deck for the same patch). A mismatch is a warning, not a
# hard stop: the assembly is patched locally either way, but the mod DLL was
# compiled against this version's API.
$ExpectedVersion = '0.221.12'
$ExpectedBuildId = '21981559'

function Write-Ok    { param($m) Write-Host "[ok]   $m" -ForegroundColor Green }
function Write-Info  { param($m) Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn2 { param($m) Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err   { param($m) Write-Host "[fail] $m" -ForegroundColor Red }

function Find-ValheimManaged {
    # 1. Steam's own record of where it is installed
    $steamPath = $null
    foreach ($k in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        if (Test-Path $k) {
            $p = (Get-ItemProperty $k -ErrorAction SilentlyContinue)
            if ($p.SteamPath)   { $steamPath = $p.SteamPath }
            elseif ($p.InstallPath) { $steamPath = $p.InstallPath }
            if ($steamPath) { break }
        }
    }

    $libraries = New-Object System.Collections.Generic.List[string]
    if ($steamPath) {
        $steamPath = $steamPath -replace '/', '\'
        $libraries.Add($steamPath)
        # 2. Extra library folders (games often live on a second drive)
        $vdf = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libraries.Add(($m.Groups[1].Value -replace '\\\\', '\'))
            }
        }
    }
    # 3. Last-resort common locations
    $libraries.Add('C:\Program Files (x86)\Steam')
    $libraries.Add('D:\SteamLibrary')

    foreach ($lib in $libraries) {
        $candidate = Join-Path $lib 'steamapps\common\Valheim\valheim_Data\Managed'
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

# ---- Locate the install -------------------------------------------------

if (-not $ManagedPath) { $ManagedPath = Find-ValheimManaged }
if (-not $ManagedPath -or -not (Test-Path $ManagedPath)) {
    Write-Err "Could not find Valheim's Managed folder."
    Write-Info 'Pass it explicitly: .\Install-Mod.ps1 -ManagedPath "D:\...\valheim_Data\Managed"'
    exit 1
}
Write-Info "Target: $ManagedPath"

$installed = Join-Path $ManagedPath 'assembly_valheim.dll'
$vanilla   = Join-Path $ManagedPath 'assembly_valheim.dll.vanilla'
$modTarget = Join-Path $ManagedPath 'ICanShowYouTheWorld.dll'

# ---- Restore mode -------------------------------------------------------

if ($Restore) {
    if (-not (Test-Path $vanilla)) { Write-Err "No vanilla backup at $vanilla"; exit 1 }
    Copy-Item $vanilla $installed -Force
    if (Test-Path $modTarget) { Remove-Item $modTarget -Force }
    Write-Ok 'Restored vanilla assembly and removed the mod DLL.'
    exit 0
}

# ---- Sanity checks ------------------------------------------------------

$modSource = Join-Path $patcherDir 'ICanShowYouTheWorld.dll'
$patcher   = Join-Path $patcherDir 'Patcher.exe'
foreach ($f in @($modSource, $patcher)) {
    if (-not (Test-Path $f)) { Write-Err "Missing bundled file: $f"; exit 1 }
}

# Compare the Steam build against what these binaries were built for
$appManifest = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $ManagedPath))) 'appmanifest_892970.acf'
if (Test-Path $appManifest) {
    $m = [regex]::Match((Get-Content $appManifest -Raw), '"buildid"\s+"(\d+)"')
    if ($m.Success) {
        if ($m.Groups[1].Value -eq $ExpectedBuildId) {
            Write-Ok "Steam buildid $($m.Groups[1].Value) matches the tested build ($ExpectedVersion)."
        } else {
            Write-Warn2 "Steam buildid is $($m.Groups[1].Value); these binaries were built against $ExpectedBuildId ($ExpectedVersion)."
            Write-Warn2 'The assembly is still patched locally, but the mod DLL may not match the game API.'
            Write-Warn2 'If the game misbehaves, rebuild the mod on the Mac against this version.'
        }
    }
}

# ---- Back up the vanilla assembly (once) --------------------------------

if (-not (Test-Path $vanilla)) {
    Copy-Item $installed $vanilla
    Write-Ok "Vanilla backup created: $vanilla"
} else {
    Write-Info 'Vanilla backup already exists — patching from it, not from the installed file.'
}

# ---- Patch --------------------------------------------------------------

$patchedOut = Join-Path $env:TEMP 'valheim-patched\assembly_valheim.dll'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $patchedOut) | Out-Null

# Run with the patcher folder as the working directory: the Patcher resolves
# the mod DLL it reads symbols from relative to the current directory.
Push-Location $patcherDir
try {
    & $patcher $vanilla $patchedOut
    if ($LASTEXITCODE -ne 0) { throw "Patcher exited with code $LASTEXITCODE" }
} finally {
    Pop-Location
}
if (-not (Test-Path $patchedOut)) { Write-Err 'Patcher produced no output.'; exit 1 }
Write-Ok 'Assembly patched.'

# ---- Install ------------------------------------------------------------

Copy-Item $patchedOut $installed -Force
Write-Ok 'Installed patched assembly_valheim.dll'

Copy-Item $modSource $modTarget -Force
Write-Ok 'Installed ICanShowYouTheWorld.dll'

Write-Host ''
Write-Info 'Start Valheim and open the Credits menu to activate the mod.'
Write-Info 'The popup should read v0.221.12-1.'
Write-Info "Roll back with: .\Install-Mod.ps1 -Restore"
Write-Warn2 'A Steam game update overwrites assembly_valheim.dll — re-run this script afterwards.'
