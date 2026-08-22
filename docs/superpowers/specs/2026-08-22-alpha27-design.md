# Tracker colours, cooking, simultaneous quests, and a name validator

Written 2026-08-22, shipped in `0.221.12-run.alpha27`.

Six changes from one round of play feedback. Five are content or polish; the
sixth — the asset-name validator — retires the mode's oldest class of bug and is
the one worth reading if you only read one section.

## 1. Tracker colours

The tracker gave each species a stable colour from an 8-entry palette, hashed off
`Character.m_name`. Nothing stopped two species hashing to the same slot, and
this hash clusters badly at `% 8`:

| Species | slot |
|---|---|
| boar, deer | 0 (parchment) |
| greyling, neck, greydwarf, troll, crow | 7 (rose) |

Five Black Forest creatures on one colour is exactly the wall of same-coloured
text the palette exists to prevent. Boar and deer both reading white is what got
reported from play.

Two changes. The palette goes to **10 entries, matching `TrackerMaxRows`** — that
equality is load-bearing, since it is what guarantees a free slot always exists.
And assignment now runs once per frame over the visible rows: each species asks
for its hashed slot, and takes the next free one if that is claimed.

Assignment walks the species keys in **ordinal order, never the buffer's distance
order**. Distance order changes as creatures move, so two contending species
would swap colours every time they passed each other — a flicker worse than the
collision it fixed.

The trade-off, stated plainly: a species can change colour when a different
species walks into range and claims the slot it preferred. It is stable whenever
it is the only claimant, which is the common case.

## 2. `BuildPiece` as a composite sub

`CreditMeasureSub` accepted `CollectItem` and `CollectFood`. `BuildPiece` joins
them — it is an absolute quantity needing no per-sub `Baseline`, which is the
rule composites actually require. `StatDelta` stays excluded for the reason it
always was: one `Baseline` per slot, not one per sub.

Simultaneous *kill* quests already worked (`cq-forest-sweep` ran two kill subs);
that was a content gap, so it got new entries rather than new machinery.

## 3. Cooking station → Act I step 7

`CookingStation` is a compiled class, so it is one more line in
`PieceCategories`, and `mq-cook` goes straight after `mq-fire` — you have just
lit the fire, now put a rack over it. It covers the plain and iron stations both,
which is the intent: the quest is "you can cook now", not "you built one specific
piece".

Nothing in the chain taught cooking before this, which left the single biggest
lever on health and stamina as something the player had to know about from
outside the run. Act I is 14 links; its heat floor goes 13 → 14.

## 4. The asset-name validator

Run Mode's oldest and most expensive bug class: asset names are Unity data,
invisible to the compiled assembly, and a wrong one does not throw.

| Mistake | What actually happens |
|---|---|
| Wrong creature name | Kill quest counter never moves |
| Wrong item token | Collect sub dead for the whole run |
| Wrong `RequiresBuilt` | Task never dealt at all — quietest of the three |
| Wrong reward prefab | Logs, but only when someone reaches that step |

Every one looks like ordinary bad luck. The only detector to date was playing
until something felt stuck, and it has cost several builds.

Both registries turn out to be reachable: `ZNetScene.GetPrefab` for creatures and
`ObjectDB.m_items` for items — the latter carrying both prefab names and the
`$item_` shared tokens `Inventory.CountItems` actually matches on. So at run
start, every name in the pool, the chain and the reward tables is resolved, and
the failures are logged once.

The creature check additionally requires a `Character` component. ZNetScene holds
every networked prefab, so a name resolving to a rock would pass a bare existence
check and still never register a kill.

Split for testability: `NameManifest` (pure, unit-tested) walks definitions and
buckets the names by which registry answers for them; `RunService.ValidateAssetNames`
does the lookups, which need the live game.

It is **diagnostics only**. Nothing is disabled on a miss — a definition that
fails here fails the way it always did, and taking the run away over it would be
worse than one dud task.

A bug the tests caught while writing it: the `RequiresBuilt` collection sat after
an early `continue` on the null-`Subs` guard, so it was skipped for every
definition without subs — which is most of them, including `s-doors`, the only
one that currently has a gate.

## 5. Craft-food quests, both kinds

The validator is what makes named cooked-food tokens usable at all. Before it, a
wrong `$item_` token was a sub that stayed dead all run with nothing to say why.

- `cq-larder` names things: cook 5 meat, hold 20 wood. Requires genuinely cooked
  food, which `CollectFood` cannot express.
- `cq-provisions` names nothing: hold 12 food, kill 3 boar, gated on owning a
  cooking station. Works whatever the tokens turn out to be. Its weakness is
  honest — `CollectFood` counts raspberries, so a determined forager finishes it
  without cooking.

Both are gated with `RequiresBuilt = "Cooking"`, so neither is ever dealt to
someone who cannot start it.

Also added: `cq-hearth` (two build subs plus food), `cq-meadow-cull` and
`cq-night-watch` (three kill subs each).

## 6. Windfall

A one-charge active on `Keypad8` that doubles every stack the player carries.
Never refills — `BoonEngine` does not re-offer a held boon, so it is exactly one
use per run.

`m_shared.m_maxStackSize > 1` is the filter, and it does real work: it is a
compiled field, so no asset names are involved, and every weapon, tool and piece
of armour has a max stack of 1 and is skipped automatically. What remains is
materials, arrows, food and trophies.

The **snapshot** is load-bearing. `GetAllItems` hands back a list; iterating the
live inventory would keep meeting the stacks it had just added and double them
again forever. Amounts are read before any grant for the same reason — a stack
that merges with one just created would otherwise be re-measured at its new size.

Overflow is not lost: grants go through `RunService.GrantItem`, which adds what
fits and drops the rest at the player's feet.

### The exploit, acknowledged

Doubling *everything stackable* includes food, and food is health and stamina in
Valheim. Used on a full pack of cooked meals this is close to a second health bar
for the next hour, on top of the boss-spoils food. The natural play is to fill
the inventory with the most valuable thing you own and then press it.

This was the owner's explicit choice over a materials-only version, made with the
trade-off stated. It is one word's difference in `ActivateWindfall` if play says
the food doubling is too much.

## Testing

276 assertions, up from 263. New: `BuildPieceCompositeTests` (param-scoping
across subs, latching, kill-vs-measure isolation) and `NameManifestTests`
(bucketing by kind, dedup, null tolerance, `RequiresBuilt` collection).

The tracker, the scanner, the validator's lookups and the boon live in
`RunMode/Unity/**`, which `Tests/run_tests.sh` excludes as game-coupled. They are
play-test verified — see the alpha27 task in `HANDOFF_WINDOWS.md`.
