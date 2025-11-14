#!/bin/bash
# Complete workflow: Download, patch, and upload Valheim assembly
# This is the most common operation when Valheim updates

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

echo ""
print_info "=========================================="
print_info "Valheim Assembly Update Workflow"
print_info "=========================================="
echo ""

# Step 1: Download
print_info "STEP 1: Downloading assemblies from Steam Deck..."
mkdir -p "$LIBRARIES_DIR"
mkdir -p "$PATCHER_DIR"

if scp "$DECK_HOST:$DECK_VALHEIM_MANAGED/assembly_valheim.dll" "$LIBRARIES_DIR/"; then
    print_success "Downloaded assembly_valheim.dll"
else
    print_error "Failed to download assembly_valheim.dll"
    exit 1
fi

if scp "$DECK_HOST:$DECK_VALHEIM_MANAGED/UnityEngine.UI.dll" "$LIBRARIES_DIR/"; then
    print_success "Downloaded UnityEngine.UI.dll"
else
    print_error "Failed to download UnityEngine.UI.dll"
    exit 1
fi

echo ""

# Step 2: Backup and Patch
print_info "STEP 2: Patching assembly..."
cp "$LIBRARIES_DIR/assembly_valheim.dll" "$PATCHER_DIR/assembly_valheim.dll.org"
print_success "Original assembly backed up"

cd "$PATCHER_DIR"
if mono Patcher.exe; then
    print_success "Assembly patched successfully!"
else
    print_error "Patching failed!"
    exit 1
fi

echo ""

# Step 3: Upload
print_info "STEP 3: Uploading patched assembly to Steam Deck..."
if scp "$PATCHED_ASSEMBLY" "$DECK_HOST:$DECK_VALHEIM_MANAGED/"; then
    print_success "Patched assembly uploaded!"
else
    print_error "Upload failed!"
    exit 1
fi

echo ""
print_success "=========================================="
print_success "All done! Valheim assembly updated."
print_success "=========================================="
echo ""
print_info "Next steps:"
print_info "  1. Copy patched assembly to libraries for linking:"
print_info "     cp $PATCHED_ASSEMBLY $LIBRARIES_DIR/"
print_info "  2. Rebuild your mod if needed"
print_info "  3. Upload mod: $SCRIPT_DIR/upload_hax.sh"
echo ""
