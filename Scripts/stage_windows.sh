#!/usr/bin/env bash
#
# Stage the current build into dist/windows/ so the Windows installer ships it.
#
# dist/windows/patcher/ICanShowYouTheWorld.dll is a COMMITTED BINARY: the Windows
# box pulls the repo and Install-Mod.ps1 copies that file into the game. Nothing
# refreshes it as a side effect of building, so a tag-build-deploy cycle that
# skips this step pushes a stale DLL to Windows while the Mac runs the new one —
# and the installer reports the stale version perfectly correctly, which makes it
# read as a version-reporting bug rather than a staging one. (Happened once, on
# alpha43.)
#
# Run this after building and BEFORE committing a release.

set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
BUILT="$ROOT/ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll"
PATCHER_BUILT="$ROOT/Patcher/bin/Debug/Patcher.exe"
DEST="$ROOT/dist/windows/patcher"

[[ -f "$BUILT" ]] || { echo "No build at $BUILT — build first."; exit 1; }

# The tag is the source of truth for what this release IS; the DLL must agree.
TAG="$(git describe --tags --abbrev=0)"
DLL_VERSION="$(python3 - "$BUILT" <<'PY'
import re, sys
data = open(sys.argv[1], 'rb').read().decode('utf-16-le', 'ignore')
m = re.search(r'\d+\.\d+\.\d+-run\.\d{4}-\d{2}-\d{2}[a-z]?|\d+\.\d+\.\d+-run\.alpha[0-9.]+|\d+\.\d+\.\d+-\d+', data)
print(m.group(0) if m else '')
PY
)"

if [[ "$DLL_VERSION" != "$TAG" ]]; then
    echo "Built DLL says '$DLL_VERSION' but the latest tag is '$TAG'."
    echo "Run Scripts/setversion.sh and rebuild, then stage again."
    exit 1
fi

cp "$BUILT" "$DEST/ICanShowYouTheWorld.dll"
[[ -f "$PATCHER_BUILT" ]] && cp "$PATCHER_BUILT" "$DEST/Patcher.exe"

echo "Staged $TAG into dist/windows/patcher/"
echo "Commit and push, then on Windows: git pull; .\\Install-Mod.ps1 -ModOnly"
