# Homestead steps, and a door task that waits for a door

Written 2026-08-22, shipped in `0.221.12-run.alpha26`.

Act I asked the player to raise a roof and then, two steps later, to settle in
and sleep — without ever asking for the fire or the bed those steps silently
depend on. This adds the three missing beats (fire, bed, chest) as questline
steps, and stops "Open 8 doors" from being dealt to a player who has no door.

## The measurement problem

Everything in Run Mode's questline measures through something Valheim already
counts. That is not a stylistic preference; it is the mode's oldest landmine.
Item and piece prefab names are Unity asset data, invisible to the compiled
assembly, so a wrong CREATURE or piece name fails **silently** — the objective
simply never completes and the whole chain stalls with no error anywhere. The
existing chain therefore leans on `PlayerStatType` members (`Builds`,
`TimeInBase`, `Sleep`, `CraftsOrUpgrades`) checked against the enum in the IL.

`PlayerStatType` has no member for "built a fire". `Builds` counts every piece
placed and cannot tell a hearth from a wall.

The way out is that the *behaviours* are compiled classes even though the
prefabs are not. Verified in the IL:

| Category | Compiled type | Notes |
|---|---|---|
| `Fire` | `Fireplace` | campfire, hearth, bonfire |
| `Bed` | `Bed` | |
| `Chest` | `Container` | |
| `Door` | `Door` | |

Plus `Piece.GetAllPiecesInRadius(Vector3, float, List<Piece>)` (public static)
and `Piece.IsCreator()`, whose IL compares `m_creator` against
`Game.instance.GetPlayerProfile().GetPlayerID()` — so it means "you built this",
not "you are standing near one". A wrong type name does not compile. A wrong
prefab name ships.

## `ChallengeKind.BuildPiece`

Appended to the enum, never inserted — saves store challenge ids rather than
kinds, but renumbering an enum the game code reads is a trap not worth setting.

`Param` names a category from the table above; `Target` is always 1. It is
param-scoped alongside `CollectItem` and `StatDelta`, so a fire report cannot
complete a bed step. It is not usable as a composite sub, which falls out for
free: `CreditMeasureSub` only handles `CollectItem` and `CollectFood`.

Targets are absolute rather than incremental because the host re-reports every
poll and reports only what it can currently see. Progress therefore relies on
`ReportMeasure`'s max-semantics to latch — walking away from the house must not
un-earn the step.

## Host side: the scan

`RunService.PollBuiltPieces`, inside the existing 1 Hz `PollMeasures`:

1. If any category is still unseen, scan `GetAllPiecesInRadius` around the
   player (`runBuildScanRadius`, default 20m) and latch every category found on
   a piece where `IsCreator()` holds.
2. Report **every** latched category, whether or not the scan found anything
   new.

Step 2 is what lets a step complete for a piece built before the step was dealt.
The engine deliberately starts each chain step at zero — a report cannot be
banked against a step that does not exist yet — so without the re-report, a
player who built a chest an hour early would be told to build a second one.

The scan stops entirely once all four categories are latched, which in a
finished house is most of the run.

`_builtSeen` is latched and never removed from. A set that could shrink would
un-finish a completed step the moment the player left home, and would make a
gated task flicker in and out of the draw pool depending on where they happened
to be standing when a slot refilled.

### Known and accepted

Start a run standing in a base you already built and the fire/bed/chest steps
complete within a second of being dealt, rewards included. Telling "built during
this run" from "was already there" needs a per-piece ZDO snapshot taken at run
start, and that still misses anything outside its radius — real machinery for
half a fix. Anyone running at an established base has skipped far more than
three steps' worth of progression already.

## The door gate

`ChallengeDefinition.RequiresBuilt` names a category the run must have built
before the definition may be **dealt**. The engine does not read it; it rides
`ExternalFilter`, host-side, beside the biome gate, because only the host knows
what the run has built. It is authored on the definition rather than special-
cased in a lambda so the requirement lives with the thing that has it.

`s-doors` gets `RequiresBuilt = "Door"`. Previously it was Tier 1, i.e. drawable
from the first minute of a run — when the player has no hammer, let alone a
door. A task that cannot be completed is not a challenge; it is a dead slot the
player must pay heat to reroll.

## The chain

Each new step lands immediately before the step that already, silently, required
it. `TimeInBase` only accrues while `Player.IsSafeInHome`, which needs real
comfort — a roof *and* a fire. `Sleep` needs a bed.

```
 1 Craft an axe            7 Kill 6 Greylings
 2 Craft a hammer          8 Settle in (2 min at home)
 3 Build a workbench       9 Build a bed              [new]
 4 Hunt 5 Boar            10 Sleep through the night
 5 Raise a roof (6)       11 Build a chest            [new]
 6 Build a fire   [new]   12 Hunt 3 Deer
                          13 Defeat Eikthyr
```

The chest is the one step nothing downstream depends on. It sits at 11 because
somewhere to put the spoils is what you want *before* a hunt, and its reward
feeds that hunt directly.

Rewards, following the chain's rule that each step pays for what the next one
needs:

| Step | Pays | Why |
|---|---|---|
| `mq-fire` | DeerHide ×6, Flint ×10 | A bed wants deer hide; the chain does not otherwise hand one over until the deer hunt at #12 |
| `mq-bed` | Wood ×30, Resin ×10 | Timber for the rest of the house |
| `mq-chest` | ArrowFlint ×30 | A full quiver, straight into Hunt 3 Deer |

## Persistence

`RunSaveState.builtCategories`, a `List<string>`. Null on a pre-alpha26 save,
which reads as "nothing yet" and re-latches on the next scan.

It has to be persisted because the scan only sees what is near the player:
resume a run out in the field with an empty set and a finished build step would
un-finish itself and the door task would drop out of the pool until the player
next walked home.

Inserting three links reorders the chain, which is exactly what
`RestoreMainQuest` was built for — it resolves the saved `mainQuestId` in
preference to the index, so a live alpha25 run resumes on the step it was
actually on rather than being handed an instant, unearned completion. This is
the third time that path has earned its keep.

## Balance note

Three more steps at 1 heat each moves Act I's questline floor from 10 heat to
13. At the default 0.05 weights that is enemy damage and level-up rate ×1.50 →
×1.65 by Eikthyr, before any random task. Not compensated for here: heat's curve
has never been played as designed (it was divided by 100 until alpha17), so the
tuning pass that is coming should set these numbers against the new floor rather
than the old one.

## Testing

`ChallengeEngineTests.BuildPieceTests` covers param-scoping (a fire must not
complete a bed), cross-kind isolation, latching against a zero report, the
reserved questline slot, that a step starts at zero rather than inheriting an
earlier report, and the `RequiresBuilt` gate through `ExternalFilter`. 263
assertions total, up from 250.

The scanner and the filter wiring live in `RunMode/Unity/**`, which
`Tests/run_tests.sh` deliberately excludes as game-coupled. They are play-test
verified — see the alpha26 task in `HANDOFF_WINDOWS.md`.
