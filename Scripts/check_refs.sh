#!/usr/bin/env bash
#
# After a Valheim update, BEFORE rebuilding: does every type and member the built mod uses still
# exist in the new game assemblies? Prints the count and any that no longer resolve.
#
#   bash Scripts/check_refs.sh                     # against this Mac's install
#   bash Scripts/check_refs.sh <Managed folder>    # against any folder of game DLLs
#
# 1.0.15, 1.0.16 and 1.0.17 all came back clean (no source change). A clean result is only
# meaningful because the checker was proven to fail: with assembly_valheim.dll removed from the
# folder it reports hundreds unresolved. It decides which assemblies are the game's from the mod's
# own reference list, so a missing DLL counts as broken rather than being skipped.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
MANAGED="${1:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app/Contents/Resources/Data/Managed}"
MOD="$ROOT/ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll"
[[ -f "$MOD" ]] || { echo "No built mod at $MOD - build first."; exit 1; }
[[ -d "$MANAGED" ]] || { echo "No folder $MANAGED"; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cp "$ROOT/dist/windows/patcher/Mono.Cecil.dll" "$WORK/"
csc -nologo -out:"$WORK/CheckRefs.exe" -r:"$WORK/Mono.Cecil.dll" "$ROOT/Scripts/CheckRefs.cs" >/dev/null
mono "$WORK/CheckRefs.exe" "$MOD" "$MANAGED"
