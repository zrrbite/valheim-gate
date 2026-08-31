#!/usr/bin/env bash
#
# Prints the next date-based build version, e.g. 0.221.12-run.2026-08-31c
#
# The scheme replaced an alpha counter that had reached 95 and meant nothing by then:
# a build number tells you which build it is, and nothing else. A date tells you that
# AND how old it is, which is the question actually asked of a version when something
# behaves oddly. It also cannot run away from you.
#
# Shape:  <game version>-run.<YYYY-MM-DD>[letter]
# The letter is added from the SECOND build of a day onward (b, c, d ...), so the first
# build of a day is bare. Derived from existing tags, so it is correct even when several
# builds happen hours apart or on another machine that has since been pulled.
#
# Usage:
#   Scripts/nextversion.sh                 # next version for today
#   git tag "$(Scripts/nextversion.sh)"    # tag it
#   Scripts/setversion.sh                  # write it into Version.cs, then build
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# The game version the mod is compiled against stays in the version, because it is the
# one part that is load-bearing: the deploy scripts guard on it, and it records which
# Valheim API this DLL was built for. Read from the current Version.cs so a game update
# carries through without editing this script.
GAME="$(sed -n 's/.*VERSION = "\([0-9][0-9.]*\)-.*/\1/p' ICanShowYouTheWorld/Assets/Version.cs)"
[[ -n "$GAME" ]] || { echo "Could not read the game version out of Version.cs" >&2; exit 1; }

TODAY="$(date +%Y-%m-%d)"
PREFIX="${GAME}-run.${TODAY}"

# Bare first, then b, c, ... Skipping 'a' keeps the first build of a day reading as a
# plain date rather than as "the a one", which is how anybody says it out loud.
if ! git rev-parse -q --verify "refs/tags/${PREFIX}" >/dev/null; then
    echo "$PREFIX"
    exit 0
fi

for letter in b c d e f g h i j k l m n o p q r s t u v w x y z; do
    if ! git rev-parse -q --verify "refs/tags/${PREFIX}${letter}" >/dev/null; then
        echo "${PREFIX}${letter}"
        exit 0
    fi
done

echo "More than 26 builds of $PREFIX already exist. Take the evening off." >&2
exit 1
