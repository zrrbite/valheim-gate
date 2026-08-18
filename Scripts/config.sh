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

# Verify a patched assembly actually carries BOTH injections before it ships.
#
# The patcher's exit code is not proof: a stale patcher/ folder happily injects
# the entry point and silently omits the death hook, producing an assembly that
# loads the mod but never fires kill events — Run Mode's kill challenges are
# then dead, with only a subtle in-game notice to say so. Both names appear in
# the assembly's metadata string heap, so a plain string scan settles it.
#
# Checked separately, because the two failure modes need different advice:
#   NotACheater   — the entry point injected into FejdStartup.OnCredits()
#   CharacterDied — the Run Mode death hook injected into Character.OnDeath()
check_injections() {
    local dll="$1"

    if ! strings -a "$dll" | grep -q 'NotACheater'; then
        print_error "Patched assembly is missing the mod entry point: $dll"
        print_info "Re-run the patch step — this assembly will not load the mod at all."
        return 1
    fi

    if ! strings -a "$dll" | grep -q 'CharacterDied'; then
        print_error "Patched assembly is missing the Character.OnDeath hook: $dll"
        print_info "Your patcher build is stale — rebuild the Patcher and re-run the patch step."
        print_info "Shipping this would leave Run Mode's kill challenges permanently at 0."
        return 1
    fi

    print_success "Verified both injections present (entry point + death hook)"
    return 0
}
