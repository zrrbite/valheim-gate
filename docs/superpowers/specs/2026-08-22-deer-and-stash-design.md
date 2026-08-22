# Eikthyr's Herd, and a stash that follows you

Written 2026-08-22, shipped in `0.221.12-run.alpha30`. Both were designed and
approved in the alpha28 session and deferred twice so the act content got played
first.

## The deer

The brief was "we should focus more on DEER in Act I since Eikthyr is the deer
god — aggressive deer? larger deer? deer with abilities?"

**The constraint found first: deer cannot be made to attack.** They run
`AnimalAI`, which has no attack at all, and giving them one is Unity asset work
this build cannot do. Everything here works around that rather than against it —
deer become harder to *catch*, and killing one becomes an event. The danger comes
from what the noise attracts.

All of it is confined to **Act I**. Eikthyr's deer are his; starring every deer
for the rest of the saga would turn a piece of Act I character into a permanent
tax on hunting.

### Starred deer

`Character.SetLevel` is how the game itself makes a starred spawn — visibly
larger, several times the health, and faster. One arrow becomes a chase.

Two guards matter more than they look:

- **Only at full health.** `SetLevel` recomputes max health, so starring a deer
  the player was halfway through killing would heal it, or appear to.
- **Rolled once per deer.** A deer that lost the roll would otherwise be rolled
  again every second until it won, which makes the chance meaningless. The marker
  is a set of `ZDOID`s rather than a flag on the `Character`, because a Character
  is destroyed and recreated as its zone unloads while the ZDOID is stable.

### The Herald

A named two-star deer, spawned for its own questline step at Act I #14 — between
the deer hunt and Eikthyr.

The interesting problem: **the Herald is an ordinary `Deer` wearing a name**, so
a `KillPrefab "Deer"` step would be completed by any deer at all. So the host
matches it by **ZDOID identity** and, when that specific creature dies, reports a
synthetic name — `EikthyrHerald` — which is what the step actually matches.

That synthetic name would fail the alpha27 asset-name validator, correctly
reporting that no such creature exists, so `SyntheticCreatureNames` exempts it.
One exemption, documented at both ends.

Its ZDO is **non-persistent**, like the Packbrother wolves: a Herald surviving a
reload would litter the world. The poll re-spawns it whenever its step is current
and none is standing, which makes it self-healing across a logout, a zone unload,
or a player who wandered off and left it behind.

### Contested kills, and lightning

A deer death in Act I may draw greylings to the carcass — a **chance**, not a
certainty, because an ambush every single time stops being tension and becomes a
tax on hunting.

Lightning is pure flavour, and the one place here that touches an unverifiable
asset name. It tries several candidate prefabs and settles for none of them
rather than failing loudly on every kill: flavour that quietly does not happen is
acceptable; a log line per deer is not.

## The stash

The brief: *"it would be fun to have universal storage since we have to leave our
house behind to go to Act x, so we don't have to carry everything."*

A chest cannot follow you, so the stash is not a chest — it is **run state**,
reachable wherever the run window opens.

### Deliberately not an inventory

No grid, no weight, no slots, no drag-and-drop. One button puts every material
in; one button per kind takes it out. Everything that makes an inventory
interesting — space pressure, what to carry — is the decision the stash exists to
*remove* for stored goods and *preserve* for carried ones. Making it a second
inventory to manage would reintroduce the chore it removes.

### What moves

**Materials only** (`ItemType.Material`, a compiled enum, so no asset names).
Food, arrows, tools and gear stay put. A button that emptied your quiver and your
dinner into a box you cannot reach in a fight would be a trap, however consistent
it was.

Equipped items are skipped outright — pulling something out from under the equip
state is a class of bug worth not having.

### Identity, and what is not kept

Quality and variant are part of an entry's **identity**, not extra data: a
level-3 axe and a level-1 axe are different objects, and merging them would hand
back two of whichever was written last.

Durability is deliberately **not** stored. A withdrawn tool comes back at full,
which is a small gift rather than a loss, and the alternative is persisting
per-instance state for every stacked item to avoid an edge case nobody would
notice.

### Order of operations

Two places where the sequence is load-bearing:

- **Deposit before remove.** If the stash refuses a stack (full, and this is a
  new kind), the item must stay in the inventory rather than being removed into
  nothing.
- **Grant before withdraw.** A prefab that no longer resolves leaves its entry in
  the stash rather than quietly deleting the contents.

The item list is snapshotted before anything is removed — mutating an inventory
while walking its own list is the same hazard Windfall's doubling has, in
reverse.

Stash actions are deferred to the next IMGUI Layout pass, like the abandon
button: both mutate the list the section is walking, and doing that mid-pass
corrupts the layout stack for every window drawn afterwards.

## Testing

332 assertions, up from 305. `RunStashTests` covers merging, quality/variant as
identity, the all-or-nothing cap, partial withdrawal, emptied-kind removal, round
trips, and malformed-save salvage (short lists, nulls, zero counts, duplicate
kinds).

`DeerHerd` and the stash UI live in `RunMode/Unity/**`, which the harness
excludes — play-test verified, see the alpha30 task in `HANDOFF_WINDOWS.md`.

## Balance

Act I is 15 steps now (the Herald), so questline heat across the saga is **45**.
Unchanged in kind from alpha29's note: still far steeper than anything played,
still on a heat model that owes its tuning pass.
