#!/bin/bash
# Upload ICanShowYouTheWorld.dll (mod) to Steam Deck

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

print_info "Uploading mod DLL to Steam Deck..."

# Validate file exists
if ! check_file_exists "$MOD_DLL"; then
    print_error "Mod DLL not found. Have you built the project?"
    print_info "Run: /Applications/\"Visual Studio\".app/Contents/MacOS/vstool build -t:Build -c:\"Debug\" \"$PROJECT_ROOT/Valheim.sln\""
    exit 1
fi

# Show file info
MOD_SIZE=$(du -h "$MOD_DLL" | cut -f1)
print_info "File: ICanShowYouTheWorld.dll ($MOD_SIZE)"
print_info "Destination: $DECK_HOST:$DECK_VALHEIM_MANAGED"

# Upload
if scp "$MOD_DLL" "$DECK_HOST:$DECK_VALHEIM_MANAGED/"; then
    print_success "Mod DLL uploaded successfully!"
    print_info "Restart Valheim and go to Credits menu to activate the mod"
else
    print_error "Upload failed!"
    exit 1
fi
