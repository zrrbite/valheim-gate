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

.PARAMETER ModOnly
    Copy just the mod DLL, skipping the patch step. This is the normal way to
    pick up mod changes; a full run is only needed after a Valheim update.

.PARAMETER Restore
    Put the vanilla assembly back and remove the mod DLL.

.PARAMETER AllowStale
    Install even when the bundled mod DLL does not match the tag you pulled.
    Only useful for deliberately installing an older build.

.PARAMETER ManagedPath
    Override auto-detection, e.g.
    "D:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed"

.EXAMPLE
    .\Install-Mod.ps1
    .\Install-Mod.ps1 -Restore
#>
[CmdletBinding()]
param(
    [switch]$ModOnly,
    [switch]$Restore,
    [switch]$AllowStale,
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

<#
.SYNOPSIS
    The mod version baked into a built ICanShowYouTheWorld.dll, or $null.

.DESCRIPTION
    READ from the DLL rather than written here as a constant. This line used to
    say "the popup should read v0.221.12-1" and went on saying it for thirty-odd
    alphas — which is worse than saying nothing, because checking the popup
    against the tag you just installed is the ENTIRE point of tagging every
    build. A version the installer states from memory is a version that can lie.

    Reads the raw bytes and looks for the literal rather than loading the
    assembly: ICanShowYouTheWorld.dll references UnityEngine and assembly_valheim,
    and reflection would drag those in and fail outside the game process.
    .NET stores string literals as UTF-16, hence the Unicode decode.
#>
function Get-ModVersion {
    param([string]$Dll)

    try {
        if (-not (Test-Path $Dll)) { return $null }

        $text = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($Dll))

        # Newest shape first: the DATE build (0.221.12-run.2026-08-31b), then the old
        # alpha counter (0.221.12-run.alpha42.1), then the plain pre-Run-Mode shape
        # (0.221.12-1). Order matters only because the first match wins, and the older
        # patterns are kept so this still reads a DLL built before the change.
        #
        # The trailing [a-z] is the same-day build letter, and matching it is not
        # cosmetic — for the same reason the alpha pattern had to match its optional
        # .N. A regex that stops early prints a version that does not exist, which
        # defeats the entire point of checking the popup.
        foreach ($pattern in @('\d+\.\d+\.\d+-run\.\d{4}-\d{2}-\d{2}[a-z]?', '\d+\.\d+\.\d+-run\.alpha\d+(?:\.\d+)?', '\d+\.\d+\.\d+-\d+')) {
            $m = [regex]::Match($text, $pattern)
            if ($m.Success) { return $m.Value }
        }
    } catch {
        # Diagnostics must never be the thing that fails an install.
    }

    return $null
}
function Write-Err   { param($m) Write-Host "[fail] $m" -ForegroundColor Red }

<#
.SYNOPSIS
    Refuse to install a bundled DLL that is older than the tag we pulled.

.DESCRIPTION
    dist\windows\patcher\ICanShowYouTheWorld.dll is a committed binary, and nothing
    on the Mac refreshes it as a side effect of building — staging it is a separate
    step (Scripts\stage_windows.sh). Skip that step and this installer copies a
    STALE build while reporting its version perfectly correctly, so the symptom is
    "the mod says the wrong version" and the cause is three machines away.

    The repo is right here and it knows which tag it is on, so compare the two.
    Silence when git is unavailable: a diagnostic must never block an install.
#>
function Assert-BundledFresh {
    param([string]$Dll)

    $tag = $null
    try { $tag = (& git -C $here describe --tags --abbrev=0 2>$null) } catch { return }
    if (-not $tag) { return }

    $bundled = Get-ModVersion $Dll
    if (-not $bundled) { return }

    if ($bundled -eq $tag) {
        Write-Ok "Bundled build $bundled matches the tag you pulled."
        return
    }

    Write-Err "The bundled mod DLL is $bundled, but this checkout is at $tag."
    Write-Info 'It was never staged for Windows, so installing would give you the OLD build.'
    Write-Info 'On the Mac: Scripts/stage_windows.sh, then commit and push. Then git pull here.'
    Write-Info 'Override with -AllowStale if you really do want the older build.'
    if (-not $AllowStale) { exit 1 }
    Write-Warn2 'Continuing anyway (-AllowStale).'
}

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

