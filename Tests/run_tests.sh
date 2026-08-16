#!/bin/bash
# Compile RunMode pure logic + tests under plain mono and run them.
# RunMode/Unity/** is game-coupled and deliberately excluded.
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${TMPDIR:-/tmp}/icsytw_tests.exe"
SRC=("$ROOT"/Tests/*.cs)
if compgen -G "$ROOT/ICanShowYouTheWorld/RunMode/*.cs" > /dev/null; then
    SRC+=("$ROOT"/ICanShowYouTheWorld/RunMode/*.cs)
fi
mcs -out:"$OUT" "${SRC[@]}"
mono "$OUT"
