#!/bin/bash
# Print the Valheim game version stored in an assembly_valheim.dll
# (works on originals and patched copies alike).
# Usage: game_version.sh <path-to-assembly_valheim.dll>

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CECIL="$PROJECT_ROOT/Patcher/bin/Debug/Mono.Cecil.dll"
SRC="$SCRIPT_DIR/GetGameVersion.cs"
EXE="${TMPDIR:-/tmp}/GetGameVersion.exe"

[ -n "$1" ] && [ -f "$1" ] || { echo "usage: $0 <assembly_valheim.dll>" >&2; exit 2; }
[ -f "$CECIL" ] || { echo "Mono.Cecil.dll not found — build the Patcher first" >&2; exit 2; }

# Recompile if missing or stale
if [ ! -f "$EXE" ] || [ "$SRC" -nt "$EXE" ]; then
    mcs -r:"$CECIL" -out:"$EXE" "$SRC" >/dev/null
fi

MONO_PATH="$PROJECT_ROOT/Patcher/bin/Debug" mono "$EXE" "$1"