# Writing into Program Files needs elevation; fail with a clear reason
# rather than a raw access-denied halfway through.
try {
    $probe = Join-Path $ManagedPath ('.write-probe-{0}' -f ([guid]::NewGuid()))
    New-Item -ItemType File -Path $probe -ErrorAction Stop | Out-Null
    Remove-Item $probe -Force
} catch {
    Write-Err "Cannot write to $ManagedPath"
    Write-Info 'Re-run this script from an elevated PowerShell (Run as Administrator).'
    exit 1
}

Assert-BundledFresh $modSource

# Mod-only refresh: the mod DLL is pure IL and identical on every platform, so
# a change to the mod alone needs no re-patching — the patched assembly already
# installed stays valid until the GAME updates.
if ($ModOnly) {
    if (-not (Test-Path $vanilla)) {
        Write-Err 'No vanilla backup found, so the assembly has never been patched here.'
        Write-Info 'Run a full install first: .\Install-Mod.ps1'
        exit 1
    }
    Copy-Item $modSource $modTarget -Force
    Write-Ok 'Updated ICanShowYouTheWorld.dll (assembly left as-is).'
    Write-Info 'Restart Valheim and open Credits to load the new build.'
    exit 0
}

# Compare the Steam build against what these binaries were built for.
# Managed -> valheim_Data -> Valheim -> common -> steamapps (four levels up).
$steamapps = $ManagedPath
1..4 | ForEach-Object { $steamapps = Split-Path -Parent $steamapps }
$appManifest = Join-Path $steamapps 'appmanifest_892970.acf'
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

# All three paths are passed explicitly, so this does not depend on the
# working directory — PowerShell's location and a child process's actual
# current directory are not the same thing.
& $patcher $vanilla $patchedOut $modSource $ManagedPath
if ($LASTEXITCODE -ne 0) {
    Write-Err "Patcher exited with code $LASTEXITCODE"
    exit 1
}
if (-not (Test-Path $patchedOut)) { Write-Err 'Patcher produced no output.'; exit 1 }
Write-Ok 'Assembly patched.'

# ---- Verify the injections actually landed ------------------------------

# A zero exit code is not proof. A stale bundled patcher/ folder injects the
# entry point and silently omits the death hook, producing an assembly that
# loads the mod but never fires kill events — Run Mode's kill challenges then
# sit at 0 forever, with only a subtle in-game notice to explain it. Both names
# live in the assembly's metadata string heap, so a string scan settles it.
# Checked before the install copy, so a bad patch never reaches the game folder.
$patchedBytes = [IO.File]::ReadAllBytes($patchedOut)
$patchedText  = [Text.Encoding]::ASCII.GetString($patchedBytes)

if (-not $patchedText.Contains('NotACheater')) {
    Write-Err 'Patched assembly is missing the mod entry point — aborting.'
    Write-Info 'Nothing was installed; the game folder is untouched.'
    exit 1
}
if (-not $patchedText.Contains('CharacterDied')) {
    Write-Err 'Patched assembly is missing the Character.OnDeath hook — your patcher/ folder is stale.'
    Write-Info 'Run git pull on the Mac, re-copy dist\windows, and retry.'
    Write-Info 'Nothing was installed; the game folder is untouched.'
    exit 1
}
Write-Ok 'Verified both injections present (entry point + death hook).'

# ---- Install ------------------------------------------------------------

Copy-Item $patchedOut $installed -Force
Write-Ok 'Installed patched assembly_valheim.dll'

Copy-Item $modSource $modTarget -Force
Write-Ok 'Installed ICanShowYouTheWorld.dll'

Write-Host ''
Write-Info 'Start Valheim and open the Credits menu to activate the mod.'

$modVersion = Get-ModVersion $modTarget
if ($modVersion) {
    Write-Info "The popup should read v$modVersion."
} else {
    # Never state a version we did not read — a wrong one is worse than none.
    Write-Warn2 'Could not read the version from the installed DLL; the popup should match the tag you pulled.'
}

Write-Info "Roll back with: .\Install-Mod.ps1 -Restore"
Write-Warn2 'A Steam game update overwrites assembly_valheim.dll — re-run this script afterwards.'
