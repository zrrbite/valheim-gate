# Filling in the acts, and the boat problem

Written 2026-08-22, shipped in `0.221.12-run.alpha29`.

alpha28 gave the saga five acts but left III–V as three-or-four-step
placeholders. This writes them properly — building and mob beats per biome — and
solves the question that came with the request: *"if we require a boat to get to
a boss we should have boat quests, but if no water around and no boats needed,
then a quest would be dumb."*

## The boat problem, and the rule it produced

A boat quest only makes sense on a world where water is in the way, and nothing
knows that in advance. The main chain is **linear and has no skip**, so a boat
step there would hard-stall a run on a world where the biome happens to be
walkable.

The rule that falls out, and which the act content now follows throughout:

> **The chain asks for destinations. The pool asks for transport.**

Each act opens with "Reach the Swamp" — true whether you walked, sailed or
portalled. Boat quests live only in the random pool, where a gate can safely make
them situational.

Boat quests are gated twice, on *evidence* rather than guesswork:

- `Biomes = Ocean` — this run has been on open water.
- `RequiresBuilt = "Ship"` — this run owns a boat (`Ship` is a compiled class
  covering raft, karve and longship).

A landlocked run satisfies neither and is never dealt one.

The Ocean gate is **conservative on purpose**. Valheim assigns `Ocean` to deep
water, so paddling off a beach usually still reads as the shore's own biome — the
gate really fires only for players genuinely out on open water. Never offering is
the right direction to be wrong here.

## `ChallengeKind.ReachBiome`

`Param` names a `Heightmap.Biome` member; target is 1. The host reads the
`_visitedBiomes` mask the run already keeps for the biome filter — a mask it only
ever ORs into, so arrival is permanent by construction and leaving cannot un-earn
it.

The whole mask is reported every poll rather than just newly-entered biomes, for
the same reason the build scanner re-reports: a step dealt *after* the player was
already there must still complete, and the engine deliberately starts each chain
step at zero.

A typo'd biome name would stall an act at its **opening** beat, with the player
standing in the biome and nothing happening, so the validator gets a biome bucket
checked against `Enum.TryParse<Heightmap.Biome>`.

## The latch constraint

`_builtSeen` latches for the whole **run**, not per act. So a build category used
in an earlier act is already satisfied when a later act's step is dealt — the step
would complete instantly and pay out for nothing.

**A build category may therefore appear in one act only.** That is invisible in
review and obvious in play, which is the worst combination, so `ValidateActs`
checks it and names both offending acts.

It works out cleanly:

| Act | Build categories |
|---|---|
| I — The Meadows | Fire, Cooking, Bed, Chest |
| II — The Black Forest | Smelter, Portal |
| III — The Swamp | Fermenter |
| IV — The Mountains | *(none)* |
| V — The Plains | Windmill |

**The Mountains have no build step, deliberately.** No distinctively
mountain-built piece has a compiled class of its own, and every category that
does is claimed by an earlier act. Inventing a filler step would be worse than an
extra fight, so the act gets an extra fight.

Two ambiguities inherited from Valheim's own class layout, worth knowing before
adding more: `Smelter` also matches the charcoal kiln and blast furnace, and
workbench, forge and artisan table are *all* `CraftingStation` — so "build a
forge" is not expressible.

## The acts

```
ACT II — The Black Forest → The Elder            (9)
  1 Reach the Black Forest      6 Kill 10 Greydwarves
  2 Mine the Black Forest       7 Kill 3 Greydwarf Brutes
  3 Build a smelter             8 Kill a Troll  → Ancient Seeds
  4 Forge 3 things in bronze    9 Defeat The Elder
  5 Build a portal

ACT III — The Swamp → Bonemass                   (7)
  1 Reach the Swamp             5 Mine iron (60 hits)
  2 Kill 8 Draugr               6 Kill 3 Leeches
  3 Build a fermenter           7 Defeat Bonemass
  4 Kill 5 Blobs

ACT IV — The Mountains → Moder                   (7)
  1 Reach the Mountains         5 Kill 2 Stone Golems
  2 Kill 6 Wolves               6 Kill 3 Fenrings
  3 Kill 4 Drakes               7 Defeat Moder
  4 Mine silver (60 hits)

ACT V — The Plains → Yagluth                     (7)
  1 Reach the Plains            5 Kill 3 Lox
  2 Kill 10 Fulings             6 Kill 2 Fuling Berserkers
  3 Build a windmill            7 Defeat Yagluth
  4 Kill 5 Deathsquitos
```

Two choices worth naming:

**The fermenter is not decoration.** Poison resistance mead *is* the answer to
Bonemass, so the building step teaches the fight. The Swamp's arrival step pays
the mead outright, and the fermenter step pays honey and thistle to make more.

**The portal is the other half of "we have to leave our house behind".** The
stash (still unbuilt) carries your things; a portal carries you. Worth watching
in play whether having both removes travel from the game entirely, which is a
fair amount of what Valheim is.

Each pre-boss step hands over that boss's summoning items — Ancient Seeds,
withered bones, dragon eggs, Fuling totems — on the Act I principle that a run
gates on the **fight**, never on drop luck.

## Balance

Questline heat across the whole saga is now **44** (14+9+7+7+7). At the default
0.05 weights that is roughly **×3.2 enemy damage by the Plains** before a single
random task.

For a roguelite that escalation may be exactly right, but it is far steeper than
anything anyone has played, and it lands on a heat model that has never had its
tuning pass. Not compensated for here — flagged so the number is not a surprise.

## Testing

305 assertions, up from 299. New `ReachBiomeTests` cover param-scoping, latching
against a zero report, and cross-kind isolation; `NameManifestTests` gains the
biome bucket.

Verified by inspection against the built source: no duplicate step ids across the
saga, every step but `pl-yagluth` has a reward entry (its reward is finishing),
and no build category appears in two acts.

The biome poll, the new categories and both validators live in
`RunMode/Unity/**`, which the harness excludes — play-test verified, see the
alpha29 task in `HANDOFF_WINDOWS.md`.

## Still not built

Deer content and the universal stash, both designed and approved in the alpha28
session — see [the acts design](2026-08-22-acts-design.md), last section. Deferred
again so the act content gets played first.
