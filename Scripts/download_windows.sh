#!/bin/bash
# Pull assembly_valheim.dll from the Windows machine and patch it into
# Patcher/bin/Debug/patched/windows/.
#
# Windows needs its own patched assembly: the per-platform game binaries
# differ even at the same game version. Requires the OpenSSH Server feature
# on the Windows box (see WIN_HOST in config.sh).

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"
source "$SCRIPT_DIR/win_common.sh"

WIN_ORG="$PATCHER_DIR/assembly_valheim_windows.dll.org"
WIN_PATCHED="$PATCHER_DIR/patched/windows/assembly_valheim.dll"

win_require_host

print_info "Downloading assembly_valheim.dll from $WIN_HOST..."
win_scp_from "assembly_valheim.dll" "$WIN_ORG"
print_success "Original staged: $WIN_ORG"

print_info "Patching (Windows)..."
# The patcher reads the injected call's symbols out of the mod DLL next to it
# (default ./ICanShowYouTheWorld.dll), so stage a fresh build first — otherwise
# it patches against a stale DLL, or fails outright on a symbol added since.
cp "$MOD_DLL" "$PATCHER_DIR/ICanShowYouTheWorld.dll"
cd "$PATCHER_DIR"
mono Patcher.exe "$WIN_ORG" "$WIN_PATCHED"
print_success "Patched assembly: $WIN_PATCHED"

VER="$("$SCRIPT_DIR/game_version.sh" "$WIN_ORG")"
print_info "Windows game version: $VER"
print_info "Deploy with: $SCRIPT_DIR/upload_windows.sh"
