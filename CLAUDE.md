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
# (Use Patcher/Scripts/copy.sh or manual copy)

# 4. Rebuild ICanShowYouTheWorld against new assembly
# (Use vstool build command above)

# 5. Upload to Steam Deck
Scripts/upload_hax.sh      # Uploads ICanShowYouTheWorld.dll
Scripts/upload_valheim.sh  # Uploads patched assembly_valheim.dll
```

### Version management
```bash
Scripts/setversion.sh  # Generates Version.cs from git tags
```

### Unity version detection
```bash
Scripts/get_unity_version.sh  # Detects Unity version from game files
```

## Architecture

### IL Patching Entry Point
The Patcher tool modifies `assembly_valheim.dll` to inject a call to `ICanShowYouTheWorld.NotACheater.Run()` at the beginning of `FejdStartup.OnCredits()`. This means the mod initializes when the player navigates to the Credits menu in-game.

The patcher also modifies `Minimap.m_pins` from private to public static to allow mod access.

### Service-Based Architecture (New)

The mod now uses a modern service-based architecture with dependency injection:

**Initialization Flow:**
```
FejdStartup.OnCredits()
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

Current Unity version: 6000.0.58 (Unity 6, since Valheim 0.221.6)

## Deployment

The same `ICanShowYouTheWorld.dll` works on every platform (pure IL). Only the
patched `assembly_valheim.dll` is per-platform: each install's *own* original
must be patched, since the binaries differ between platforms even at the same
game version. All installs must be on the same game version — the deploy
scripts enforce this with a version guard (`Scripts/game_version.sh`, which
reads the version out of the assembly's IL).

**Activation** (all platforms): start Valheim and navigate to the Credits menu.

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
→ Re-download assembly, re-patch with Patcher.exe, re-upload both DLLs

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
