#!/bin/bash
# Shared helpers for the Windows deploy scripts.
#
# Quoting note: scp hands the remote path to the remote shell, and Windows
# OpenSSH defaults to cmd.exe. A path containing spaces (the default Steam
# location under "Program Files (x86)") therefore has to reach cmd.exe still
# quoted, hence the embedded double quotes below. Pointing
# WIN_VALHEIM_MANAGED at a space-free Steam library avoids the whole problem.

win_require_host() {
    if [ -z "$WIN_HOST" ]; then
        print_error "WIN_HOST is not set."
        print_info "Set it in Scripts/config.sh (or export it), e.g. martin@192.168.86.50"
        print_info "On the Windows machine, enable the OpenSSH Server feature:"
        print_info "  Settings > System > Optional features > Add > OpenSSH Server"
        print_info "  then: Start-Service sshd; Set-Service -Name sshd -StartupType Automatic"
        exit 1
    fi
    if ! ssh -o ConnectTimeout=8 -o BatchMode=yes "$WIN_HOST" "echo ok" >/dev/null 2>&1; then
        print_error "Cannot reach $WIN_HOST over SSH (key auth)."
        print_info "For an ADMIN account, Windows OpenSSH ignores ~/.ssh/authorized_keys."
        print_info "The key must go in C:\\ProgramData\\ssh\\administrators_authorized_keys,"
        print_info "readable only by Administrators and SYSTEM:"
        print_info '  icacls administrators_authorized_keys /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F"'
        exit 1
    fi
}

# win_scp_from <filename-in-Managed> <local-destination>
win_scp_from() {
    scp -q "$WIN_HOST:\"$WIN_VALHEIM_MANAGED/$1\"" "$2"
}

# win_scp_to <local-file> <filename-in-Managed>
win_scp_to() {
    scp -q "$1" "$WIN_HOST:\"$WIN_VALHEIM_MANAGED/$2\""
}
