#!/bin/bash
S=/private/tmp/claude-501/-Users-martinkjeldsen-Development-valheim-gate/c15744b9-c402-4a6d-9dc6-e09c82c91094/scratchpad/catalog
B="$HOME/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app/Contents/Resources/Data/StreamingAssets/SoftRef/Bundles"
cd "$S/work"
for p in "$B"/*; do
  b=$(basename "$p")
  case $b in c4210710|f9285044|61c598bb) continue;; esac
  [ -f "$S/work/ex/$b.names.json" ] && continue
  python3 -I /Users/martinkjeldsen/Development/valheim-gate/Scripts/unpack_bundle.py "$p" "$S/work/bin/$b.bin" >/dev/null 2>>"$S/work/errors.log" || { echo "UNPACK FAIL $b" >>"$S/work/errors.log"; rm -f "$S/work/bin/$b.bin"; continue; }
  python3 -I "$S/scripts/extract.py" "$p" "$S/work/bin/$b.bin" "$S/work/ex" >>"$S/work/progress.log" 2>>"$S/work/errors.log" || echo "EXTRACT FAIL $b" >>"$S/work/errors.log"
  [ "$b" = 86c3d76e ] || [ "$b" = 6a33a62 ] || rm -f "$S/work/bin/$b.bin"
done
echo DONE >> "$S/work/progress.log"
