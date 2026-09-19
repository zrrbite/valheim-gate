# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Valheim game modification project that uses IL patching to inject custom cheat/enhancement features into the game. The project consists of two main components:

1. **ICanShowYouTheWorld** - The main mod DLL (C# .NET Framework 4.7.2) that provides teleportation, god mode, buffs, and other gameplay enhancements
2. **Patcher** - IL patching tool using Mono.Cecil to inject the mod into Valheim's assembly_valheim.dll

Development is done on macOS, with deployment to Steam Deck (Linux/Proton). Assemblies are cross-platform due to .NET IL.

## Build and Development Commands

### Build the solution
```bash
msbuild Valheim.sln -p:Configuration=Debug -v:minimal
```
(vstool from the discontinued Visual Studio for Mac crashes; use Mono's msbuild.)

### Standard update workflow (after Valheim patch)
```bash
# 1. Download assemblies from Steam Deck
Scripts/download.sh

# 2. Patch assembly_valheim.dll (from Patcher/bin/Debug/)
mono Patcher.exe

# 3. Copy patched assembly to libraries
# (Use Patcher/Scripts/copy_valheim_assembly_to_plugin.sh or manual copy)

# 4. Rebuild ICanShowYouTheWorld against new assembly
# (Use vstool build command above)

# 5. Upload to Steam Deck
Scripts/upload_hax.sh      # Uploads ICanShowYouTheWorld.dll
Scripts/upload_valheim.sh  # Uploads patched assembly_valheim.dll
```

### Version management
```bash
Scripts/nextversion.sh                 # prints the next version, e.g. 0.221.12-run.2026-08-31c
git tag "$(Scripts/nextversion.sh)"    # tag it
Scripts/setversion.sh                  # writes the latest tag into Assets/Version.cs
```

Builds are **date-based**: `<game version>-run.<YYYY-MM-DD>[letter]`. The letter is
added from the second build of a day onward (`b`, `c`, …). This replaced an
`alphaNN` counter that reached 95 and by then said nothing a date does not say
better. The `<game version>` prefix is load-bearing — the deploy scripts guard on
it and it records which Valheim API the DLL was compiled against.

Three things parse the version out of the built DLL and all three still read the
old `alphaNN` tags: `Scripts/stage_windows.sh`, `Scripts/make_release.sh`, and
`dist/windows/Install-Mod.ps1`. Change the shape and you must change all three.

**Order matters**: tag *before* `setversion.sh`, and stage *after* building, or
`stage_windows.sh` refuses — it compares the built DLL's version against the tag
precisely to stop a stale build shipping while reporting the right number.

### Unity version detection
```bash
Scripts/get_unity_version.sh  # Detects Unity version from game files
```

## Architecture

### IL Patching Entry Point
The Patcher tool modifies `assembly_valheim.dll` to inject a call to
`ICanShowYouTheWorld.NotACheater.Run()` at the start of **`FejdStartup.Start()`** — the mod
loads itself when the game starts, with no player action. The call into
`FejdStartup.OnCredits()` is still injected as well; the second call is a no-op.

**Why it moved (2026-09-19):** a saga item is only known to the game while the mod is
loaded. Load a character without visiting Credits and Thor's bow is an unresolved prefab
name — `Inventory.AddItem` logs `Failed to find item prefab`, drops it, and the next save
writes the pack without it. Gone, silently. `Start` rather than `Awake` because every
`Awake` in the menu scene has run by then, including `UnifiedPopup`'s, which owns the
version popup. `Run()` catches everything for the same reason: an exception escaping it
would now take the main menu with it.

Two consequences worth knowing. `Start` runs again every time the player returns to the
main menu from a world, so `Run()`'s re-entry path logs and returns rather than popping a
dialog. And the activation popup is queued, not pushed: `UnifiedPopup.instance` is assigned
in its `OnEnable`, so `CheatController.Update` shows the popup on the first frame
`UnifiedPopup.IsAvailable()` says yes.

The Patcher also stamps an empty marker type, `ICSYTW_EntryPoint_FejdStartup_Start`, into
the patched assembly. A byte scan for `NotACheater` only answers "patched at some point",
which is how an assembly patched before the entry point moved would pass verification and
still load the mod only from Credits. **Three things share that marker name and all three
must agree**: `Patcher/Program.cs`, `dist/windows/Install-Mod.ps1`, and
`Scripts/config.sh`. `Install-Mod.ps1 -ModOnly` now refuses an assembly that lacks it.

The patcher also modifies `Minimap.m_pins` from private to public static to allow mod access.

### Service-Based Architecture (New)

The mod now uses a modern service-based architecture with dependency injection:

**Initialization Flow:**
```
FejdStartup.Start()          [and OnCredits(), which is then a no-op]
  → NotACheater.Run()
    → ModBootstrap.Initialize()
      → Creates ServiceContainer singleton
      → Loads Configuration from JSON
      → Creates ValheimGameAPI
      → Instantiates and registers all services:
        - CombatService
        - TeleportService
        - PetService
        - SpawnService
      → Attaches CheatController and UIManager components
```

**Core Components:**

**Foundation Layer:**
- **ModBootstrap** (`Core/ModBootstrap.cs`) - Entry point that initializes the dependency injection container and all services
- **ServiceContainer** (`Core/ServiceContainer.cs`) - Thread-safe DI container supporting singleton/factory/instance registration
- **Configuration** (`Core/Configuration.cs`) - JSON-based configuration system with auto-save/load
- **IGameAPI** (`GameAPI/IGameAPI.cs`) - Abstraction layer over Valheim's game API for testability

**Service Layer:**
- **ITeleportService** (`Services/ITeleportService.cs`) - Teleportation to map cursor, spawn, safe pins
- **ICombatService** (`Services/ICombatService.cs`) - God mode, AoE damage/healing, defensive abilities, weapon scaling
- **IPetService** (`Services/IPetService.cs`) - Tamed creature buffing and damage management
- **ISpawnService** (`Services/ISpawnService.cs`) - Prefab spawning at cursor/player, structure repair, AoE effects

**Legacy Components (to be refactored):**
- **NotACheater.Run()** - Entry point that calls ModBootstrap and creates persistent GameObject
- **CheatController** (Cheat.cs) - Main MonoBehaviour managing lifecycle and command registration
- **CheatCommands.cs** - Legacy static command implementations (being gradually replaced by services)
- **UIManager.cs** - On-screen UI overlay using Unity's IMGUI system
- **InputManager.cs** - Keyboard input polling and command dispatch
- **CommandRegistry** - Global list of keyboard bindings using CommandBinding pattern

### Configuration System

Configuration is stored in JSON at:
- **Linux/Steam Deck**: `~/.config/unity3d/IronGate/Valheim/ICanShowYouTheWorld.json`
- **Windows**: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\ICanShowYouTheWorld.json`

**Features:**
- Auto-created with defaults on first run
- Hot-editable (restart game to apply changes)
- No DLL redeployment needed for config changes
- 28+ configurable settings (pet buffs, AoE power, combat tunables, etc.)

### Service Access Pattern

Services can be accessed via the ServiceContainer:
```csharp
// From anywhere after ModBootstrap.Initialize()
var teleport = ModBootstrap.GetService<ITeleportService>();
teleport.TeleportToMapCursor();

var combat = ModBootstrap.GetService<ICombatService>();
combat.ToggleGodMode();
```

### Command Pattern
Commands are registered using the `CommandBinding` class which encapsulates:
- Key binding (KeyCode)
- Description
- Action (callback)
- State (enabled/disabled)

InputManager polls keyboard in Update() and dispatches to appropriate commands.

**Controller setups**: every command is keyboard-only and numpad-heavy, so on
a machine played with a gamepad (Steam Deck, or a couch Windows setup) the
commands need **Steam Input** remaps — bind controller inputs to the keyboard
keys, e.g. R3 → `F1` for the cheat window. The Deck's on-screen keyboard is no
help: it has no F-keys and its digits send the main row, not `Keypad 0-9`.
Note the trade-off if a controller shows double inputs (a known 8BitDo/Steam
quirk): the usual fix of disabling Steam Input for Valheim also removes the
keyboard-remap ability the mod relies on.

### MonoBehaviour Lifecycle
- `Awake()` - Component initialization
- `Update()` - Per-frame input handling
- `OnGUI()` - Immediate-mode UI rendering (IMGUI)

### Run Mode ("Saga")

A roguelite challenge mode layered on the mod (branch `feature/run-mode`).
**Resuming work on it? Read `docs/superpowers/RESUME.md` first** — current
state, the build/tag/deploy loop, what is waiting on a play-test, and the
landmines. Design lives in `docs/superpowers/specs/`, the reasoning behind the
landmines in `docs/superpowers/2026-08-16-run-mode-build-notes.md`.
`End` opens the Run window: lobby outside a run, Heat HUD during one. While a
run is live, GM-mode commands are gated off (`InputManager.Gate`) and F1 shows
the Heat HUD instead of the cheat windows.

Key pieces: pure engines in `RunMode/` (`HeatModel`, `ChallengeEngine`,
`BoonEngine` — unit-tested via `Tests/run_tests.sh`), game-coupled code in
`RunMode/Unity/` (`RunService` orchestrator, `WorldModifiers` global-key
control, `RunStorage` persistence, `BoonEffects`, `RunWindow` UI,
`GameEvents`). The Patcher injects a second call — `Character.OnDeath` →
`GameEvents.CharacterDied` — used for kill challenges and death penalties.
Empowerment and heat ride Valheim's world-modifier global keys
(`ResourceRate`, `SkillGainRate`, `EnemyDamage`, …), which PERSIST with the
world save — all writes are guarded by world-identity checks and pre-run
originals stored in the run state. Live run state: per-character JSON next to
the config. Permanent record: `Player.m_customData` (`ICSYTW_saga_*` keys).

Two codebase facts that bite here: the legacy `CheatCommands` statics (ticked
by `CheatCommands.HandlePeriodic`) and the DI services are parallel,
UNSYNCED worlds — effects must ride the legacy pipeline, which is the one
actually ticked; and cached Unity objects need `ReferenceEquals`, not
`== null`, across destruction (destroyed objects compare equal to null).

## Unity Version Management

**Critical**: Unity version must match between development and deployed game. Valheim updates may change Unity versions.

### Check Unity version on Steam Deck
```bash
ssh deck@192.168.86.42
cd /home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data
strings globalgamemanagers | head -n1
```

### Download unstripped Unity assemblies
When Unity version changes, download from https://unity.bepinex.dev/:
- `https://unity.bepinex.dev/corlibs/[VERSION].zip`
- `https://unity.bepinex.dev/libraries/[VERSION].zip`

Extract both to the same folder and copy to:
1. `libraries/` folder (for development/linking)
2. Steam Deck `/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/` (for runtime)

Current Unity version: 6000.0.75 (Unity 6; 6000.0.58 from Valheim 0.221.6, 6000.0.61 since 0.221.12, 6000.0.75 since 1.0.12 — unchanged by 1.0.15)

## Deployment

The same `ICanShowYouTheWorld.dll` works on every platform (pure IL). Only the
patched `assembly_valheim.dll` is per-platform: each install's *own* original
must be patched, since the binaries differ between platforms even at the same
game version. All installs must be on the same game version — the deploy
scripts enforce this with a version guard (`Scripts/game_version.sh`, which
reads the version out of the assembly's IL).

**Activation** (all platforms): start Valheim. The mod loads itself at startup; a popup at
the main menu reports the version. Visiting Credits is no longer required (and does
nothing but log a line).

### Steam Deck (Linux)

**Target**: 192.168.86.42, `/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/`

```bash
Scripts/download.sh        # pull assembly, patch to patched/
Scripts/upload_hax.sh      # ICanShowYouTheWorld.dll
Scripts/upload_valheim.sh  # patched assembly_valheim.dll
```

### macOS (native build, Apple Silicon)

**Target**: `~/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app/Contents/Resources/Data/Managed/`

```bash
Scripts/download_macos.sh      # patch from the local install -> patched/macos/
Scripts/deploy_local.sh        # deploy + re-sign + verify
Scripts/deploy_local.sh --restore   # back to vanilla
```

**Critical — code signing**: every file inside a `.app` is covered by the
bundle's signature seal. Changing *or adding* one invalidates it, and Apple
Silicon then refuses to launch with *"valheim.app is damaged and can't be
opened"* — which prompts macOS to suggest trashing the app. `deploy_local.sh`
handles this: it re-signs ad-hoc after deploying (preserving entitlements and
the hardened-runtime flag, regenerating the designated requirement, which the
original Developer ID one would fail), verifies the result, and keeps backups
*outside* the bundle. Never hand-copy DLLs into `valheim.app`.

### Windows

Push from the Mac over SSH, mirroring the Deck flow. No signing constraints.

```bash
Scripts/download_windows.sh   # pull that machine's assembly, patch -> patched/windows/
Scripts/upload_windows.sh     # deploy both DLLs
```

Setup: see **[SSH_SETUP.md](SSH_SETUP.md)** for enabling SSH on every push
target (SteamOS and Windows 11). Short version: enable the optional **OpenSSH
Server** feature on Windows and set `WIN_HOST` in `Scripts/config.sh`. Two
gotchas, both handled/explained by `Scripts/win_common.sh`:

- For an **administrator** account, Windows OpenSSH ignores
  `~/.ssh/authorized_keys`; the key belongs in
  `C:\ProgramData\ssh\administrators_authorized_keys` with ACLs restricted to
  Administrators and SYSTEM.
- The default Steam path contains spaces, and `scp` passes remote paths
  through `cmd.exe`. Pointing `WIN_VALHEIM_MANAGED` at a space-free Steam
  library sidesteps the quoting entirely.

## Update Scenarios

### Simple scenario: Valheim patch only
Valheim updates via Steam, overwriting patched assembly_valheim.dll
→ Re-download assembly, re-patch with Patcher.exe, **rebuild the mod against
the new assembly**, re-upload both DLLs. Re-patching alone is never enough:
the mod DLL is compiled against one exact game assembly, and every update so
far has changed something it calls (1.0.12: five signatures gained a trailing
parameter, `PlayerProfile.m_playerStats` became an array, `Hoverable` gained
`GetHoverOffset()`). The deploy scripts and the Windows installer refuse a mod
whose version prefix does not match the game's version for this reason.

**1.0.15 (2026-09-19) was the first exception**: all 316 member references from the built
mod DLL into the game's assemblies still resolved, and the rebuild needed no source change.
Checking that first is cheap and worth doing — read every `MemberReference` out of the built
mod DLL with Cecil and look each one up in the new assemblies. It turns "what did they
break this time" into a list before a single compile. (The one rename spotted in 1.0.15,
`FejdStartup.PlayIntroCinematic` → `TryPlayIntroCinematic`, is not something the mod calls.)

### Complex scenario: Unity version change
Check https://valheim.fandom.com/wiki/Version_History for Unity version updates
→ Download new Unity binaries from bepinex, update libraries/, rebuild, re-patch, re-upload

### Feature addition to mod
Changes only to ICanShowYouTheWorld code
→ Rebuild, upload ICanShowYouTheWorld.dll only (no need to re-patch or upload assembly_valheim.dll)

## Directory Structure

- **ICanShowYouTheWorld/** - Main mod C# project (.csproj)
- **Patcher/** - IL patching tool using Mono.Cecil
- **Scripts/** - Build automation and deployment scripts
- **libraries/** - Unity and Valheim assemblies for linking
- **binaries/** - Unity version-specific binaries organized by version
- **Release/** - Release builds

## Important Notes

- **Security**: This is game modification software for personal use. The code patches game binaries and provides cheat functionality.
- **Cross-platform**: Development on macOS, deployment to Steam Deck (Linux). .NET IL is platform-agnostic.
- **Unity IMGUI**: UI is built using Unity's immediate-mode GUI (OnGUI pattern), not the newer UI Toolkit.
- **Keyboard shortcuts**: Primarily numpad-focused (Keypad 0-9, arrows, Home, Insert, NumLock, F1).
