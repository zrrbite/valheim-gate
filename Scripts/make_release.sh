#!/usr/bin/env bash
#
# Build a self-contained Windows release zip that a tester can unpack and run.
#
# The zip is everything dist/windows already needs to install the mod on a
# machine that has never seen this repo: the installer, the patcher, the mod
# DLL, and the human instructions. The tester needs Valheim and nothing else —
# no git, no build tools, no Mac.
#
# Why a script rather than "zip the folder": three things have to AGREE, and
# have not always. The git tag, the version compiled into the DLL, and the DLL
# actually staged into dist/windows. alpha43 shipped to Windows as alpha42.2
# because the third was skipped, and the installer reported the stale version
# perfectly correctly — which reads as a version bug and is a staging one. This
# refuses to build a release unless all three match.
#
#   Scripts/make_release.sh              # release the current tag
#   Scripts/make_release.sh --allow-dirty  # ...with uncommitted changes
#
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

ALLOW_DIRTY=0
[[ "${1:-}" == "--allow-dirty" ]] && ALLOW_DIRTY=1

green() { printf '\033[0;32m✓\033[0m %s\n' "$1"; }
info()  { printf '\033[0;34mℹ\033[0m %s\n' "$1"; }
fail()  { printf '\033[0;31m✗\033[0m %s\n' "$1" >&2; exit 1; }

# ---- 1. The three things that must agree --------------------------------

TAG="$(git describe --tags --abbrev=0)"

if [[ $ALLOW_DIRTY -eq 0 ]] && [[ -n "$(git status --porcelain)" ]]; then
    fail "Working tree is dirty. Commit first, or pass --allow-dirty for a test build."
fi

info "Releasing $TAG"

# Build, so the DLL is definitely current rather than whatever was left lying
# around from an experiment.
info "Building..."
msbuild Valheim.sln -p:Configuration=Debug -v:quiet > /tmp/release-build.log 2>&1 \
    || { tail -20 /tmp/release-build.log; fail "Build failed."; }
green "Built"

read_version() {
    python3 - "$1" <<'PY'
import re, sys
data = open(sys.argv[1], 'rb').read().decode('utf-16-le', 'ignore')
m = re.search(r'\d+\.\d+\.\d+-run\.alpha[0-9.]+|\d+\.\d+\.\d+-\d+', data)
print(m.group(0) if m else '')
PY
}

BUILT="$ROOT/ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll"
[[ -f "$BUILT" ]] || fail "No build output at $BUILT"

BUILT_VERSION="$(read_version "$BUILT")"
[[ "$BUILT_VERSION" == "$TAG" ]] \
    || fail "Built DLL says '$BUILT_VERSION' but the tag is '$TAG'. Run Scripts/setversion.sh and rebuild."
green "DLL version matches the tag"

# Stage into dist/windows, which is what actually gets zipped.
Scripts/stage_windows.sh > /dev/null
STAGED_VERSION="$(read_version "$ROOT/dist/windows/patcher/ICanShowYouTheWorld.dll")"
[[ "$STAGED_VERSION" == "$TAG" ]] || fail "Staging did not take: dist has '$STAGED_VERSION'."
green "Staged into dist/windows"

# ---- 2. The payload is complete -----------------------------------------

for f in dist/windows/Install-Mod.ps1 \
         dist/windows/patcher/ICanShowYouTheWorld.dll \
         dist/windows/patcher/Patcher.exe \
         dist/windows/patcher/Mono.Cecil.dll; do
    [[ -f "$f" ]] || fail "Missing from the release payload: $f"
done
green "Payload complete"

# ---- 3. Zip it ----------------------------------------------------------

OUT_DIR="$ROOT/Release"
NAME="ICanShowYouTheWorld-$TAG-windows"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

mkdir -p "$OUT_DIR" "$STAGE/$NAME"
cp -R dist/windows/. "$STAGE/$NAME/"

# A tester unzipping this has no repo and no context. Tell them what it is and
# what to run, in the first file they will see.
cat > "$STAGE/$NAME/START-HERE.txt" <<EOF
Valheim: The Saga — $TAG
========================================

WHAT THIS IS
  A mod that turns Valheim into a story-driven campaign. Act I (the Meadows)
  is the part being tested.

WHAT YOU NEED
  Valheim, installed through Steam. Nothing else.

INSTALL
  1. Right-click Install-Mod.ps1 and choose "Run with PowerShell".
     If it refuses, open PowerShell as Administrator, cd to this folder,
     and run:  .\\Install-Mod.ps1
  2. Start Valheim and open the CREDITS menu. That is what loads the mod.
     A popup should say v$TAG.
  3. Press End in-game to open the Run window, and start a saga.

UNINSTALL
  .\\Install-Mod.ps1 -Restore

IF SOMETHING LOOKS WRONG
  See CHECKING-THE-LOG.md — it explains how to read the mod's own log,
  which usually says exactly what went wrong.

NOTES
  - A Steam game update overwrites the patched file. Re-run the installer.
  - This modifies your Valheim install. The installer keeps a vanilla backup
    and -Restore puts it back.
  - Play on a world you do not mind experimenting with.
EOF

( cd "$STAGE" && zip -qr "$OUT_DIR/$NAME.zip" "$NAME" )

green "Release built: Release/$NAME.zip"
info "$(du -h "$OUT_DIR/$NAME.zip" | cut -f1) — hand this to a tester as-is."

# ---- 4. The zip is not the only way people install --------------------------
#
# Staging above may have changed dist/windows/patcher/ICanShowYouTheWorld.dll. The ZIP is built
# from the staged files so it is always correct — but the Windows box installs by `git pull`, and
# an uncommitted staging leaves that path one release behind while this script reports success.
#
# That happened on alpha62: the zip was right, the repo was not, and the installer's own staleness
# guard is what caught it. Correct is not the same as shipped.
if [[ -n "$(git status --porcelain dist/windows)" ]]; then
    echo
    printf '\033[0;33m!\033[0m %s\n' "dist/windows changed and is NOT committed."
    printf '\033[0;33m!\033[0m %s\n' "The zip is correct, but a 'git pull' install would still get the old build."
    info "Fix with:  git add dist/windows && git commit -m 'chore: stage $TAG for windows' && git push"
    exit 2
fi
green "dist/windows is committed — git-pull installs will get $TAG"
