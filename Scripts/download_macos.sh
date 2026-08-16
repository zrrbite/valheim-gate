#!/bin/bash
# macOS counterpart to download.sh: take assembly_valheim.dll from the LOCAL
# Steam install and patch it into Patcher/bin/Debug/patched/macos/.
# (The Mac assembly differs byte-wise from the Deck's even at the same game
# version, so each platform gets patched from its own original.)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

MAC_MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app/Contents/Resources/Data/Managed"
MAC_ORG="$PATCHER_DIR/assembly_valheim_macos.dll.org"
MAC_PATCHED="$PATCHER_DIR/patched/macos/assembly_valheim.dll"

check_dir_exists "$MAC_MANAGED" || exit 1

print_info "Copying assembly_valheim.dll from local install..."
cp "$MAC_MANAGED/assembly_valheim.dll" "$MAC_ORG"
print_success "Original staged: $MAC_ORG"

print_info "Patching (macOS)..."
cd "$PATCHER_DIR"
mono Patcher.exe "$MAC_ORG" "$MAC_PATCHED"
print_success "Patched assembly: $MAC_PATCHED"
print_info "Deploy with: $SCRIPT_DIR/deploy_local.sh"
