#!/bin/bash
# Deploy the mod to the LOCAL macOS Valheim install (Steam).
# Copies the patched assembly_valheim.dll + ICanShowYouTheWorld.dll into the
# app bundle's Managed folder, with a version guard so a stale patched
# assembly never overwrites a newer game.
#
# Usage: deploy_local.sh [--force]   (--force skips the version guard)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

MAC_VALHEIM_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Valheim"

# Locate the Managed folder inside the app bundle (layout differs from Linux)
MANAGED="$(find "$MAC_VALHEIM_DIR" -maxdepth 5 -type d -name Managed 2>/dev/null | head -1)"
if [ -z "$MANAGED" ]; then
    print_error "No Managed folder found under $MAC_VALHEIM_DIR — is Valheim installed?"
    exit 1
fi
print_info "Target: $MANAGED"

# macOS gets its own patched assembly (byte-different original vs the Deck)
PATCHED_ASSEMBLY="$PATCHER_DIR/patched/macos/assembly_valheim.dll"
if [ ! -f "$PATCHED_ASSEMBLY" ]; then
    print_error "No macOS patched assembly — run download_macos.sh first."
    exit 1
fi
check_file_exists "$MOD_DLL" || exit 1

# Version guard: the patched assembly must match the installed game version
INSTALLED_VER="$("$SCRIPT_DIR/game_version.sh" "$MANAGED/assembly_valheim.dll")"
PATCHED_VER="$("$SCRIPT_DIR/game_version.sh" "$PATCHED_ASSEMBLY")"
if [ "$INSTALLED_VER" != "$PATCHED_VER" ] && [ "$1" != "--force" ]; then
    print_error "Version mismatch: install is $INSTALLED_VER, patched assembly is $PATCHED_VER"
    print_info "Re-run the patch flow against this install, or pass --force to override."
    exit 1
fi
print_success "Version check: install $INSTALLED_VER == patched $PATCHED_VER"

# Keep a one-time vanilla backup of the original assembly
if [ ! -f "$MANAGED/assembly_valheim.dll.vanilla" ]; then
    cp "$MANAGED/assembly_valheim.dll" "$MANAGED/assembly_valheim.dll.vanilla"
    print_info "Vanilla backup created: assembly_valheim.dll.vanilla"
fi

cp "$PATCHED_ASSEMBLY" "$MANAGED/assembly_valheim.dll"
print_success "Deployed patched assembly_valheim.dll"

cp "$MOD_DLL" "$MANAGED/ICanShowYouTheWorld.dll"
print_success "Deployed ICanShowYouTheWorld.dll"

print_info "Launch Valheim and open the Credits menu to activate the mod."
