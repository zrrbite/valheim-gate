# ICanShowYouTheWorld Configuration Guide

## Overview

The mod now supports user configuration through a JSON file. You can customize various gameplay parameters, UI settings, and debug options without recompiling the mod.

## Configuration File Location

The configuration file is automatically created at:
- **Steam Deck/Linux**: `/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/../ICanShowYouTheWorld.json`
- **Windows**: `%APPDATA%\..\LocalLow\IronGate\Valheim\ICanShowYouTheWorld.json`
- **macOS**: `~/Library/Application Support/IronGate/Valheim/ICanShowYouTheWorld.json`

The exact path depends on Unity's `Application.persistentDataPath` for the Valheim game.

## How to Configure

1. **Start Valheim once** with the mod installed - this creates the default config file
2. **Exit Valheim**
3. **Edit** `ICanShowYouTheWorld.json` with any text editor
4. **Save** your changes
5. **Restart Valheim** - your settings will be loaded

## Configuration Options

### Pet System
```json
"petBuffRadius": 10.0          // How far from player to buff pets (in meters)
"petBuffMultiplier": 1.2       // Damage multiplier for pet buffs (1.2 = 20% boost)
"petHealthMultiplier": 1.5     // Health multiplier for pets (1.5 = 50% more HP)
```

### Combat System
```json
"defaultDamageCounter": 1      // Starting damage multiplier
"damageCounterIncrement": 1    // How much damage changes per arrow key press
"speedIncrement": 0.5          // How much speed changes per arrow key press
"defaultRunSpeed": 7.0         // Base run speed for player
```

### AoE & Buff System
```json
"defaultAoePower": 50.0        // Starting AoE power/healing amount
"aoePowerIncrement": 10.0      // How much AoE power changes per +/- key
"renewalTickInterval": 1.0     // How often HoT (heal over time) ticks (seconds)
"guardianGiftRadius": 20.0     // Guardian Gift buff radius (meters)
"cloakOfFlamesRadius": 8.0     // Cloak of Flames damage radius (meters)
"cloakOfFlamesDamage": 20.0    // Cloak of Flames damage per tick
"aoeRenewalRadius": 20.0       // AoE Renewal healing radius (meters)
```

### Teleport System
```json
"teleportSafeFallDistance": 5.0  // Max safe fall distance when teleporting
"teleportRequireGodMode": false  // If true, teleport only works with god mode on
```

### Cleanup System
```json
"trashCleanupRadius": 1.0      // Radius for trash/drop cleanup (meters)
```

### UI Settings
```json
"trackingWindowWidth": 300.0   // Tracking window width (pixels)
"trackingWindowHeight": 250.0  // Tracking window height (pixels)
"modesWindowWidth": 325.0      // Modes window width (pixels)
"modesWindowHeight": 550.0     // Modes window height (pixels)
"petsWindowWidth": 200.0       // Pets window width (pixels)
"petsWindowHeight": 250.0      // Pets window height (pixels)
"trackingRange": 100.0         // How far to track enemies (meters)
"petDisplayRange": 50.0        // How far to show pets in UI (meters)
```

### Debug & System
```json
"enableDebugMode": false       // Enable debug features
"enableDebugLogs": false       // Enable verbose logging
"configVersion": "1.0"         // Config file version (for future migrations)
```

## Example Configurations

### Hardcore Mode (Reduced Power)
```json
{
    "petBuffMultiplier": 1.1,
    "petHealthMultiplier": 1.2,
    "defaultAoePower": 25.0,
    "aoePowerIncrement": 5.0,
    "cloakOfFlamesDamage": 10.0,
    "teleportRequireGodMode": true
}
```

### Easy Mode (Increased Power)
```json
{
    "petBuffMultiplier": 1.5,
    "petHealthMultiplier": 2.0,
    "defaultAoePower": 100.0,
    "aoePowerIncrement": 20.0,
    "cloakOfFlamesDamage": 40.0,
    "speedIncrement": 1.0
}
```

### Minimal UI (Smaller Windows)
```json
{
    "trackingWindowWidth": 200.0,
    "trackingWindowHeight": 150.0,
    "modesWindowWidth": 250.0,
    "modesWindowHeight": 400.0,
    "petsWindowWidth": 150.0,
    "petsWindowHeight": 150.0
}
```

## Troubleshooting

### Config file not loading?
- Ensure the JSON syntax is valid (use a JSON validator: https://jsonlint.com/)
- Check that you saved the file after editing
- Look for error messages in the Unity log/console

### Want to reset to defaults?
- Delete the `ICanShowYouTheWorld.json` file
- Restart Valheim - a fresh config with defaults will be created

### Where are the Unity logs?
- **Steam Deck/Linux**: `~/.config/unity3d/IronGate/Valheim/Player.log`
- **Windows**: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\Player.log`
- **macOS**: `~/Library/Logs/IronGate/Valheim/Player.log`

## Future Configuration Options

As the mod architecture evolves, more options will become configurable:
- Key bindings
- Visual effect colors
- Sound settings
- Advanced gameplay tweaks

## Notes

- **All changes require a game restart** - the config is loaded once at startup
- **Invalid values will be ignored** and defaults will be used
- **Missing values will use defaults** - you only need to specify values you want to change
- Configuration is version-specific (`configVersion`) - future updates may add new settings
