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

    The backup is refreshed whenever the installed assembly is unpatched,
    because that is what a Steam update leaves behind: a NEW vanilla. A backup
    taken once and kept forever is a backup of the OLD game, and patching that
    installs a previous version's assembly into the updated game — which does
    not start. (This happened on the 1.0 update.)

    The installer also refuses to install a mod DLL built for a different game
    version than the one on disk. A game update needs the mod REBUILT, not just
    re-installed; the installer cannot do that for you.

.PARAMETER ModOnly
    Copy just the mod DLL, skipping the patch step. This is the normal way to
    pick up mod changes; a full run is only needed after a Valheim update.

.PARAMETER Restore
    Put the vanilla assembly back and remove the mod DLL.

.PARAMETER AllowStale
    Install even when the bundled mod DLL does not match the tag you pulled.
    Only useful for deliberately installing an older build.

.PARAMETER IgnoreGameVersion
    Install even when the mod DLL was built against a different Valheim version
    than the one installed. Expect MissingMethodExceptions in the log; this is
    for finding out WHAT broke, not for playing.

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
    [switch]$IgnoreGameVersion,
    [string]$ManagedPath
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$patcherDir = Join-Path $here 'patcher'

function Write-Ok    { param($m) Write-Host "[ok]   $m" -ForegroundColor Green }
function Write-Info  { param($m) Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn2 { param($m) Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err   { param($m) Write-Host "[fail] $m" -ForegroundColor Red }

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
        foreach ($pattern in @('\d+\.\d+\.\d+-run\.\d{4}-\d{2}-\d{2}[a-z]{0,2}', '\d+\.\d+\.\d+-run\.alpha\d+(?:\.\d+)?', '\d+\.\d+\.\d+-\d+')) {
            $m = [regex]::Match($text, $pattern)
            if ($m.Success) { return $m.Value }
        }
    } catch {
        # Diagnostics must never be the thing that fails an install.
    }

    return $null
}

<#
.SYNOPSIS
    The Valheim version (e.g. "1.0.12") compiled into an assembly_valheim.dll, or $null.

.DESCRIPTION
    Same method as Scripts/GetGameVersion.cs on the Mac: find the store into
    Version.CurrentVersion and read the three integer pushes that precede it.
    The version is not a string literal in the assembly, so a byte scan cannot
    find it; this needs Mono.Cecil, which is bundled for the Patcher anyway.
    Works on vanilla and patched assemblies alike. $null when anything goes
    wrong, so a diagnostic never blocks an install by itself.
#>
function Get-GameVersion {
    param([string]$Dll)

    try {
        if (-not (Test-Path $Dll)) { return $null }
        Add-Type -Path (Join-Path $patcherDir 'Mono.Cecil.dll') -ErrorAction Stop

        $asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Dll)
        try {
            foreach ($type in $asm.MainModule.GetTypes()) {
                foreach ($method in $type.Methods) {
                    if (-not $method.HasBody) { continue }
                    $ins = $method.Body.Instructions
                    for ($i = 0; $i -lt $ins.Count; $i++) {
                        $field = $ins[$i].Operand -as [Mono.Cecil.FieldReference]
                        if ($ins[$i].OpCode -ne [Mono.Cecil.Cil.OpCodes]::Stsfld -or -not $field) { continue }
                        if (-not $field.Name.Contains('CurrentVersion')) { continue }

                        # Expect: ldc major, ldc minor, ldc patch, newobj GameVersion, stsfld
                        $nums = @()
                        for ($j = [Math]::Max(0, $i - 8); $j -lt $i; $j++) {
                            $op = $ins[$j].OpCode
                            if ($op -eq [Mono.Cecil.Cil.OpCodes]::Ldc_I4)       { $nums += [int]$ins[$j].Operand }
                            elseif ($op -eq [Mono.Cecil.Cil.OpCodes]::Ldc_I4_S) { $nums += [int][sbyte]$ins[$j].Operand }
                            elseif ($op -eq [Mono.Cecil.Cil.OpCodes]::Ldc_I4_M1) { $nums += -1 }
                            elseif ($op.Code -ge [Mono.Cecil.Cil.Code]::Ldc_I4_0 -and $op.Code -le [Mono.Cecil.Cil.Code]::Ldc_I4_8) {
                                $nums += ([int]$op.Code - [int][Mono.Cecil.Cil.Code]::Ldc_I4_0)
                            }
                        }
                        if ($nums.Count -ge 3) {
                            $n = $nums.Count
                            return "$($nums[$n-3]).$($nums[$n-2]).$($nums[$n-1])"
                        }
                    }
                }
            }
        } finally {
            $asm.Dispose()
        }
    } catch {
        # See Get-ModVersion: diagnostics must never be the thing that fails an install.
    }

    return $null
}

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

    # Newest version tag reachable from HEAD. Not `git describe --tags --abbrev=0`:
    # when two tags sit on the same commit (two builds with no commit in between)
    # describe returns whichever sorts FIRST, i.e. the OLDER one, and this check
    # then rejects a perfectly fresh DLL. Newest commit date wins, and on a tie the
    # higher version; the '[0-9]*' filter skips stray non-version tags ('working').
    $tag = $null
    try {
        $tag = (& git -C $here tag --merged HEAD --list '[0-9]*' --sort=-v:refname --sort=-creatordate 2>$null | Select-Object -First 1)
    } catch { return }
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

