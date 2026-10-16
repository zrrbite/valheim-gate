# Spike: checking the saga's asset names offline (2026-10-15)

**Question.** Can the asset names the saga looks up by name be checked against the game's files without launching
it? Today only the run-start self-check (`RunService.LogSelfCheck`) catches a name a game update renamed, and
`check_refs.sh` cannot see names at all.

**Answer: yes, and cheaply.** Measured on Valheim 1.0.20 on the Mac. The spike's scripts were throwaway; this note
keeps what they found.

## What the game ships

- Nearly all assets live in about 800 Unity asset bundles under
  `valheim.app/Contents/Resources/Data/StreamingAssets/SoftRef/Bundles/`, about 4 GB. The classic data files
  (`resources.assets`, `level0`, ...) hold only fonts, textures and script records.
- Two **plain-text indexes** sit beside the bundles, written by Valheim's own SoftRef system (header
  `SoftRef manifest - Text`, `version: 2`). Each entry has an `asset ID`, a `bundle` and a `path in bundle`.
  - `manifest_extended` (3 MB): items, creatures, pieces, effects and status effects. It lists 5,936 `.prefab` files
    and 904 `.asset` files.
  - `manifest` (217 KB): the **locations** and dungeon rooms, about 587 entries. `Eikthyrnir`, `GDKing` and the
    other altars are only here.

## Results

- **Names, from both indexes: 94 of 94 found in 0.03 s, with no extra software.** The names covered:
  - the watch-post and its bolts;
  - the sea creatures and the ward's sparks;
  - the 35 creatures the acts name;
  - the company;
  - the saga items' looks, the pyre's flametal and the Plains foods;
  - the 9 gods' altars;
  - `GP_Moder`.

  Both made-up control names were reported missing.
- **Item display tokens: 7 of 7 found in 2.7 s.** These are `$item_wood` and the like, used by the CollectItem steps.
  - They are not file names. They sit in each item's `ItemDrop.m_itemData.m_shared.m_name`.
  - Reading them needs UnityPy (`pip install UnityPy`, 1.25.4). The bundles carry type trees, so
    `obj.read_typetree()` works with no game assemblies.
  - All 1,080 item tokens are in the two bundles that hold `Assets/GameElements/Items/*.prefab`.
  - The made-up control token was reported missing.

## Pitfalls found

- **Match by kind, never by bare file name.** Bare matching "found":
  - `TurretBoltWood` as a material;
  - `Skeleton` as a texture;
  - the god `Bonemass`'s creature prefab in place of his altar.

  Match prefabs on `.prefab`, altars on `.prefab` under `/Locations/`, and status effects on `.asset`.
- **A prefab file existing is not the whole of the in-game check.** The self-check also asks, for example, that a
  sea creature's prefab has a `Character` component. The indexes cannot say that; UnityPy could, at bundle cost.
- **The index format is Valheim's own and could change.** A real check should fail loudly if neither index parses,
  and not report everything as missing.

## What building it would take

- A script (`Scripts/check_assets.sh` plus Python) that reads both indexes, with UnityPy for the item tokens. It runs
  in under 5 s. It belongs in the game-update recipe beside `check_refs.sh` and `decompile_game.sh`.
- **The names list is the real work.**
  - Some names live in pure code: `NameManifest`, `SeaDanger.PrefabOf`.
  - Most live in game-coupled code: `RunService.Acts()`, the `Bosses` table, `SagaItems` fallbacks and `BoonEffects`.
  - The spike gathered them by hand, which would drift. The names need one source that both the self-check and the
    offline check read.
