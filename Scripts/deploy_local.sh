#!/bin/bash
# Deploy the mod to the LOCAL macOS Valheim install (Steam).
#
# Copies the patched assembly_valheim.dll + ICanShowYouTheWorld.dll into the
# app bundle's Managed folder, then RE-SIGNS the bundle: any change inside a
# signed .app breaks its code-signature seal, and Apple Silicon refuses to
# launch it ("valheim.app is damaged"). Backups live outside the bundle for
# the same reason — an extra file inside it also breaks the seal.
#
# Usage:
#   deploy_local.sh            deploy mod, re-sign, verify
#   deploy_local.sh --force    skip the game-version guard
#   deploy_local.sh --restore  put the vanilla assembly back, re-sign

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

MAC_VALHEIM_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Valheim"
APP="$MAC_VALHEIM_DIR/valheim.app"
MANAGED="$APP/Contents/Resources/Data/Managed"
# Backups sit next to the bundle, never inside it
VANILLA="$MAC_VALHEIM_DIR/assembly_valheim.dll.vanilla"

check_dir_exists "$MANAGED" || { print_error "Valheim not installed at $APP"; exit 1; }

# Re-sign the bundle ad-hoc and confirm macOS accepts it. Without a valid
# signature Apple Silicon will not launch the app at all.
resign_bundle() {
    print_info "Re-signing bundle (ad-hoc)..."
    xattr -dr com.apple.quarantine "$APP" 2>/dev/null || true
    # Keep the original entitlements/hardened-runtime flags; a bare ad-hoc
    # signature would drop them. Deliberately NOT preserving `requirements`:
    # the original designated requirement names Coffee Stain's certificate,
    # which an ad-hoc signature can never satisfy.
    SIGN_OPTS=(--force --sign - --preserve-metadata=entitlements,flags,runtime)
    if ! codesign "${SIGN_OPTS[@]}" "$APP" 2>/dev/null; then
        print_warning "Plain re-sign failed, retrying with --deep"
        codesign --force --deep --sign - "$APP"
    fi
    if codesign --verify "$APP" 2>/dev/null; then
        print_success "Bundle signature valid"
    else
        print_error "Signature verification FAILED — do not launch; run --restore"
        exit 1
    fi
}

if [ "$1" = "--restore" ]; then
    check_file_exists "$VANILLA" || { print_error "No vanilla backup at $VANILLA"; exit 1; }
    cp "$VANILLA" "$MANAGED/assembly_valheim.dll"
    rm -f "$MANAGED/ICanShowYouTheWorld.dll"
    print_success "Restored vanilla assembly and removed the mod DLL"
    resign_bundle
    print_info "Install is back to vanilla."
    exit 0
fi

PATCHED_ASSEMBLY="$PATCHER_DIR/patched/macos/assembly_valheim.dll"
if [ ! -f "$PATCHED_ASSEMBLY" ]; then
    print_error "No macOS patched assembly — run download_macos.sh first."
    exit 1
fi
check_file_exists "$MOD_DLL" || exit 1

# One-time vanilla backup, taken before the first modification
if [ ! -f "$VANILLA" ]; then
    cp "$MANAGED/assembly_valheim.dll" "$VANILLA"
    print_info "Vanilla backup: $VANILLA"
fi

# Version guard: compare against the vanilla copy, since the installed file
# may already be a patched one from an earlier deploy.
INSTALLED_VER="$("$SCRIPT_DIR/game_version.sh" "$VANILLA")"
PATCHED_VER="$("$SCRIPT_DIR/game_version.sh" "$PATCHED_ASSEMBLY")"
if [ "$INSTALLED_VER" != "$PATCHED_VER" ] && [ "$1" != "--force" ]; then
    print_error "Version mismatch: install is $INSTALLED_VER, patched assembly is $PATCHED_VER"
    print_info "Re-run download_macos.sh against this install, or pass --force."
    exit 1
fi
print_success "Version check: install $INSTALLED_VER == patched $PATCHED_VER"

# Right version is not the same as right contents — refuse to deploy an
# assembly whose injections did not land.
check_injections "$PATCHED_ASSEMBLY" || exit 1

cp "$PATCHED_ASSEMBLY" "$MANAGED/assembly_valheim.dll"
print_success "Deployed patched assembly_valheim.dll"

cp "$MOD_DLL" "$MANAGED/ICanShowYouTheWorld.dll"
print_success "Deployed ICanShowYouTheWorld.dll"

resign_bundle

print_info "Launch Valheim and open the Credits menu to activate the mod."
print_info "Roll back any time with: $0 --restore"
