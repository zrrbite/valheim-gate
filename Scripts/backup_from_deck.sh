#!/bin/bash
# Backup current mod and assembly from Steam Deck

set -e  # Exit on error

# Load configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

# Create backup directory with timestamp
BACKUP_DIR="$PROJECT_ROOT/backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

print_info "Backing up files from Steam Deck..."
print_info "Backup location: $BACKUP_DIR"

# Backup mod DLL
print_info "Downloading ICanShowYouTheWorld.dll..."
if scp "$DECK_HOST:$DECK_VALHEIM_MANAGED/ICanShowYouTheWorld.dll" "$BACKUP_DIR/"; then
    print_success "Backed up ICanShowYouTheWorld.dll"
else
    print_warning "Could not backup ICanShowYouTheWorld.dll (may not exist)"
fi

# Backup patched assembly
print_info "Downloading assembly_valheim.dll..."
if scp "$DECK_HOST:$DECK_VALHEIM_MANAGED/assembly_valheim.dll" "$BACKUP_DIR/"; then
    print_success "Backed up assembly_valheim.dll"
else
    print_error "Failed to backup assembly_valheim.dll"
    exit 1
fi

# Create info file
cat > "$BACKUP_DIR/backup_info.txt" <<EOF
Backup created: $(date)
Steam Deck: $DECK_HOST
Source: $DECK_VALHEIM_MANAGED

Files:
$(ls -lh "$BACKUP_DIR")
EOF

print_success "Backup complete!"
print_info "Backed up to: $BACKUP_DIR"
