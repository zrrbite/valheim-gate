# Classes for the saga — design

Written 2026-09-27 on `feature/run-mode` at `1.0.15-run.2026-09-20aa`, from a brief the owner drafted
with Claude-web (`docs/valheim-classes-plan.md`) and a session that read it against the code. The
brief carries its own "Integration notes" saying what was adopted; this file carries the REASONING,
so the decisions are not re-litigated the next time someone opens the boon pool.

## What a class is here

A **way**: the way of one of the fallen, taken up at their grave. A run may hold one. It gives a
passive at the choice and three abilities on a ladder — the first at the choice, the second once one
god is down, the third once three are — each *available* at its threshold and *learned* by walking
back to the one who teaches it. The saga's frame absorbs it without strain: Valheim is where the
put-away are put, the graves are theirs, and someone who remembers what the dead were in life is not
a god and so may speak.

Classes never touch the light. They do not answer the shortage, they do not raise heat, and they are
not Odin's. That last point is mechanical, below.

## Why classes are boons

The obvious build was a second system: a `ClassEngine` with its own held list, its own effects, its
own save fields, its own keys and its own HUD block. Everything on that list already exists once, for
boons, and each exists because a play-test found a hole (Tireless is granted by a step, `heldBoonIds`
survive a resume, passives are reapplied on respawn, `BoonKeys` is the one table so a key can never go
unlabelled, `UnapplyAll` is the reason a run leaves the character as it found it). A second copy would
have to re-find every one of those holes.

So `BoonDefinition` gains `ClassId`, null for the 21 general boons. Three rules follow, all pure and
all tested in `BoonEngineTests`:

1. **`CreateOffer` never deals a class boon.** The wheel is Odin's.
2. **`RemoveLatest` never takes a class boon.** Death lets the world collect a LOAN; what the thane
   taught was not lent. It takes the newest general boon instead, or nothing.
3. **`Grant` is the only door in.** Same door as `mq-rest` → Tireless. Everything downstream —
   `OnBoonGained`, `BoonEffects.Apply`, save, restore, reapply, unwind — is unchanged and unaware.

The pure half of the class layer is therefore small: `ClassDefinition` (id, display, the fallen one's
name, description, passive boon ids, three rungs of boon ids) and `ClassLadder` (thresholds `{0,1,3}`,
`Due(class, defeatedBosses, held)`). A run-start validator checks that every rung id exists in the
pool with the matching `ClassId`, and that no two boons of one class share a key.

## Why per run, why during the saga

The brief stored the class on the character (`m_customData`) and let it outlive the run. Two things
argue against it here. The saga's invariant is that power is loaned per run and repaid; a class that
persists is the one piece of power that does not. And the saga is replayable — "this time as a
Völva" is worth more than a permanent Hunter. The owner's own framing settled where the choice
happens: "a specialization you pick during the saga, not in the lobby", from a trainer, which is also
the natural place to teach the later rungs. The permanent record may later remember which ways have
finished a saga; that is a title, not power.

## Why saga progression, not a custom skill

The brief injects a `Skills.SkillType` with Cecil and gates unlocks on its level (10/50/75). In the
saga that fights two existing things: skill gain runs at ×3, and skills raised by the run are LOANS,
repaid at run end. It also needs a Patcher change (a full reinstall on every machine) and an icon the
skills window will not throw on. The brief's own pacing note — "level 50 lands ~Bonemass" — says the
ladder is really the boss count, which the saga already derives from the world and already uses to
gate boons (`MinBosses`). So rung 2 is Eikthyr, rung 3 is Bonemass, and there is no new save data.

## The migration

"Everything class-defining moves" (owner). Moved, ids unchanged so a saved run restores: brother,
menagerie, shepherd, hearthlight, unseen, hunter → Hunter; bonecaller, shaman → Völva; warrior →
Berserker. Kept general: `wind` (Second Wind) — Völva would otherwise hold four actives for three
slots, and a heal window anyone can draw is a different thing from a Völva's burst.

| Class | The fallen one | Passive | Rung 1 [7] | Rung 2 [0] | Rung 3 [Ins] |
|---|---|---|---|---|---|
| Hunter | Eydís | `hunter` Bows 50, `shepherd` | `brother` | `menagerie` | `unseen` |
| Völva | Sigrún | `hearthlight` | `shaman` Mending | `bonecaller` | `wrath` (new) |
| Berserker | Ulfr | `warrior` Axes/Swords/Clubs 50 | `rend` (new) | `rage` (new) | `warcry` (new) |

