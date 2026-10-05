# Act III gets voices — the drowned one, the Bog Witch, and the Stormward reforged

Written 2026-10-05, agreed with the owner the same afternoon as Act II's
(`2026-10-05-act-two-voices-design.md`), whose shape this follows and whose machinery
(`SagaSpeaker`, the trader voice) it reuses.

## Why

The same diagnosis as Act II, applied before anyone had to play it thin: Act III has no speaker,
a plain-recipe Stormsworn piece, two chores, and its story in four opening lines. The act's
answer to the shortage is **never let go** — the swamp hoards its dead's light, and Bonemass is
everything that never let go. Its `ChapterClose` stays as written: *the first hoard you had found
that nobody was using.*

## Decisions taken (owner, 2026-10-05)

| Question | Answer |
|---|---|
| Who carries the act | **The Bog Witch + one new speaker (a draugr)** |
| Item quests | **Both, one per speaker** — the draugr teaches the Stormsworn cuirass; the Bog Witch reforges the Stormward |
| The draugr's ask | **Help him let go** — he asks to be put down, and leaves a light |
| The Bog Witch's thread | **Preparation** — Bonemass is decided before the fight; a real mead step |
| The cartography table step | **Cut** |
| A hard gate on Bonemass | **None** — the witch warns; the altar does not wait |

## The story

| Beat | What the player learns |
|---|---|
| The drowned one | The dead here cannot let go — and one of them wants to |
| The Bog Witch | The one living thing in the marsh, because nothing keeps hold of her |
| Letting him go | The swamp can give back what it holds; a light rises where he falls |
| Bonemass (exists) | Everything that never let go, standing up |

### The drowned one

A draugr who kept his wits. **Unnamed.** The first speaker NOT in the Ghost's body: he wears
the game's own `Draugr`, because in this act the dead are not ghosts — they are bodies that
would not lie down.

- **Where:** at the sunken crypt nearest the player, from the moment `sw-scrap` is the current
  step (the crypts are where the scrap is). He stands in the shallows if there is no dry ground —
  his spot search accepts ankle-deep water, unlike the other speakers'; he is drowned already.
- **Look:** pale, waterlogged, a cold green-grey glow; never mistaken for a hostile draugr at a
  glance. Dressed via `ApplyWhenSettled`.
- **Tame** while he talks: he never fights and nothing fights him.
- **First words (meaning):** he has held this door since before the iron rusted, because holding
  is all the marsh lets anyone do. What he wore is no use to him now. **Speaking to him teaches
  the Stormsworn cuirass** (its recipe gate moves from `sw-ironbar` to `sw-drowned`; the cuirass
  still needs an improved forge and 12 iron, so the iron step still comes first in practice).
- **His ask, once the cuirass is made:** *put me down.* He is no longer tame, raises his weapon
  once out of habit, and stands at a fraction of his health. His death — matched by his own ZDO
  id, like the Herald's, never by species — completes `sw-letgo`.
