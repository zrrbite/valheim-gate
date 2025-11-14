#!/bin/bash
# Bump mod version with format: <valheim_version>-<mod_version>
# Example: ./bump_version.sh 0.225.5 3  => "0.225.5-3"

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

# ===== Parse Arguments =====
GAME_VERSION="$1"
MOD_VERSION="$2"
CREATE_TAG="${3:-yes}"  # Default: yes

# ===== Usage =====
usage() {
    echo "Usage: $0 <valheim_version> <mod_version> [create_tag]"
    echo ""
    echo "Examples:"
    echo "  $0 0.225.5 3           # Creates version 0.225.5-3 and git tag"
    echo "  $0 0.225.5 3 no        # Creates version 0.225.5-3 without git tag"
    echo ""
    echo "Arguments:"
    echo "  valheim_version  - Valheim game version (e.g., 0.225.5)"
    echo "  mod_version      - Mod version number (e.g., 3)"
    echo "  create_tag       - Create git tag (yes/no, default: yes)"
    exit 1
}

# ===== Validate Arguments =====
if [ -z "$GAME_VERSION" ] || [ -z "$MOD_VERSION" ]; then
    print_error "Missing arguments!"
    usage
fi

# Validate version format (basic check)
if ! [[ "$GAME_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    print_error "Invalid game version format: $GAME_VERSION"
    print_info "Expected format: X.Y.Z (e.g., 0.225.5)"
    exit 1
fi

if ! [[ "$MOD_VERSION" =~ ^[0-9]+$ ]]; then
    print_error "Invalid mod version: $MOD_VERSION"
    print_info "Expected: a number (e.g., 1, 2, 3)"
    exit 1
fi

# ===== Generate Full Version =====
FULL_VERSION="${GAME_VERSION}-${MOD_VERSION}"

print_info "=========================================="
print_info "Bumping Mod Version"
print_info "=========================================="
echo ""
print_info "Game Version: $GAME_VERSION"
print_info "Mod Version:  $MOD_VERSION"
print_info "Full Version: $FULL_VERSION"
echo ""

# ===== Check if Version.cs exists =====
VERSION_FILE="$PROJECT_ROOT/ICanShowYouTheWorld/Assets/Version.cs"

if [ ! -f "$VERSION_FILE" ]; then
    print_warning "Version.cs not found, will create it"
    mkdir -p "$PROJECT_ROOT/ICanShowYouTheWorld/Assets"
fi

# ===== Get Current Version (if exists) =====
CURRENT_VERSION=""
if [ -f "$VERSION_FILE" ]; then
    CURRENT_VERSION=$(grep 'VERSION = "' "$VERSION_FILE" | sed 's/.*VERSION = "\([^"]*\)".*/\1/')
    if [ -n "$CURRENT_VERSION" ]; then
        print_info "Current Version: $CURRENT_VERSION"
    fi
fi

# ===== Update Version.cs =====
print_info "Updating Version.cs..."

cat > "$VERSION_FILE" <<EOF
using System;
namespace ICanShowYouTheWorld
{
    public static class ModVersion
    {
        public const string VERSION = "$FULL_VERSION";
    }
}
EOF

print_success "Version.cs updated!"

# ===== Verify the file was written correctly =====
if grep -q "$FULL_VERSION" "$VERSION_FILE"; then
    print_success "Verified: Version.cs contains $FULL_VERSION"
else
    print_error "Failed to update Version.cs!"
    exit 1
fi

# ===== Create Git Tag (Optional) =====
if [ "$CREATE_TAG" = "yes" ]; then
    print_info "Creating git tag: $FULL_VERSION"

    # Check if tag already exists
    if git rev-parse "$FULL_VERSION" >/dev/null 2>&1; then
        print_warning "Git tag $FULL_VERSION already exists"
        read -p "Delete and recreate? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            git tag -d "$FULL_VERSION"
            print_info "Deleted old tag"
        else
            print_info "Skipping tag creation"
            CREATE_TAG="no"
        fi
    fi

    if [ "$CREATE_TAG" = "yes" ]; then
        # Add Version.cs to git
        git add "$VERSION_FILE"

        # Create tag
        git tag -a "$FULL_VERSION" -m "Release $FULL_VERSION"
        print_success "Git tag created: $FULL_VERSION"
        print_info "Push with: git push origin $FULL_VERSION"
    fi
else
    print_info "Skipping git tag creation"
fi

echo ""
print_success "=========================================="
print_success "Version Bump Complete!"
print_success "=========================================="
echo ""
print_info "Version: $FULL_VERSION"
print_info "File:    $VERSION_FILE"

if [ "$CREATE_TAG" = "yes" ]; then
    print_info "Git Tag: $FULL_VERSION"
fi

echo ""
print_info "Next steps:"
print_info "  1. Verify Version.cs: cat $VERSION_FILE"
print_info "  2. Rebuild: ./build_and_deploy.sh"
if [ "$CREATE_TAG" = "yes" ]; then
    print_info "  3. Push tag: git push origin $FULL_VERSION"
fi
echo ""
