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

# Mono on the Mac, Roslyn on the Windows box. Windows has no mcs and no mono, and the
# build loop insists the suite passes before anything ships — so the machine the mode is
# actually PLAYED on has to be able to run it. csc.exe produces a native exe; nothing
# else about the tests changes.
CSC_WIN="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe"

if command -v mcs > /dev/null 2>&1; then
    mcs -out:"$OUT" "${SRC[@]}"
    mono "$OUT"
elif [[ -x "$CSC_WIN" ]]; then
    WIN_OUT="$(cygpath -w "$OUT")"
    "$CSC_WIN" -nologo -out:"$WIN_OUT" "${SRC[@]}"
    "$OUT"
else
    echo "Neither mcs nor Visual Studio's csc.exe found — cannot build the tests." >&2
    exit 1
fi
