#!/bin/bash
# Download Valheim assemblies from Steam Deck and patch them locally

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

print_info "Downloading Valheim assemblies from Steam Deck..."

# Create directories if they don't exist
mkdir -p "$LIBRARIES_DIR"
mkdir -p "$PATCHER_DIR"

# Download assembly_valheim.dll
print_info "Downloading assembly_valheim.dll..."
if scp "$DECK_HOST:$DECK_VALHEIM_MANAGED/assembly_valheim.dll" "$LIBRARIES_DIR/"; then
    print_success "Downloaded assembly_valheim.dll"
else
    print_error "Failed to download assembly_valheim.dll"
    exit 1
fi

# Download UnityEngine.UI.dll
print_info "Downloading UnityEngine.UI.dll..."
if scp "$DECK_HOST:$DECK_VALHEIM_MANAGED/UnityEngine.UI.dll" "$LIBRARIES_DIR/"; then
    print_success "Downloaded UnityEngine.UI.dll"
else
    print_error "Failed to download UnityEngine.UI.dll"
    exit 1
fi

# Backup original assembly before patching
print_info "Backing up original assembly..."
cp "$LIBRARIES_DIR/assembly_valheim.dll" "$PATCHER_DIR/assembly_valheim.dll.org"
print_success "Backup created: assembly_valheim.dll.org"

# Patch the assembly
print_info "Patching assembly_valheim.dll..."
cd "$PATCHER_DIR"

if mono Patcher.exe; then
    print_success "Assembly patched successfully!"
    print_info "Patched assembly available at: $PATCHED_ASSEMBLY"
    print_info ""
    print_info "Next steps:"
    print_info "  1. Copy patched assembly to libraries: cp $PATCHED_ASSEMBLY $LIBRARIES_DIR/"
    print_info "  2. Rebuild your mod against the new assembly"
    print_info "  3. Upload: $SCRIPT_DIR/upload_valheim.sh"
else
    print_error "Patching failed!"
    exit 1
fi
