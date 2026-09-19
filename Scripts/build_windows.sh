#!/usr/bin/env bash
#
# Build the mod on the WINDOWS box and stage it for the installer. Run from Git Bash.
#
#   Scripts/build_windows.sh             # rebuild the current version and restage it
#   Scripts/build_windows.sh --release   # new date tag first, then build and stage
#
# then, in PowerShell:
#
#   .\dist\windows\Install-Mod.ps1 -ModOnly     # mod change only
#   .\dist\windows\Install-Mod.ps1              # after a Valheim update (re-patches too)
#
# This is the Mac's tag / setversion / build / stage loop with Visual Studio's MSBuild
# in place of Mono's. Only the mod project is built here: the bundled
# dist/windows/patcher/Patcher.exe does the Patcher's job, and the Patcher project wants
# a NuGet restore this box has no nuget.exe for.
#
# When the PATCHER itself changed, build it too — the restore is four files:
#   mkdir -p packages/Mono.Cecil.0.11.4/lib/net40
#   cp dist/windows/patcher/Mono.Cecil*.dll packages/Mono.Cecil.0.11.4/lib/net40/
#   "$MSBUILD" Patcher/Patcher.csproj -p:Configuration=Debug -v:minimal
# (the bundled Cecil is exactly the 0.11.4 the csproj HintPaths ask for). stage_windows.sh
# then picks the new Patcher.exe up — but only if that build exists, so check it changed.
# A Patcher change also needs a FULL Install-Mod.ps1 run, not -ModOnly.
#
# After a VALHEIM UPDATE do this first, or the build compiles against the old game:
#   1. Copy the game's unpatched assembly_valheim.dll, assembly_utils.dll,
#      assembly_guiutils.dll, Splatform.dll and UnityEngine.UI.dll into libraries/
#      (UnityEngine.UI.dll also to the repo root — the csproj looks for it there).
#   2. If Unity changed too (first line of Player.log), unzip both
#      https://unity.bepinex.dev/{corlibs,libraries}/<version>.zip into libraries/.
#   3. Patch the new assembly into the reference the csproj compiles against:
#        dist/windows/patcher/Patcher.exe <vanilla> Patcher/bin/Debug/patched/assembly_valheim.dll \
#            dist/windows/patcher/ICanShowYouTheWorld.dll <game Managed folder>
#   4. Change the game-version prefix in ICanShowYouTheWorld/Assets/Version.cs, then
#      run this script with --release. Fix whatever no longer compiles.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

MSBUILD="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
[[ -x "$MSBUILD" ]] || { echo "MSBuild not found at: $MSBUILD (Visual Studio 2022 Community?)"; exit 1; }

if [[ "${1:-}" == "--release" ]]; then
    NEXT="$(Scripts/nextversion.sh)"
    git tag "$NEXT"
    echo "Tagged $NEXT"
    Scripts/setversion.sh
fi

echo "Building..."
"$MSBUILD" ICanShowYouTheWorld/ICanShowYouTheWorld.csproj -p:Configuration=Debug -v:minimal -nologo \
    | grep -Ei "error|Build succeeded|Build FAILED|-> " || true
[[ -f ICanShowYouTheWorld/bin/Debug/ICanShowYouTheWorld.dll ]] || { echo "No build output."; exit 1; }

# stage_windows.sh refuses if the DLL's version and the newest tag disagree —
# which is the point: a build that reports the wrong number never ships.
Scripts/stage_windows.sh

echo
echo "Now install, in PowerShell:"
echo "    .\\dist\\windows\\Install-Mod.ps1 -ModOnly     # or without -ModOnly after a game update"
echo "Then commit and push (with --release, push the tag too: git push origin --tags)."
