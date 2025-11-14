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
/Applications/"Visual Studio".app/Contents/MacOS/vstool build -t:Build -c:"Debug" "Valheim.sln"
```

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

### Core Components

- **NotACheater.Run()** - Entry point that creates a persistent GameObject with DontDestroyOnLoad and attaches the CheatController and UIManager components
- **CheatController** (Cheat.cs) - Main MonoBehaviour that manages lifecycle and command registration
- **CheatCommands.cs** - Implementations of all cheat commands (teleportation, buffs, spawning, etc.)
- **UIManager.cs** - On-screen UI overlay using Unity's IMGUI system
- **InputManager.cs** - Keyboard input polling and command dispatch
- **CommandRegistry** - Global list of all keyboard bindings using CommandBinding pattern

### Command Pattern
Commands are registered using the `CommandBinding` class which encapsulates:
- Key binding (KeyCode)
- Description
- Action (callback)
- State (enabled/disabled)

InputManager polls keyboard in Update() and dispatches to appropriate commands.

### MonoBehaviour Lifecycle
- `Awake()` - Component initialization
- `Update()` - Per-frame input handling
- `OnGUI()` - Immediate-mode UI rendering (IMGUI)

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

Current Unity version: 2022.3.50

## Deployment

**Target**: Steam Deck at 192.168.86.42
**Path**: `/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/`

Files to deploy:
- `ICanShowYouTheWorld.dll` (from ICanShowYouTheWorld/bin/Debug/)
- `assembly_valheim.dll` (from Patcher/bin/Debug/patched/)

**Activation**: Start Valheim and navigate to Credits menu to initialize the mod.

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
