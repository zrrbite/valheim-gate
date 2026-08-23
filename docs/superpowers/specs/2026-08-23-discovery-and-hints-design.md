# Discovery steps, a Herald worth hunting, and hints

Written 2026-08-23, shipped in `0.221.12-run.alpha36`.

Owner, on playing alpha35:

> *"The Herald step is truly great, but you just get handed every step without any
> work. Is it possible to add a discovery step before a mini boss and boss? To make
> the acts truly feel special."*
>
> *"Can we do quest text?"*

## 1. `ChallengeKind.DiscoverLocation`

`Param` names a generated location — the same strings the boss table already holds
(`Eikthyrnir`, `GDKing`, `Bonemass`, `Dragonqueen`, `GoblinKing`). The host polls
`ZoneSystem.FindClosestLocation` and completes the step within **30m**
(`runDiscoverRadius`).

One per act, on the HUNT track immediately before the boss:

```
ACT I   … 3 Deer → Hunt the Herald → Find Eikthyr's altar → Defeat Eikthyr
ACT II  … Kill a Troll → Find the Elder's altar → Defeat The Elder
```

**Why the altar and not a rune stone.** A Vegvisir is the thematically right
answer — it is how vanilla tells you where a boss is — but rune stones are
scattered by luck, and the questline is linear with no skip. A step that a bad map
can make impossible is the trap the boat step already taught us to avoid. The world
generator guarantees one altar per boss, so "find the altar" can always be
finished.

There is a pleasing consequence: **Waystone teleports you to the next undefeated
altar**, so a player holding it can shortcut the discovery. That is a legitimate
use of a boon, and it matches the owner's own earlier ruling that spending a boon
to skip travel is the player's business.

Latched, like built pieces and biomes — the poll sees only what is nearby, and a
set that could shrink would un-find an altar the moment you walked away.

Each discovery step pays what its boss fight actually wants: summoning items and
the relevant mead. Arriving at the altar is the right moment to be handed the thing
you would otherwise have gone home for.

### Validating a location name

A location name cannot be resolved against the game. `FindClosestLocation` returns
false both for "no such location" and for "you are simply far away", so a typo is
indistinguishable from a long walk.

So the validator checks discovery params against the **boss table they come
from** — which does catch the mistake that actually happens: using a boss's
CREATURE name where its LOCATION name belongs. Those differ (`Eikthyr` vs
`Eikthyrnir`, `gd_king` vs `GDKing`) and the pair has caused confusion before.

## 2. The Herald becomes a hunt

Spawn distance goes from a flat 24m to **150–250m**. At 24m you turned around and
it was there: a target delivered, not a hunt.

That alone would be a search rather than a hunt, so it comes with direction:

- Announced on spawn — *"Eikthyr's Herald is abroad — north-east, 180m of here."*
- A **live bearing** under the quest step while it is current: *"Tracks lead
  north-east, 140m"*.

Deliberately coarse — eight compass points, distance rounded to ten metres. It
tells you where to walk without walking you there, and Hunter's Eye takes over at
70m for the last stretch.

The bearing returns null rather than a stale value when the Herald's zone has
unloaded. A direction that quietly stopped updating would be worse than none.

## 3. Hints

`ChallengeDefinition.Hint`, one line under the step saying what it actually needs.

**Written for a specific failure, twice observed.** "Build a smelter" needs
surtling cores from burial chambers; "Settle in" needs a fire as well as a roof.
Neither was discoverable from the objective text, and both cost real time guessing.

**Only where the requirement is not self-evident.** A hint on "Kill 5 Boar" is
noise. Eighteen steps have one — the building steps, the mining steps, the arrival
steps, the fermenter, the portal — and the rest have none. The test for adding one
is whether a player could reasonably not know what to do.

Placed above the reward line, because the reward is what you read when you already
know what you are doing.

## Balance

Five discovery steps take questline heat from 45 to **50** across the saga, and
each pays its boss's summoning items — so the act finales get materially easier to
*reach* while the world gets slightly hotter.

## The HUD is now dense

The pinned quest area can show, per track: label and step, a progress bar, a hint,
a bearing, and a reward. Two tracks makes that up to **ten lines** where the
original design had three.

Not trimmed pre-emptively — it wants seeing in play. If it crowds the timer, the
first thing to go is the reward line once a step has progress, since by then you
have already read it.

## Testing

`DiscoverLocationTests` covers param-scoping, latching against a zero report, and
cross-kind isolation. Verified against the built source: all five discovery params
match the boss table exactly, no duplicate step ids across the saga, and every step
but the final boss has a reward entry.

The poll, the bearing and the HUD are Unity-coupled and play-verified.
