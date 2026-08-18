#!/bin/bash
# Upload patched assembly_valheim.dll to Steam Deck

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

print_info "Uploading patched Valheim assembly to Steam Deck..."

# Validate file exists
if ! check_file_exists "$PATCHED_ASSEMBLY"; then
    print_error "Patched assembly not found. Have you run the patcher?"
    print_info "Run: cd $PATCHER_DIR && mono Patcher.exe"
    exit 1
fi

# Both injections must be present — the patcher's exit code alone has already
# let a hookless assembly through once.
check_injections "$PATCHED_ASSEMBLY" || exit 1

# Show file info
ASSEMBLY_SIZE=$(du -h "$PATCHED_ASSEMBLY" | cut -f1)
print_info "File: assembly_valheim.dll ($ASSEMBLY_SIZE)"
print_info "Destination: $DECK_HOST:$DECK_VALHEIM_MANAGED"

# Upload
if scp "$PATCHED_ASSEMBLY" "$DECK_HOST:$DECK_VALHEIM_MANAGED/"; then
    print_success "Patched assembly uploaded successfully!"
    print_warning "The game assembly has been replaced. Make sure you have a backup!"
else
    print_error "Upload failed!"
    exit 1
fi
