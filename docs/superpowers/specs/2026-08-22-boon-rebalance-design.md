# Fewer stamina boons, four new categories

Written 2026-08-22, shipped in `0.221.12-run.alpha34`.

Owner, on playing alpha33: *"we need more boon types. There are like three sta
ones, and they seem a bit lack luster since we already regen quite fast."*

It was five, not three: Enduring, Vigorous, Cat's Breath, Marathoner and Acrobat —
**five of seventeen slots spent on a problem the run's baseline already solves.**
Every run starts with move stamina ×0.5, regen ×2.5 and all costs ×0.75. Those
boons were competing with that, which is why they felt flat.

## The merge

The five become **Tireless** — +25 max stamina, faster recovery, cheaper dodges —
one boon worth picking. Marathoner's run-drain cut was dropped rather than folded
in, because baseline move stamina ×0.5 already *is* that boon.

The `FieldBoost` table already allowed several rows; only the lookup returned the
first match. It now applies every row sharing an id, which is what lets one boon
lend four fields.

Pool: 17 → 22.

## A gate on offers

Resistances forced this. "Resistant to frost" offered in the Meadows spends one of
only three options on something that does nothing for hours.

`BoonDefinition.MinBosses`, filtered in `CreateOffer` against
`BoonEngine.DefeatedBosses` — the boon pool's equivalent of the challenge pool's
`MaxTier`, which has gated content by world progression since alpha11. The host
derives the count from the world's keys rather than storing it, exactly as the act
index does, so a resume and a run on an already-progressed world both gate
correctly with no new save state.

Gates are set one biome **early** on purpose: being handed the swamp's answer while
finishing the Black Forest is preparation, whereas being handed it in the swamp is
a rescue.

## The four new categories

**Resistances** — `Irongut` (poison, 1 boss), `Coldblooded` (frost, 2),
`Fire-blooded` (fire, 2). These are Valheim's real biome gates: poison mead *is*
the swamp.

**On-kill** — `Bloodthirst` (kills heal) and `Relentless` (kills restore stamina).
The first boons that reward aggression rather than raising a number, and the
cheapest to build: the `Character` death hook already existed for the questline's
kill steps.

Dropped from this category: "extra drops on kill", which needs the corpse's drop
table — asset data, and this project has paid for that mistake enough times.

**Risk** — `Glass Cannon` (+40% damage, −30% max health) and `Reckless` (+50%
damage, +25% damage taken). The first boons that **cost** something. Every other
boon is pure gain, which makes an offer a question of which number goes up; these
make it a decision. Both spell the cost out in the description — a downside the
player did not see coming would be a different thing entirely.

**Heat** — `Slow Burn` (heat rises 25% slower) and `Forge-fed` (weapons hit harder
the hotter the run). Forge-fed is the interesting one: it rewards running hot, so
"work both quest tracks" becomes a build rather than merely harder.

## Two mechanisms that had to be rebuilt

### Weapon damage now composes

Three boons multiply weapon damage — Sharpened, Glass Cannon, Forge-fed — where
before there was one. Each now registers a **factor**, and the damage is always
recomputed from a pristine snapshot as the product of all live factors.

The alternative, each boon scaling whatever it found, is exactly the compounding
bug the original snapshot was written to avoid: two boons applying in an order
nobody controls, and the first unapply stomping the prefab's true original with a
partly-boosted value.

This also fixed a latent flaw: `RefreshWeaponDamage` runs on the poll tick, so a
weapon crafted *after* the boon was taken is now covered. Sharpened silently
missed those before.

And it is what makes Forge-fed safe. Recomputing from the original means its
multiplier can move with heat without ever ratcheting.

### One damage-modifier snapshot, not one per boon

`Character.m_damageModifiers` is a single struct, so two boons each restoring
"their" version would put back whichever ran last and silently discard the other.
The pristine copy is kept once, the live value recomputed from it, and each boon
just declares itself a claimant.

Reckless uses the game's own `Weak` modifier rather than an invented percentage —
a value Valheim already balances around.

## Heat changes go through one path

`AddHeat` / `RemoveHeat` / `OnHeatChanged` replace four hand-written pairs of
"change the number, then push it to the world". Slow Burn needed a single place to
discount gains, and Forge-fed needed a single place to re-scale.

This is the alpha33 lesson applied before it bit: **two copies of the same
sequence will drift.** Restoring saved heat on resume deliberately does *not* go
through `AddHeat` — that is a restore, not a gain, and Slow Burn must not discount
it.

## A silent failure caught while building

`FirstBoonPin` — the designed opening pick — still named `"enduring"`, which the
merge deleted. The engine ignores a pin it cannot resolve, by design, so this
would have quietly un-steered every opening offer forever with nothing in the log.
Now `"tireless"`, with a note that the pin fails silently.

## Testing

`MinBosses` gating is pure and unit-tested: never offered early, offered at the
threshold, does not shrink an offer while ungated options remain, opens mid-run
when the count rises.

The effects live in `BoonEffects` (Unity-coupled, play-verified). The invariant to
watch is the one the mode has always had — **power is loaned** — so every new boon
must leave no trace when the run ends. Weapon damage and damage modifiers are both
written into shared, per-prefab state, so both are unwound per-boon *and* again
wholesale in `UnapplyAll`'s finally block.

## Not built

**Berserker** ("damage rises as health falls") was the obvious fourth risk boon.
It needs continuous re-application of weapon damage, which is the landmine that
has cost this project builds before. Forge-fed is the same shape but moves on a
discrete event instead of every frame — that is the line.
