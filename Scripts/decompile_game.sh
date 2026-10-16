#!/usr/bin/env bash
#
# The game's own code as readable C#, one folder per game version, OUTSIDE this repo. A question
# like "does the turret spare deer?" is then answered by reading Turret.cs rather than an IL dump,
# and a game update's behaviour changes can be read as a diff, which check_refs.sh cannot show: it
# only says whether every name the mod uses still exists.
#
#   bash Scripts/decompile_game.sh                          # libraries/ as it stands
#   bash Scripts/decompile_game.sh <git rev>                # libraries/ at that commit (c4f6756 is 1.0.17)
#   bash Scripts/decompile_game.sh --diff <old> <new>       # e.g. --diff 1.0.17 1.0.20
#
# Output: $VALHEIM_SOURCE_DIR (default ~/Development/valheim-decompiled)/<game version>/<assembly>/.
# It is Iron Gate's code. It never goes into this repo or any other, and the script refuses a target
# inside a git work tree. It is decompiled from libraries/, which holds the VANILLA assemblies (the
# patched reference lives under Patcher/), so it reads as the game ships.
#
# Needs ilspycmd (`dotnet tool install -g ilspycmd`), which runs on .NET 10. This Mac's default
# dotnet is older, so Homebrew's is used when DOTNET_ROOT is not set.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
OUT_ROOT="${VALHEIM_SOURCE_DIR:-$HOME/Development/valheim-decompiled}"
ASSEMBLIES=(assembly_valheim assembly_utils assembly_guiutils Splatform)
MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app/Contents/Resources/Data/Managed"

# Checked on the nearest folder that exists, BEFORE creating anything: a refused target leaves nothing behind.
EXISTING="$OUT_ROOT"
while [[ ! -d "$EXISTING" ]]; do EXISTING="$(dirname "$EXISTING")"; done
if git -C "$EXISTING" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "Refusing: $OUT_ROOT is inside a git work tree, and the game's code must never be committed."
    exit 1
fi
mkdir -p "$OUT_ROOT"

if [[ "${1:-}" == "--diff" ]]; then
    OLD="$OUT_ROOT/${2:?usage: --diff <old version> <new version>}"
    NEW="$OUT_ROOT/${3:?usage: --diff <old version> <new version>}"
    [[ -d "$OLD" && -d "$NEW" ]] || { echo "Decompile both versions first."; exit 1; }
    REPORT="$OUT_ROOT/diff-$2-to-$3.diff"
    diff -ruN -x SOURCE.txt "$OLD" "$NEW" > "$REPORT" || true    # diff exits 1 when they differ
    echo "Files that changed from $2 to $3:"
    diff -rqN -x SOURCE.txt "$OLD" "$NEW" | sed -E "s|^Files $OLD/(.*) and .* differ$|  \1|" || true
    echo "Full diff: $REPORT"
    exit 0
fi

ILSPY="$(command -v ilspycmd || echo "$HOME/.dotnet/tools/ilspycmd")"
[[ -x "$ILSPY" ]] || { echo "No ilspycmd: dotnet tool install -g ilspycmd"; exit 1; }
if [[ -z "${DOTNET_ROOT:-}" && -d /opt/homebrew/opt/dotnet/libexec ]]; then
    export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
REV="${1:-}"
if [[ -n "$REV" ]]; then
    for a in "${ASSEMBLIES[@]}"; do git -C "$ROOT" show "$REV:libraries/$a.dll" > "$WORK/$a.dll"; done
    SRC="$WORK"
    FROM="libraries/ at $(git -C "$ROOT" rev-parse --short "$REV")"
else
    SRC="$ROOT/libraries"
    FROM="libraries/ in the working tree"
fi

VERSION="$(bash "$ROOT/Scripts/game_version.sh" "$SRC/assembly_valheim.dll" | tail -1)"
DEST="$OUT_ROOT/$VERSION"
if [[ -e "$DEST" && "${FORCE:-}" != 1 ]]; then
    echo "$DEST is already there (FORCE=1 to redo)."
    exit 0
fi

# Unity's assemblies come from libraries/; the game's own folder adds the few that are not kept there.
REFS=(-r "$ROOT/libraries")
[[ -d "$MANAGED" ]] && REFS+=(-r "$MANAGED")

PARTIAL="$DEST.partial"
rm -rf "$PARTIAL"
mkdir -p "$PARTIAL"
for a in "${ASSEMBLIES[@]}"; do
    echo "Decompiling $a ($VERSION)..."
    if ! "$ILSPY" --disable-updatecheck -p -o "$PARTIAL/$a" "${REFS[@]}" "$SRC/$a.dll" > "$WORK/$a.log" 2>&1; then
        tail -5 "$WORK/$a.log"
        exit 1
    fi
done
{
    echo "Valheim $VERSION, decompiled $(date +%F) by ilspycmd $("$ILSPY" --version | head -1 | awk '{print $2}')"
    echo "from $FROM."
    echo "Iron Gate's code: read it, never commit or share it."
} > "$PARTIAL/SOURCE.txt"
rm -rf "$DEST"
mv "$PARTIAL" "$DEST"
echo "Done: $DEST ($(find "$DEST" -name '*.cs' | wc -l | tr -d ' ') files)"
