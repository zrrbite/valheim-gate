# Game catalogue (2026-10-06)

Read out of Valheim 1.0.16's own data on the Mac, to plan what the saga could use.

- `game-catalog.md`: everything the game ships, by category (NPCs, creatures, pieces, items,
  locations, status effects, raids, dreams, runestones, the Deep North), with English names.
- `loc-en.tsv`: all 6,058 localization keys with their English text, from the `localization` text
  asset in `resources.assets`.
- `saga-usage.md`: what the saga already uses at `1.0.16-run.2026-10-06`, and how deeply.

Both are the game's text and data, so they stay in this private repo; a public repo gets the
scripts, never these files.

The scripts are in `Scripts/catalog/`, and they were written for a single session: `run_all.sh`
hardcodes that session's scratch folder, and the largest bundle (`c4210710`) was handled by hand.
Point `S` at a work folder before rerunning. A full unpack takes about ten minutes and about 260 MB
of intermediates. `Scripts/unpack_bundle.py` alone is enough to check one name.
