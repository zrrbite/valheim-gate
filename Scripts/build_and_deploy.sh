#!/bin/bash
# Build the mod and deploy it to Steam Deck

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

echo ""
print_info "=========================================="
print_info "Build and Deploy Workflow"
print_info "=========================================="
echo ""

# Step 1: Build
print_info "STEP 1: Building solution..."
cd "$PROJECT_ROOT"

BUILD_CMD="/Applications/Visual Studio.app/Contents/MacOS/vstool"
if [ ! -f "$BUILD_CMD" ]; then
    print_error "Visual Studio build tool not found at: $BUILD_CMD"
    print_info "Building manually with msbuild..."
    BUILD_CMD="msbuild"
fi

if "$BUILD_CMD" build -t:Build -c:"Debug" "Valheim.sln" > /tmp/valheim_build.log 2>&1; then
    print_success "Build succeeded!"
else
    print_error "Build failed! Check /tmp/valheim_build.log for details"
    tail -20 /tmp/valheim_build.log
    exit 1
fi

# Verify DLL was created
if ! check_file_exists "$MOD_DLL"; then
    print_error "Build succeeded but DLL not found at: $MOD_DLL"
    exit 1
fi

MOD_SIZE=$(du -h "$MOD_DLL" | cut -f1)
print_info "Built: ICanShowYouTheWorld.dll ($MOD_SIZE)"

echo ""

# Step 2: Upload
print_info "STEP 2: Uploading to Steam Deck..."
print_info "Destination: $DECK_HOST:$DECK_VALHEIM_MANAGED"

if scp "$MOD_DLL" "$DECK_HOST:$DECK_VALHEIM_MANAGED/"; then
    print_success "Mod uploaded successfully!"
else
    print_error "Upload failed!"
    exit 1
fi

echo ""
print_success "=========================================="
print_success "Deploy complete!"
print_success "=========================================="
echo ""
print_info "Next steps:"
print_info "  1. Restart Valheim on Steam Deck"
print_info "  2. Go to Credits menu to activate the mod"
echo ""
