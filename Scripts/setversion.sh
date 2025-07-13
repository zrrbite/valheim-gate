#!/usr/bin/env bash
set -e

# 1) Compute project root (where your .git folder lives)
ROOT="$(git rev-parse --show-toplevel)"
if [[ ! -d "$ROOT" ]]; then
  echo "Error: not in a git repo"
  exit 1
fi

# 2) Find the latest tag
VERSION="$(git describe --tags --abbrev=0)"
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
