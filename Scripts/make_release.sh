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
#   Scripts/make_release.sh              # release the current tag (must be a SAGA-ONLY build)
#   Scripts/make_release.sh --gm           # ...allow a GM build to be released
#   Scripts/make_release.sh --allow-dirty  # ...with uncommitted changes
#
# A release is a thing handed to somebody else, so it REFUSES a GM build unless asked twice.
# The flavour is baked into the DLL (ModVersion.FLAVOUR) and read back out of it here rather
# than taken on trust from whoever ran the build: the one accident the build-time switch can
# still cause is shipping the wrong DLL, and this is the place to catch it.
#
# To make one:  Scripts/build_windows.sh --release --saga-only
#
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

ALLOW_DIRTY=0
ALLOW_GM=0
for arg in "$@"; do
    case "$arg" in
        --allow-dirty) ALLOW_DIRTY=1 ;;
        --gm)          ALLOW_GM=1 ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

green() { printf '\033[0;32m✓\033[0m %s\n' "$1"; }
info()  { printf '\033[0;34mℹ\033[0m %s\n' "$1"; }
fail()  { printf '\033[0;31m✗\033[0m %s\n' "$1" >&2; exit 1; }

# ---- 1. The three things that must agree --------------------------------

# Newest version tag reachable from HEAD. Not `git describe --tags --abbrev=0`: when
# two tags sit on the same commit (two builds with no commit in between) describe
# returns whichever sorts FIRST, i.e. the OLDER one — which is how a fresh 1.0.12
# build got refused as stale on 2026-09-12. Newest commit date wins, and on a tie
# the higher version; the '[0-9]*' filter skips stray non-version tags ('working').
TAG="$(git tag --merged HEAD --list '[0-9]*' --sort=-v:refname --sort=-creatordate | head -n1)"

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
m = re.search(r'\d+\.\d+\.\d+-run\.\d{4}-\d{2}-\d{2}[a-z]{0,2}|\d+\.\d+\.\d+-run\.alpha[0-9.]+|\d+\.\d+\.\d+-\d+', data)
print(m.group(0) if m else '')
PY
}

BUILT="$ROOT/ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll"
[[ -f "$BUILT" ]] || fail "No build output at $BUILT"

# The same decode-and-grep as read_version, against ModVersion.FlavourMarker - which exists
# precisely so there is something unique to search for. See the remarks on that constant.
read_flavour() {
    python3 - "$1" <<'PYEOF'
import re, sys
data = open(sys.argv[1], 'rb').read().decode('utf-16-le', 'ignore')
m = re.search(r'ICSYTW_FLAVOUR_(gm|saga)', data)
print(m.group(1) if m else '')
PYEOF
}

BUILT_VERSION="$(read_version "$BUILT")"
[[ "$BUILT_VERSION" == "$TAG" ]] \
    || fail "Built DLL says '$BUILT_VERSION' but the tag is '$TAG'. Run Scripts/setversion.sh and rebuild."
green "DLL version matches the tag"

FLAVOUR="$(read_flavour "$BUILT")"
[[ -n "$FLAVOUR" ]] || fail "Could not read the flavour out of the DLL. Rebuild with Scripts/build_windows.sh."
if [[ "$FLAVOUR" == "gm" && $ALLOW_GM -eq 0 ]]; then
    fail "This is a GM build. A release goes to somebody else, so it wants a saga-only one:
       Scripts/build_windows.sh --release --saga-only
     Or pass --gm if you really mean to hand over the cheat mod."
fi
green "Flavour: $FLAVOUR"

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
# The flavour is in the NAME as well as in the DLL, so a zip sitting in a downloads folder
# still answers the question.
NAME="ICanShowYouTheWorld-$TAG-$FLAVOUR-windows"
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
  2. Start Valheim. The mod loads itself at startup; a popup at the main
     menu should say v$TAG.
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