The general pool goes from 30 to 21. Refilling it is a follow-up; the offer still has plenty to draw
from, and the pets and heals were the boons most often reported as "I already have that".

## The other four (2026-09-28)

Added the day after the first three were played (owner: "I tested the first 3 classes a bit. Can we
do the rest?"). Same shape — a passive, three rungs, a gift at the choice — and the same rule: every
effect is a legacy cheat or an existing loan with a cooldown and a reason, or it is not in v1.

| Way | The fallen one | Passive | Rung 1 [7] | Rung 2 [0] | Rung 3 [Ins] | Gift |
|---|---|---|---|---|---|---|
| Húskarl | Halvard | Blocking/Spears 50, +20 HP | Shield Bash (stagger the front) | Shield Wall (20 s Resistant to physical) | Last Stand (6 s at ≥1 HP — the game's own god-mode clamp — then half health) | wood shield, flint spear |
| Skald | Ormr | Run/Jump/Swim 50 | Marching Song (20 s speed + stamina) | War Song (20 s +15% damage) | Saga of Bragi (Rested where you stand, +30% health) | three minor meads |
| Sæfari | Ragna | Swim 60, Spears 50 | Tide-borne (30 s the water cannot tire you) | Fair Wind (60 s wind at the ship's back) | Sea Legs (5 min neither cold nor wet) | abyssal harpoon |
| Smiðr | Dvalinn | Woodcutting/Pickaxes 50, +100 carry | Field Forge (bench + forge at your feet, 90 s) | Master’s Minute (60 s building costs nothing — the `NoBuildCost` world key through `WorldModifiers.SetFlag`, NOT the `nocost` cheat, which would also make every recipe free and every station optional) | Reinforce (10 min no weather or support wear within 20 m) | hoe, cultivator |

Two things decided the shape. The saga is solo, so the Skald's "crew" is you and your companions,
and Sæfari's fishing is NOT lent — the hearth track has a fishing-skill step a loan would complete
by itself. And the card now holds seven, so it picks with Keypad 1–7 rather than 1–3; activation
is already off while the card is up, so the four boon keys are free at that moment.

Three things the IL corrected in the brief, worth knowing: `Player.m_noPlacementCost` is private and IS the
whole `nocost` cheat (free recipes, no stations), so the minute rides the world key instead;
`WearNTear.m_noRoofWear`/`m_noSupportWear` default TRUE and true means the piece TAKES wear, so
Reinforce sets them false; and the legacy `PetBuff` is not a damage boost and has no clean undo, so
War Song sharpens you only. **Ruled by the owner, 2026-09-28: both accepted** — a piece built free and taken down after the
minute refunds its full bill (nothing can mark it without new save data), and Field Forge teaches
the forge to the character the way Farsight teaches the map; Sea Legs strips Wet every frame while rain
re-adds it every physics step, so the icon may flicker.

The thane's opening names seven and calls himself the eighth. Each of the four has a line in his
voice and a past-tense line in the BOOK, like the first three.

## Elemental arrows (owner's idea, 2026-09-28)

> "A skill 'elemental arrow', that lets Thor's bow toggle either lightning (already exists), fire,
> ice. Besides the slow arrow. Being able to toggle the 'ammo' of the bow would be cool."

A Hunter grant at rung 2, beside Menagerie: `elemental`, an active with no cooldown that cycles
Thor's bow between lightning, fire and frost on the arrow keys (unused by the saga and by vanilla
play; the GM mod's arrow bindings are dead during a run). Frost IS the slow — the game's own frost
damage applies its slow — so the "slow arrow" comes free with the element. The toggle rewrites the
bow's own elemental damage (exactly one of the three at the bow’s own 22, +4 per level) and swaps the flash the
in-flight arrows get on hit, taken from the vanilla fire and frost arrows' own projectiles. The
element is run state (`bowElement`) and the bow goes back to lightning at run end, since it
outlives the run and ships as lightning.

## The way as a skill, and laying it down (2026-09-28)

**The mirror.** The owner wanted the way on a skill bar ("I still don't see the class skill under
skills"). The custom-skill design was dropped for good reasons (it would fight the run's x3 skill
rate and the loans), but a READOUT costs none of that and — on a second look — no Patcher change
either: C# lets the mod cast an unused number to `Skills.SkillType`, the Skills window names a
skill from a `$skill_<n>` localisation token the mod can add at runtime, and the player's skill
definitions are a public list the mod can append to, icon included. So one skill, named "The way
of <Title>" with the way's favoured vanilla icon, sits at 33 / 66 / 100 as the rungs are learned,
recomputed from what is held every poll and lent through the existing skill loan so it returns to
nothing when the saga ends. It raises no XP and gates nothing; it is the ladder, drawn where the
player already looks for a bar.

**Respec.** Shift + Use on the thane while a way is held lays it down: the way's boons are given
back, the card reopens, and choosing again grants the new way's due rungs and gifts at once. It
costs **heat, +3** — the saga's own currency, which needs no new item flow, makes the world notice
a name changing hands, and stops idle switching without punishing a real change of mind (the
owner's pick over "free" and "a boss trophy"). Gifts already given stay, since they are items; a
class-only recipe leaves the bench through the class filter. The dev cycle pays nothing.

## Keys

The keypad and the Home/End cluster are full. Moving brother, bonecaller and menagerie into classes
frees Keypad7, Keypad0 and Insert, and a run holds ONE class, so the three rungs of every class bind
to those three keys. Several ids share a key in `BoonKeys.Actives`; the activation handler picks the
held one (today it returns on the first row whose key matched, which would make the Völva's Keypad7
try the Hunter's wolf and stop). `Label()` is keyed by id, so the HUD is unaffected. Keypad+ and
Keypad− are freed as a side effect; the dev layer keeps its modifier on them for now, because the
help text and DEV-MODE.md say so and a help line that is wrong is worse than none.

## The new effects

Four, all shaped like something that exists:

- **`rage`** — the Emberskin shape: a weapon multiplier under lender `"rage"`, a pending off-timer,
  plus a `Weak` damage modifier for the duration so it costs something. Discrete, not per-frame,
  which is the line the boon design drew when it rejected "Berserker: damage rises as HP falls".
- **`rend`** — the `StaggerAoE` iteration with a `HitData` (slash + poison; `RPC_Damage` turns poison
  into the game's own `SE_Poison`, so "bleed" costs no new status code), attacker set so tames and
  stats follow the normal path.
- **`warcry`** — `Character.Stagger` on hostiles within 8 m, skipping bosses: `RPC_Stagger` only
  fires an anim trigger, and a boss may not have one.
- **`wrath`** — the first lightning `HitData` the mod builds; the flash is the Stormward's resolved
  lightning prefab. The prefab must be checked for an `Aoe` component before it is spawned at the
  aim point, or it may deal ownerless damage of its own.

Dropped: `seidr`, an eitr-regen passive. The IL says eitr has no base value and only regenerates when
food gives eitr — Mistlands food — so it would have been a passive that did nothing for five acts.
The runtime `SE_Stats` route is viable (`SEMan.AddStatusEffect(StatusEffect)` clones an instance with
no `ObjectDB` lookup) and is on record for later.

## The thane

Act I's third speaker, after the shade (dark) and Thjalfi (rain). He wants daylight, which makes the
three gates a set. He stands a walk from the claimed bed, not on a shore, and is CREATED only when the
player is near, like the others. He is one of the put-away and uses what ships — the Ghost again in a
third palette through `CreatureDressing.ApplyWhenSettled` — because there is no AssetBundle pipeline
and there will not be one.

Meeting him is a HEARTH-track step (`mq-thane`, directly after `mq-bed`) that completes on SPEAKING,
so declining a way never stalls the chain. The choice itself is the offer card the boon wheel already
draws, with three ways instead of three boons and the same Keypad1/2/3. A rung coming due is announced
by Hugin once, and the bearing to the thane returns while something is due.

Later, each way's trainer may stand somewhere else — a grave in the biome that way belongs to. The
ladder does not care who teaches; that is one field.

## Landmines, restated for this feature

- Every class effect is a loan: `Unapply`/`UnapplyAll` must leave nothing. Fields through
  `LoanLedger`, weapon damage through the multiplier product, damage modifiers through
  `ApplyDamageModifier`.
- Moved boons keep their ids. A pre-class save holding `brother` with no class is legal.
- Actors are not reset by `EndRun` — reset in `StartRun`, build in `BuildActSystems`.
- Asset names are data: log which lightning prefab resolved; never fail silently.
- Dress the thane two frames after spawn (`ApplyWhenSettled`).
- No line the world does not back.
