# Valheim Mod Deployment Scripts

Improved deployment scripts with error handling, validation, and colored output.

## Configuration

All scripts use **`config.sh`** for shared settings. Edit this file to change:
- Steam Deck IP address
- Remote paths
- Local project paths

## Scripts Overview

### 🚀 Quick Workflows

**`build_and_deploy.sh`** - Build and upload mod in one command
```bash
./build_and_deploy.sh
```
- Builds ICanShowYouTheWorld.dll
- Uploads to Steam Deck
- Shows success/failure with colors

**`dl_patch_valheim.sh`** - Update Valheim assembly (after game updates)
```bash
./dl_patch_valheim.sh
```
- Downloads assembly_valheim.dll from Steam Deck
- Patches it with Mono.Cecil
- Uploads patched version back
- Use this after Valheim updates via Steam

### 📤 Upload Scripts

**`upload_hax.sh`** - Upload mod DLL only
```bash
./upload_hax.sh
```
- Uploads ICanShowYouTheWorld.dll
- Use after rebuilding mod code

**`upload_valheim.sh`** - Upload patched assembly only
```bash
./upload_valheim.sh
```
- Uploads patched assembly_valheim.dll
- Use after re-patching

### 📥 Download & Patch

**`download.sh`** - Download and patch locally (no upload)
```bash
./download.sh
```
- Downloads assembly_valheim.dll + UnityEngine.UI.dll
- Patches assembly locally
- Does NOT upload - for local testing

### 💾 Backup

**`backup_from_deck.sh`** - Backup current files from Steam Deck
```bash
./backup_from_deck.sh
```
- Downloads current mod + assembly from Steam Deck
- Saves to `backups/YYYYMMDD_HHMMSS/`
- Creates backup info file

### 🔢 Version Management

**`bump_version.sh`** - Bump mod version
```bash
./bump_version.sh <valheim_version> <mod_version> [create_tag]
```
- Updates `Version.cs` with format: `<valheim_version>-<mod_version>`
- Optionally creates git tag
- Example: `./bump_version.sh 0.225.5 3` → `"0.225.5-3"`

Arguments:
- `valheim_version` - Current Valheim game version (e.g., 0.225.5)
- `mod_version` - Your mod version number (e.g., 1, 2, 3)
- `create_tag` - Create git tag (yes/no, default: yes)

## Features

All scripts include:
- ✅ **Error handling** - Stops on first error
- ✅ **Validation** - Checks files exist before uploading
- ✅ **Colored output** - Green ✓ success, Red ✗ errors, Blue ℹ info
- ✅ **File size display** - Shows what's being uploaded
- ✅ **Clear messages** - Helpful error messages and next steps
- ✅ **Work from anywhere** - No need to cd into Scripts/ first

## Common Workflows

### After modifying mod code
```bash
./build_and_deploy.sh
```

### After Valheim game update
```bash
./dl_patch_valheim.sh
# Then rebuild and deploy mod:
cp ../Patcher/bin/Debug/patched/assembly_valheim.dll ../libraries/
./build_and_deploy.sh
```

### Releasing a new version
```bash
# Bump version (creates git tag)
./bump_version.sh 0.225.5 3

# Build and deploy
./build_and_deploy.sh

# Push tag to remote
git push origin 0.225.5-3
```

### Just upload mod (already built)
```bash
./upload_hax.sh
```

### Backup before major changes
```bash
./backup_from_deck.sh
```

## Troubleshooting

### Permission denied (SSH)
Set up SSH keys for passwordless access:
```bash
ssh-keygen -t rsa -b 4096
ssh-copy-id deck@192.168.86.42
```

### File not found errors
- Make sure you've built the project first
- Check paths in `config.sh`

### Build failed
- Check Visual Studio is installed at the expected path
- Check `/tmp/valheim_build.log` for details

### Case sensitivity issues
All paths now use correct capitalization (Debug, not debug)

## Configuration Variables

Edit `config.sh` to customize:

```bash
DECK_HOST="deck@192.168.86.42"                    # Steam Deck SSH address
DECK_VALHEIM_MANAGED="/home/deck/.local/share..." # Remote Valheim path
```

## Old Scripts

Old scripts (without improvements) have been replaced. If you need them, check git history.