<#
.SYNOPSIS
    Refuse to install a mod DLL built against a different Valheim version.

.DESCRIPTION
    The mod is compiled against one specific assembly_valheim.dll, and a game
    update changes signatures the mod calls (1.0 changed five and added an
    interface member). Installing anyway gives a mod that loads, shows its popup,
    and then throws MissingMethodException from whatever it touches first — a
    failure that looks like a bug in the mod rather than a version gap.

    The mod's version starts with the game version it was built for
    (1.0.12-run.2026-09-12 was built against 1.0.12), and the game's own version
    is compiled into its assembly, so the two can simply be compared. This used
    to be a WARNING keyed on the Steam buildid, and the warning was missed on the
    update that mattered; a hard stop with the fix spelled out is the honest form.
#>
function Assert-GameVersionMatches {
    param([string]$ModDll, [string]$GameDll)

    $mod = Get-ModVersion $ModDll
    $game = Get-GameVersion $GameDll
    if (-not $mod -or -not $game) {
        Write-Warn2 'Could not compare the mod and game versions; installing unchecked.'
        return
    }

    $builtFor = ($mod -split '-')[0]
    if ($builtFor -eq $game) {
        Write-Ok "Mod build $mod matches the installed game ($game)."
        return
    }

    Write-Err "The mod was built for Valheim $builtFor, but the installed game is $game."
    Write-Info 'A game update needs the mod REBUILT against the new assembly, not re-installed:'
    Write-Info '  1. Copy this machine''s (unpatched) assembly_valheim.dll, assembly_utils.dll,'
    Write-Info '     assembly_guiutils.dll, Splatform.dll and UnityEngine.UI.dll into libraries\.'
    Write-Info '  2. If the Unity version changed too (Player.log, first lines), refresh libraries\'
    Write-Info '     from https://unity.bepinex.dev/ — see CLAUDE.md, "Unity Version Management".'
    Write-Info '  3. Patch the new assembly, rebuild the mod against it, fix whatever no longer'
    Write-Info '     compiles, tag, stage (Scripts/stage_windows.sh), then re-run this installer.'
    Write-Info 'Override with -IgnoreGameVersion to install anyway (for diagnosis, not play).'
    if (-not $IgnoreGameVersion) { exit 1 }
    Write-Warn2 'Continuing anyway (-IgnoreGameVersion).'
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

# The mod's entry point lives in the assembly's metadata string heap once the
# Patcher has injected it, so a byte scan tells patched from vanilla. Used both
# to decide whether the installed file IS the vanilla and to verify a patch.
function Test-Patched {
    param([string]$Dll)
    return ([Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Dll))).Contains('NotACheater')
}

# 'NotACheater' answers "patched at all" and nothing more — an assembly patched by an
# older Patcher, at an entry point that has since MOVED, passes that test perfectly.
# The Patcher therefore stamps a marker type naming the entry point, and this is what
# tells a current patch from a stale one. THREE things share the name and all three must
# agree: Patcher/Program.cs, this file, Scripts/config.sh (check_injections).
$EntryPointMarker = 'ICSYTW_EntryPoint_FejdStartup_Start'

function Test-EntryPointCurrent {
    param([string]$Dll)
    return ([Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Dll))).Contains($EntryPointMarker)
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

$installedIsPatched = Test-Patched $installed

