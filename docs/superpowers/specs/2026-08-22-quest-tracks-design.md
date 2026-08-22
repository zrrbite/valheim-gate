# Two questlines, and heat as a dial

Written 2026-08-22, shipped in `0.221.12-run.alpha32`.

Owner: *"I think the kill quests could be a separate questline, so there are two
main quest lines. Kill quests and build quests/whatever."*

And, on choosing what happens when you rush the boss: *"The good thing about dual
paths is that you can decide if you want the heat."*

That second remark is the design. It is not a UI change with two rows — it makes
**difficulty a thing the player steers**.

## What the split actually buys

One chain forced an order: to reach the next kill you had to build the next
building. Two tracks let the player choose which thread to pull — and since every
questline step pays heat, and heat drives enemy damage and level-up rate, that
choice *is* the difficulty dial:

- Pursue both tracks → more rewards, more heat, harder enemies, higher score.
- Rush the boss down the hunt track → fewer rewards, less heat, safer, lower
  score.

That reframes something flagged as a defect in the previous build. The later acts
are kill-heavy, so their craft tracks are short — Act IV's is two steps. With heat
as a dial, a short craft track simply offers **less optional heat in that act**
rather than being a hole in the content.

## The engine

`QuestTrack` — an id, a label, a chain, an index and the step in play. The engine
holds a list of them instead of one chain, and each advances independently.

Slot addressing generalises: `TrackSlot(i) = -1 - i`, so track 0 lands on the
historical `MainQuestSlot = -1` and the addressing a single questline used still
means the same thing. Every other negative index keeps its "ignored" behaviour.

Two details worth keeping:

- **Tracks are COPIED by `SetTracks`.** The act table is content, and content must
  be replayable; advancing a run must not mutate the definition it came from.
- **`Tick` advances tracks by index, not `foreach`.** The `Completed` handler is
  host code that can legally call back into the engine, and a collection modified
  during enumeration would throw.

`SetMainChain` survives as a shim installing one unnamed track. It is used only by
tests — but those are ~30 assertions of proven single-chain behaviour, and keeping
them green while the shape changed underneath is exactly what they are for.

## The split is computed, not hand-written

`Split()` cuts an act's steps by `Kind`: `KillPrefab` to HUNT, everything else to
CRAFT, preserving relative order within each.

Splitting rather than maintaining two hand-written lists is deliberate. The acts
read better written as one ordered narrative, the seam is a property of each
step's Kind rather than an editorial decision, and a step added later lands on the
right track without anyone having to remember to put it there.

```
ACT I    HUNT  boar, greylings, deer, Herald, Eikthyr
         CRAFT axe, hammer, bench, roof, fire, cooking, settle, bed, sleep, chest
ACT II   HUNT  greydwarves, brutes, troll, Elder
         CRAFT arrive, mine, smelter, bronze, portal
ACT III  HUNT  draugr, blobs, leeches, Bonemass
         CRAFT arrive, fermenter, iron
ACT IV   HUNT  wolves, drakes, golems, fenrings, Moder
         CRAFT arrive, silver
ACT V    HUNT  fulings, squitos, lox, berserkers, Yagluth
         CRAFT arrive, windmill
```

## The act still ends on the boss

Unchanged, and deliberately so. The boss lives on the hunt track; the act flips on
the world's defeated-boss count; an unfinished craft track is simply unfinished.

Nothing new is persisted for this, the act rule stays derived-from-world, and
rushing carries a real cost — which is the dial working as intended.

## Migration

New per-track parallel lists in the save. An older save carries one `mainQuestId`
from when there was one questline, and that id could now belong to **either**
track, since the split cut the old chain in two.

So it is looked up across every track's chain and seats whichever track owns it,
leaving the other at its start — the same id-over-index principle the single chain
already used, one level up. Restoring the other track to zero rather than guessing
is the conservative direction: the player repeats a step at worst, whereas a
guessed position risks seating a step whose target is already met, which fires an
unearned completion — rewards and all — on the first tick.

The legacy fields are still written for one build, so a save made here and read by
an alpha31 binary still finds its first track where it expects it.

## HUD

Both rows always shown, under the act banner. A thread you must press a key to see
is one you forget you have, and a dial you cannot see is not a dial.

The completion flash became **per track**. A single shared timestamp would flash
both rows whenever either advanced — exactly the wrong signal when the point of
two tracks is telling them apart.

An exhausted track says `done` rather than vanishing: a row that disappeared would
read as a bug, and for a craft track "finished early" is real information.

## Validation

`ValidateActs` now checks step ids unique across **every track of every act** — the
resume-seats-the-wrong-step hazard is two-dimensional now — that each act has a
hunt track ending on a kill, and that no build category is used by two acts.

A craft track may legitimately be short or empty; a hunt track that does not end
on a kill cannot end its act, so that one is an error.

## Testing

359 assertions, up from 332. `QuestTrackTests` covers independence between tracks,
slot addressing, per-track completion events, exhaustion, id-preferred restore,
unknown-track tolerance, and that the single-chain shim still behaves.
`ActDefinitionTests` was rewritten for tracks.

Verified against the built source: every act's hunt track ends on its boss, no
duplicate step ids, no build category shared between acts.

Heat is unchanged in total — the same steps, redistributed.