- **A light rises where he falls** (`StolenLights.Release`, the Gatherer's freed-light path): the
  one he never let go of. Lights stay an Act I–II currency as SINKS; this is a source, so nothing
  can stall on it.
- **The swamp does not take him back.** His death is checked BEFORE `FenWatch`, and he is exempt
  from its chance to rise as bone — the one thing in the act the marsh lets go.

### The Bog Witch

The game's own swamp trader, given a voice for Act III exactly as Haldor is for Act II.

- **Idle talk:** nothing here rots because nothing here lets go — the marsh keeps everything,
  even its smell. She lives here because nothing keeps hold of her. Bonemass is decided before
  the fight: poison, and mead against it. Her shop (greet, buy, sell) stays her own.
- **Found:** `sw-witch` completes when the player comes within a few metres of her while it is
  live (her voice reports it; she has no "speak" of her own to hang a step on).
- **The reforge:** **alt-use on her** (Shift+E, the chord the thane's respec uses) with the
  **Stormward, 10 iron and 10 ancient bark** in the pack. She takes all three and hands back the
  **Ironbound Stormward**. Plain Use still opens her shop. The bible's plan — "Act III puts that
  shield back on the Storm-Anvil with iron and ancient bark" — is honoured in materials and
  moved to a person, which is the owner's call: the act should have somebody at the work.

### The Ironbound Stormward

A new saga item, a heavier Stormward. Same lightning-on-block behaviour, same two-blocks-in-five-
seconds discharge; the storm still eats it.

| | Stormward | Ironbound |
|---|---|---|
| Block | 60 (+8/level) | 90 (+10/level) |
| Durability | 300 (+60) | 500 (+80) |
| Discharge lightning | 26 (+5) | 40 (+6) |
| Discharge blunt | 12 (+2) | 20 (+3) |
| Wear per discharge | 12 | 8 |
| Mesh | `ShieldFlametalTower` | `ShieldBlackmetalTower` (fallbacks as the Stormward's) |

Numbers are first picks, tuned in play. It is a Stormsworn piece for set-count purposes exactly
as the Stormward is (same hand slot).

## The chain

### MARSH — preparation

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Claim the Elder's power | StatDelta | `sw-power` |
| 2 | Build a fermenter | BuildPiece | `sw-fermenter` |
| 3 | **Find the Bog Witch** | PlayerEvent `WitchMet` | **new** `sw-witch` |
| 4 | **Stir a poison mead base** | CollectItem `$item_meadbasepoisonresist`, 1 | **new** `sw-mead` — the BASE, because the finished mead is already handed out by `sw-arrive`'s and `sw-cull`'s rewards and would complete the step on arrival |
| 5 | Sail the fens | StatDelta | `sw-sail`, kept |
| 6 | **Reforge the Stormward** | CollectItem (Ironbound) | **new** `sw-reforge` |

### CRAFT

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Reach the Swamp | ReachBiome | `sw-arrive` |
| 2 | Haul out 20 Scrap Iron | CollectItem | `sw-scrap` |
| 3 | **Speak with the drowned one** | PlayerEvent `DrownedFound` | **new** `sw-drowned` |
| 4 | Iron enough to stand in (10) | CollectItem | `sw-ironbar` |
| 5 | Rivet the Stormsworn cuirass | CollectItem | `sw-storm`; recipe now taught by him |
| 6 | **Let him go** | KillPrefab (synthetic `__the_drowned`) | **new** `sw-letgo` |

### HUNT — unchanged

Clear the mire → fell an Abomination → find Bonemass's altar → defeat Bonemass.

### Cut

`sw-chart` (cartography table). **13 steps → 16**: three more completions, so Act III's
questline heat rises by three steps' worth — intended, the act was the thinnest in heat as well.

## The code

### `TraderVoice` — `HaldorVoice` generalised

`HaldorVoice` (written today, not yet played) becomes a configuration of a `TraderVoice` class:
match (name token or object name), talk lines (and the after-told variant), an optional give
entry, an optional **proximity event** (for `sw-witch`), and an optional **alt action** with its
own hover line (for the reforge). Haldor and the Bog Witch are two instances.

**The alt action** is a child trigger object on the trader carrying our own
`Interactable`/`Hoverable`: plain Use delegates to the trader's own `Interact`, alt-Use runs the
action, and the hover is the trader's own text plus one line. Whether our child collider wins
the hover raycast over the trader's own is the one play-verified risk; logged when the voice
attaches, and the fallback is a Use-with-item ask like Haldor's.

### `SagaSpeaker` — two small extensions

- `Prefab` becomes virtual (Ghost by default), so the drowned one wears `Draugr`.
- The waterline for spot search becomes virtual, so he may stand in the shallows.
- A hook to **untame** the current body and set its health fraction, and to ask whether a dying
  `Character` is the current body (ZDO id).

### `DrownedOne` — new, on `SagaSpeaker`

Phases `Speak | Wait | LetGo | Gone`. Spot: nearest `SunkenCrypt4` location (name logged and
validated at run start), ring search around it. In `LetGo` he is spawned untamed at 35% health,
and re-spawned that way if the player walks off and back. `OnCharacterDied` returns the
synthetic kill name when it was him.

### `RunService`

Chain edits; the cuirass gate; `PollDrownedOne` and the Bog Witch's voice in Act III; the death
hook checks the drowned one before `FenWatch` and releases his light; validators (spawn events
`WitchMet`/`DrownedFound`, the reforge price, the synthetic kill name in
`SyntheticCreatureNames`); a run-start **location registry** line for every location whose name
contains "Sunken", "BogWitch" or "Vendor" — so the guessed names are confirmed by the first run.

### `SagaItems` / `SagaRecipes`

The Ironbound Stormward clone (stats above). No recipe: the witch's alt action makes it.

## Verification before or during the first run

1. `SunkenCrypt4` — the location name (the registry line).
2. The Bog Witch's `m_name` and object name (the voice's "Trader in range" line).
3. `MeadBasePoisonResist` and `ElderBark` — item names (the price validator).
4. Ancient bark needs at least a bronze axe (ancient trees' tool tier) — reachable in Act III;
   confirm in play.
5. The alt-use child collider winning the hover over the trader's own.

## Testing

- **Pure:** predicates for the drowned one's phases and the witch's steps
  (`StepPredicates.Drowned*`, `WitchFind`), in `StepPredicateTests`.
- **Run-start validators:** as above.
- **Play:** his spot at a sunken crypt; tame, then hostile on cue; the light; FenWatch not raising
  him; the witch's lines, the proximity step, the reforge flow and its hover; the cuirass taught
  on speaking.

## Documentation

Story bible glossary (the drowned one, the Bog Witch in the saga, the Ironbound Stormward),
Act III's "as built" paragraph; atlas regenerated and republished; RESUME and a HANDOFF task.

## Not in this work

- Moving Act I's speakers onto `SagaSpeaker`.
- A gate holding Bonemass's altar on preparation.
- Boats beyond keeping `sw-sail` — their own design when it arrives.
