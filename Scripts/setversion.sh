#!/usr/bin/env bash
set -e

# 1) Compute project root (where your .git folder lives)
ROOT="$(git rev-parse --show-toplevel)"
if [[ ! -d "$ROOT" ]]; then
  echo "Error: not in a git repo"
  exit 1
fi

# 2) Find the latest tag
# Newest version tag reachable from HEAD. Not `git describe --tags --abbrev=0`: when
# two tags sit on the same commit (two builds with no commit in between) describe
# returns whichever sorts FIRST, i.e. the OLDER one — which is how a fresh 1.0.12
# build got refused as stale on 2026-09-12. Newest commit date wins, and on a tie
# the higher version; the '[0-9]*' filter skips stray non-version tags ('working').
VERSION="$(git tag --merged HEAD --list '[0-9]*' --sort=-v:refname --sort=-creatordate | head -n1)"
echo "Using version: $VERSION"

# 3) Paths
TEMPLATE="$ROOT/ICanShowYouTheWorld/VersionTemplate.cs"    # adjust if yours lives elsewhere
DESTDIR="$ROOT/ICanShowYouTheWorld/Assets/"
DEST="$DESTDIR/Version.cs"

# 4) Ensure target directory exists
mkdir -p "$DESTDIR"

# 5) Generate
sed "s/__VERSION__/$VERSION/" "$TEMPLATE" > "$DEST"

echo "Wrote $DEST"