# Mod-only refresh: the mod DLL is pure IL and identical on every platform, so
# a change to the mod alone needs no re-patching — the patched assembly already
# installed stays valid until the GAME updates.
if ($ModOnly) {
    if (-not $installedIsPatched) {
        if (Test-Path $vanilla) {
            Write-Err 'The installed assembly is unpatched — Valheim has updated since the last install.'
        } else {
            Write-Err 'No vanilla backup found, so the assembly has never been patched here.'
        }
        Write-Info 'A mod-only copy would never be called. Run a full install: .\Install-Mod.ps1'
        exit 1
    }
    if (-not (Test-EntryPointCurrent $installed)) {
        Write-Err 'The installed assembly was patched by an OLDER Patcher — the mod entry point has moved.'
        Write-Info 'A mod-only copy would leave the game calling the old one. Run a full install: .\Install-Mod.ps1'
        exit 1
    }
    Assert-GameVersionMatches $modSource $installed
    Copy-Item $modSource $modTarget -Force
    Write-Ok 'Updated ICanShowYouTheWorld.dll (assembly left as-is).'
    Write-Info 'Restart Valheim to load the new build — the mod loads itself at startup.'
    exit 0
}

# ---- Back up the vanilla assembly ---------------------------------------

# Whatever is installed and NOT patched is the vanilla, and it REPLACES the
# backup: a Steam update leaves exactly that behind, and the old backup is
# then a backup of the old game. Only an installed file that carries the
# injection leaves the backup alone, because then the backup is the only
# unpatched copy there is — and even then it must be from the same game
# version, or it is the stale one that broke the 1.0 update.
if (-not $installedIsPatched) {
    if (Test-Path $vanilla) {
        Write-Info 'Installed assembly is unpatched (game updated?) — refreshing the vanilla backup from it.'
    }
    Copy-Item $installed $vanilla -Force
    Write-Ok "Vanilla backup: $vanilla"
} else {
    if (-not (Test-Path $vanilla)) {
        Write-Err 'The installed assembly is already patched and there is no vanilla backup to patch from.'
        Write-Info 'In Steam: Properties > Installed Files > Verify integrity of game files, then re-run.'
        exit 1
    }
    $installedGame = Get-GameVersion $installed
    $vanillaGame   = Get-GameVersion $vanilla
    if ($installedGame -and $vanillaGame -and $installedGame -ne $vanillaGame) {
        Write-Err "The installed assembly is Valheim $installedGame but the vanilla backup is $vanillaGame."
        Write-Info 'The backup is from an older game; patching it would install the older game''s assembly.'
        Write-Info 'In Steam: Properties > Installed Files > Verify integrity of game files (this puts'
        Write-Info 'the real vanilla back), then re-run this installer.'
        exit 1
    }
    Write-Info 'Installed assembly is already patched — patching from the vanilla backup, not from it.'
}

Assert-GameVersionMatches $modSource $vanilla

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
$patchedText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($patchedOut))

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
if (-not $patchedText.Contains($EntryPointMarker)) {
    Write-Err "Patched assembly is missing the $EntryPointMarker stamp — your Patcher.exe predates the startup entry point."
    Write-Info 'The mod would then only load from the Credits menu, and a saga item can be lost.'
    Write-Info 'Pull the repo again so dist\windows\patcher\Patcher.exe is current, and retry.'
    Write-Info 'Nothing was installed; the game folder is untouched.'
    exit 1
}
Write-Ok 'Verified all injections present (startup entry point + credits + death hook).'

# ---- Install ------------------------------------------------------------

Copy-Item $patchedOut $installed -Force
Write-Ok 'Installed patched assembly_valheim.dll'

Copy-Item $modSource $modTarget -Force
Write-Ok 'Installed ICanShowYouTheWorld.dll'

Write-Host ''
Write-Info 'Start Valheim. The mod loads itself at startup — no Credits menu visit needed.'

$modVersion = Get-ModVersion $modTarget
if ($modVersion) {
    Write-Info "The popup at the main menu should read v$modVersion."
} else {
    # Never state a version we did not read — a wrong one is worse than none.
    Write-Warn2 'Could not read the version from the installed DLL; the popup should match the tag you pulled.'
}

Write-Info "Roll back with: .\Install-Mod.ps1 -Restore"
Write-Warn2 'A Steam game update overwrites assembly_valheim.dll — re-run this script afterwards.'
Write-Warn2 'If the game VERSION changed, the mod must be rebuilt first; this script will refuse otherwise.'
