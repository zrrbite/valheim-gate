#!/bin/bash
# Configuration file for Valheim mod deployment scripts
# Edit these values to match your setup

# Steam Deck connection
DECK_HOST="deck@192.168.86.42"
DECK_VALHEIM_MANAGED="/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed"

# Windows machine (needs the optional OpenSSH Server feature enabled).
# Use forward slashes; a path without spaces saves a lot of quoting pain, e.g.
# a library on D: rather than the default under "Program Files (x86)".
WIN_HOST="${WIN_HOST:-}"                    # e.g. martin@192.168.86.50
WIN_VALHEIM_MANAGED="${WIN_VALHEIM_MANAGED:-C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed}"

# Project paths (auto-detected relative to script location)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Build paths
MOD_DLL="$PROJECT_ROOT/ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll"
PATCHED_ASSEMBLY="$PROJECT_ROOT/Patcher/bin/Debug/patched/assembly_valheim.dll"
PATCHER_DIR="$PROJECT_ROOT/Patcher/bin/Debug"
LIBRARIES_DIR="$PROJECT_ROOT/libraries"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Check if file exists
check_file_exists() {
    if [ ! -f "$1" ]; then
        print_error "File not found: $1"
        return 1
    fi
    return 0
}

# Check if directory exists
check_dir_exists() {
    if [ ! -d "$1" ]; then
        print_error "Directory not found: $1"
        return 1
    fi
    return 0
}
