# Act VI gets a voice — the lantern-keeper, and the Borrowed Light

Written 2026-10-05, after Acts II–V were voiced the same day. Act VI, *A Light to Carry*, was a
five-step stand-in; it is reached only when `runFinalBossKey` is moved past Yagluth.

## Why

The bible gives Act VI the saga's planned payoff: the dvergr carry light in lanterns, borrowed and
returned — the only ones who got it right — and the rescued lights the player has pocketed since
Act I's deer hunt are the same kind of thing. The player has been carrying light honestly all along,
before anyone taught them. Nobody in the act said so.

## Decisions taken (owner, 2026-10-05)

| Question | Answer |
|---|---|
| Who carries the act | **A dvergr lantern-keeper** |
| His ask / gift | **Give one light back, get a lantern** |
| A LANTERN track | Not yet — after someone has played the Mistlands |

## The lantern-keeper

A dvergr, in the game's own `Dverger` body, tame. The first speaker who is **alive** and not of the
put-away. Unnamed.

- **Where:** the nearest dvergr site in the Mistlands. Location names are unverified: a list of
  likely ones is tried (nearest found wins), the run-start location registry gains "Dvergr" and
  "Mistlands", and if none resolves he stands on dry Mistlands ground near the player instead —
  so he can never be unfindable.
- **Wanted:** from `mi-arrive` done until the run ends.
- **First words (meaning):** his people borrow light and give it back; it is the only way it has ever
  worked. Then he sees what the player carries: *you've been carrying them honestly since the
  meadows, and nobody taught you.* `LanternFound` → `mi-lantern`.
- **His ask — set one free:** **at night**, give him one light: a rescued light carried since Acts
  I–II, **or** a wisp caught in the Mistlands (`$item_wisp`). Either works, because not every player
  still holds a rescued light by Act VI — Thor's bow and the Stormward spend them — and an ask that
  cannot be paid stalls an act. By day he says light only rises in the dark. Paid, he lets it go: a
  light rises at his side into the mist (`StolenLights.Release`) — borrowed, returned.
  `LightFreed` → `mi-free`.
- **What he gives:** the recipe for **the Borrowed Light** (below).
- **After the Queen:** one remark — the lanterns are lit again under the mist.

## The Borrowed Light

A saga item cloned from the game's wisplight (`Demister`), so its mist-clearing equip effect comes
with it unchanged. What it adds: **it also casts real light** — while it is equipped the host keeps a
warm point light on the player (range ~12 m), removed when it is unequipped. Made at the **galdr
table** (`piece_magetable`) for 5 wisps and 12 silver, taught by the keeper (`RequiresStepDone =
"mi-free"`). The name is the act's whole idea: no light is owned, only carried.

## The chain

| # | Track | Step | Kind |
|---|---|---|---|
| 1 | CRAFT | Claim Yagluth's power | `mi-power` |
| 2 | CRAFT | Reach the Mistlands | `mi-arrive` |
| 3 | CRAFT | **Speak with the lantern-keeper** | **new** `mi-lantern`, PlayerEvent `LanternFound` |
| 4 | CRAFT | **Set a light free** | **new** `mi-free`, PlayerEvent `LightFreed` |
| 5 | CRAFT | **Carry the Borrowed Light** | **new** `mi-borrowed`, CollectItem |
| — | HUNT | Thin the nests → the Queen's lair → the Queen | unchanged |

**5 → 8 steps.**

## The code

- `SagaNames.LanternFound`, `LightFreed`; `StepPredicates.Lantern*`; tests.
- `LanternKeeper` on `SagaSpeaker` (`BodyPrefab => "Dverger"`), with a palette that keeps him a
  dvergr — warm lantern-gold light, colours largely untouched.
- `SagaItems`: the Borrowed Light clone. `SagaRecipes`: its recipe.
- `RunService`: chain edits; `PollLanternKeeper` (the release on payment, at night only); the
  equipped-lantern light; validators (spawn events, the price as alternatives, registry keywords).

## Verification in the first run

The dvergr site names (registry); `Dverger` spawning tame and staying put; `Demister` cloning with
its effect intact; `piece_magetable` as the galdr table's prefab (the recipe validator).

## Not in this work

A LANTERN track; Acts VII–VIII.
