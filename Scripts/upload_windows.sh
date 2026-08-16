#!/bin/bash
# Deploy the mod to the Windows machine: patched assembly_valheim.dll +
# ICanShowYouTheWorld.dll, with the same game-version guard the other
# targets use.
#
# Usage: upload_windows.sh [--force]   (--force skips the version guard)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"
source "$SCRIPT_DIR/win_common.sh"

WIN_ORG="$PATCHER_DIR/assembly_valheim_windows.dll.org"
WIN_PATCHED="$PATCHER_DIR/patched/windows/assembly_valheim.dll"

win_require_host

if [ ! -f "$WIN_PATCHED" ]; then
    print_error "No Windows patched assembly — run download_windows.sh first."
    exit 1
fi
check_file_exists "$MOD_DLL" || exit 1

# Version guard against the original we patched from. Re-run
# download_windows.sh after a Steam update so this reflects the live install.
if [ -f "$WIN_ORG" ]; then
    ORG_VER="$("$SCRIPT_DIR/game_version.sh" "$WIN_ORG")"
    PATCHED_VER="$("$SCRIPT_DIR/game_version.sh" "$WIN_PATCHED")"
    if [ "$ORG_VER" != "$PATCHED_VER" ] && [ "$1" != "--force" ]; then
        print_error "Version mismatch: original $ORG_VER, patched $PATCHED_VER"
        exit 1
    fi
    print_info "Game version: $PATCHED_VER"
fi

print_info "Uploading to $WIN_HOST..."
win_scp_to "$WIN_PATCHED" "assembly_valheim.dll"
print_success "Uploaded patched assembly_valheim.dll"

win_scp_to "$MOD_DLL" "ICanShowYouTheWorld.dll"
print_success "Uploaded ICanShowYouTheWorld.dll"

print_warning "The game assembly has been replaced — Steam will overwrite it on update."
print_info "Start Valheim and open the Credits menu to activate the mod."
