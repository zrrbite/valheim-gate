# Homeward, health for every completion, and the loan bug underneath

Written 2026-08-23, shipped in `0.221.12-run.alpha35`.

Four notes from playing alpha34, one of which turned out to expose a shipped bug.

## What was asked

> *"We might want to swap the 'build bed' and 'spend 2 minutes in home' since the
> home is not complete yet."*
>
> *"When a boss is downed, the player should get 1 Gate to home (since thats where
> we built our house and crafting stuff)."*
>
> *"Act 2 begins with 'build a smelter' which is a little too soon? Maybe not,
> whats required for a smelter?"*
>
> *"Maybe we should reward the player with a tiny bit of armor and health for every
> quest/task completed since heat is also increased."*

## 1. Bed before settle-in

Correct, and straightforward: the CRAFT track asked the player to settle into a
home they had no bed for. Now `fire → cook → bed → settle in → sleep → chest`.

Step ids are unchanged, and restore-by-id handles the reorder — that path exists
for exactly this.

## 2. Homeward

One charge per boss felled; **Keypad 9** teleports to the player's claimed bed via
`PlayerProfile.GetCustomSpawnPoint()` — which is precisely "where we built our
house and crafting stuff", and is set by the very bed Act I makes you build.

**Not a boon, deliberately.** A boon competes with 21 others for three offer
slots, so most runs would not have it in the act where they wanted it — and "the
trip home is solved" has to be true every run or it is not solved. Waystone
already carries you *to* the next altar on the same charge-per-boss rule; this is
the return leg, which was the one gap left after the stash removed the hauling.

With no bed claimed the charge is **not** spent and the player is told why.
Dumping them at world spawn would cost the charge *and* put them somewhere they
never chose.

## 3. The smelter

Worse than a pacing problem. A smelter needs **surtling cores**, which come from
burial chambers, and the chain handed none over until the *portal* step — two
steps after the smelter. It was quietly sending the player crypt-hunting inside a
step that reads as "build a thing".

The mining step before it now pays 8 cores. Same rule the chain already follows
with deer trophies and ancient seeds: **gate on the fight, never on a scavenger
hunt.**

## 4. Health for every completion

`+2 max health` per completion, questline step or random task alike, accumulating
through the run and given back at the end like every other loaned power. Act I is
15 questline steps plus tasks, so Eikthyr is met around +40 — real, and well short
of a second health bar.

It scales with *how much you do*, which is the same dial the two tracks are: more
completions means more heat **and** more health.

Shown in the HUD next to heat, and only once earned. Showing the cost without the
counterweight would make the trade look worse than it is.

### Armor is not in it

`GetBodyArmor()` is computed from equipped items — there is no player armor field
to nudge — and Valheim's damage-modifier steps (`Resistant`, `Weak`) are far too
coarse for "a tiny bit". So this is the health half done properly rather than a
fudge that pretends to be armor.

## 5. The bug this surfaced

`Hearty` (+15) and `Glass Cannon` (−7.5) **both write `Player.m_baseHP`**, and the
old `FieldBoost` gave each row its own snapshot of "the original":

```
Hearty applies       -> snapshots 25, sets 40
Glass Cannon applies -> snapshots 40 as "original", sets 32.5
Hearty is lost       -> restores ITS snapshot, 25 — wiping Glass Cannon
Glass Cannon is lost -> restores ITS snapshot, 40 — never an original value
```

The player ends the run with more base health than they started with,
**permanently** — exactly what "power is loaned" exists to prevent. That shipped
in alpha34. The per-completion reward would have been a third claimant on the same
field.

### The fix, and why it is the third of its kind

One pristine value **per field**, every lender declaring a contribution, the live
value recomputed as `original + sum(contributions)`. Order stops mattering,
removal is exact, and unwinding is a single assignment.

This is the third time in one day the same correction was needed: weapon damage
(three boons multiplying it), damage modifiers (four boons sharing one struct),
and now player fields. **The original per-lender design was on the wrong axis** —
it should always have been per-field, and the pattern only looked correct while
there was exactly one claimant.

So this time the arithmetic was extracted into `LoanLedger`, a pure class with no
game types, and the part that was wrong is now the part that is tested: composition,
repay-order independence, re-lending without compounding, refusal to re-snapshot
an already-loaned original, and multi-field lenders.

## Testing

405 assertions, up from 384. `LoanLedgerTests` reproduces the Hearty/Glass Cannon
bug as a test and pins the properties that prevent it.

The wiring, Homeward and the HUD are Unity-coupled and play-verified. The
invariant to watch is the one it has always been: **take Hearty and Glass Cannon
together, finish a run, and health must be exactly what it was before.**
