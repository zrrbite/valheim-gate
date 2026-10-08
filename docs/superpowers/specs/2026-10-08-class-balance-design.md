# The ways, balanced — design

2026-10-08. Branch `feature/run-mode`. Builds on `2026-09-27-classes-design.md` (what a way is, why ways are
boons, the ladder by boss count, the keys). **Build after the owner has played `...08g`/`...08h`**: the rule
since 2026-10-06 is not to build saga features faster than they can be tested.

**What the owner approved, and how.** The Hunter, the Sæfari, the Völva and the Berserker were each presented
in conversation and approved ("yes", with the Sæfari's land Ward made one tier weaker). Everything from the
Húskarl on was approved unseen ("yes to the rest"). **Read those sections first when reviewing.**

## Why

From the class review of 2026-10-08, and the owner's own words:

- **"Wolves are extremely powerful. The other classes, like healers, should feel the same."** The Hunter fights
  with two wolves and a Menagerie beast at her side the whole time. Four ways have nothing that stays.
- **Why the wolves are so strong is a bug, not a number.** The Hunter's passive, Shepherd, is the GM mod's
  "buff tamed" (`CheatCommands.BuffAllPets`). It sets every tame's health to **5000** (a wolf has 80). It also
  copies the strongest tame's weapon damage ×1.2 onto every tame's attack **through `m_shared`**, which
  `ItemData.Clone` does not copy (verified in the IL). So every wolf and boar in the world, wild ones
  included, bites harder while a Hunter holds Shepherd.
- **"If I spawn wolves in the meadows they should have no stars. Maybe they will get stars as we progress."**
  The code already gives a summon one star per god (none in the Meadows). The owner's picture is right; Shepherd
  is what overrode it.
- **The way stops growing in Act II of eight.** The ladder's last rung lands with the Elder (thresholds
  `{0, 1, 2}` since 2026-10-05). Bonemass to Fader teach the way nothing.
- **Two ways have no answer to a fight.** The Sæfari and the Smiðr are utility only; the saga sends bosses,
  raids, the Gatherer, the Breaker and now the sea.
- **The passives are skill floors.** "Axe skill to 50" is invisible (owner, as a Berserker: "I wasn't quite sure
  what the class benefits were").

Already done in `...08h`: the wheel no longer deals a card a held boon wholly covers (`BoonDefinition.CoveredBy`).

## The yardstick

**About two allies of the current tier, always present** — the Hunter's pack once Shepherd is honest:
two one-star wolves (≈320 health fighting for you) in the Meadows, two two-star wolves (≈480) from Act II.
Healing and sustain are measured over **a two-minute fight**: a way that restores ≈320 health to its side over
two minutes in the Meadows matches the pack. Every way gets one **always-on engine** in its own idiom, and
one **short-cooldown verb** to press.

## Shared rules

1. **The star rule, for every summon** (wolves, skeletons, Menagerie beasts): none in the Meadows, one per god,
   **two at most** (level 3, the game's own ceiling: a star doubles health then triples it, and adds +50%
   damage each). The Hunter's Shepherd star counts toward the same two.
2. **Buffs to a creature are per creature, never through `m_shared`.** `m_shared` is one object for every copy
   of an item; writing it changes the wild ones too. This is the Shepherd bug, restated as a rule.
3. **Everything is a loan.** A star or a stat given to a *persistent* tame (the hearth boar) carries a ZDO mark
   naming it ours, so it comes off at run end or when the boon is lost — after a reload too.
4. **The retinue cap stays four** across all summoning boons (`BoonEffects.MaxCompanions`).
5. **No new keys.** The laptop layout has no letter left. Rungs keep `U I O` / `Keypad 7 0 Insert`.
6. **The card says what the kit does.** Each way's `Description` (THE WAY card, the thane's voice) and its BOOK
   line are rewritten to match, promising nothing the kit does not do.
7. **No spoilers** (owner's standing rule): a tempering line names its god the way the ladder's `AfterLine`
   already does, and nothing the story has not told.

## The Hunter (approved)

Her engine is **the pack**.

- **Shepherd becomes one star**: every animal on your side, tamed or summoned, gets one star. No 5000 health, no
  copied bite. The speed match ("keeps up with you") stays; it was already per animal.
- **Summons follow the star rule**: in the Meadows, two one-star wolves (160 health each); from Act II, two
  two-star wolves (240 each), the ceiling.
- **Menagerie's roster grows by biome**: boar, hen, chicken from the start; **wolf** after Bonemass; **Lox**
  after Moder; **Asksvin** after the Queen. No more rerolling to a Lox (1000 health) in Act II. Same star rule.
- Unchanged: Packbrother (two wolves, 240 s, they stay), Elemental Arrows, Unseen (20 s, 150 s), bow and sneak 50.

## The Sæfari (approved)

Her engine is **her Ward, which she carries with her**. Coins and fittings become her strength, everywhere.

- **Passive, Seafarer:**
  - **Fittings cost her half** (Sail I 25, Wind-horn 100, Ward III 225; the full Ward ladder is 400 for her).
  - **Her ship sails a tier above what is fitted** in Sail, Hull and Ward (a bare raft has Ward I).
  - **The Ward walks with her on land, one tier weaker than at sea, never below I:**

    | Ward bought | at sea | on land |
    |---|---|---|
    | none | I | I |
    | I | II | I |
    | II | III | II |
    | III | III | II |

  - **The sea pays her double** (sea creatures' coins).
  - **Swimming never tires her** (Tide-borne's effect, now always on). Swim 60, spear 50 stay.
- **Rungs:**
  - Choice: **Undertow** (20 s), her verb. A wave from her staggers and throws back everything within 6 m and
    leaves it Wet; at sea it also strikes what is in the water around her ship.
  - After Eikthyr: **Stormcaller** (120 s), replacing Fair Wind (the Wind-horn does that job, at half price for
    her). Twenty seconds in which her Ward strikes every second instead of every three, at twice the radius.
  - After the Elder: **Sea Legs**, unchanged (5 min free of cold and wet).
- Gift: the abyssal harpoon, unchanged.
- Against the yardstick: weaker than the pack early (Ward I on foot), very strong once paid in.

## The Völva (approved)

Her engine is **a real healing aura**, and her dead come first.

- **Passive, Hearthlight**: heals you and every ally within 15 m **3 per second in the Meadows, +1 per god, to
  8** (it was 4 health every 5 s). ≈360 over a Meadows fight, per ally.
- **Rungs, reordered:**
  - Choice: **Bonecaller** (moved up; cooldown 180 → 120 s). Two skeletons that stay, on the star rule, without
    the Shepherd star.
  - After Eikthyr: **Mending**, unchanged (the dverger healing staff's own burst, 90 s).
  - After the Elder: **Thor's Wrath**, unchanged (6 m, 60 s, +25% per god to triple).

## The Berserker (approved)

His engine is **Fury**: stronger the longer he keeps hitting, and healed by it.

- **Passive:**
  - **Fury**: every hit that lands, +5% weapon damage, to **+50% at ten**. One hit's worth fades per second
    without a landed blow.
  - **Bloodied**: at five or more, every hit heals him 10% of the damage it deals (≈3 per second with a flint
    axe in a real fight — the Völva's aura, earned).
  - Axe, sword and club 50 stay.
- **Rungs, feeding the engine:**
  - Choice: **Rend**, unchanged (20 s); every foe it hits counts as a Fury hit.
  - After Eikthyr: **Blood Rage** reworked: it fills Fury and **holds it full for 15 s**; he takes 25% more while
    it lasts. It is no longer its own ×1.5 multiplier.
  - After the Elder: **Warcry**, unchanged (stagger within 8 m, 90 s), and it fills half his Fury.
- Gift: Ulfr's axes, unchanged.

## The Húskarl (approved unseen)

His engine is **Guard**: what lands on his shield comes back to him.

- **Passive, Hirdman:**
  - **Every blocked hit** refunds half the stamina the block cost and heals him 4.
  - **Every parry** heals him 10.
  - Blocking and spear 50, +20 health, stay.
  - In a busy fight ≈2–3 health per second — the aura again, earned by standing.
- **Rungs:**
  - Choice: **Shield Bash**, unchanged (25 s), his verb.
  - After Eikthyr: **Shield Wall**, unchanged (20 s resistant to physical, 120 s), and Guard heals double while
    it stands.
  - After the Elder: **Last Stand**, unchanged.
- Gift: wood shield, flint spear, unchanged.

## The Skald (approved unseen)

His engine is **the song**: one always playing, chosen with the rung keys, for him and every ally within 15 m.

- **Passive, Poet**: run, jump and swim 50 stay. The song is the engine.
- **The songs** (each rung teaches one; pressing its key switches to it):
  - Choice: **Marching Song**: +20% speed, stamina regenerates half again as fast.
  - After Eikthyr: **War Song**: +25% damage for him and his allies (allies through a per-creature status
    effect, never `m_shared`).
  - After the Elder: **Saga of Bragi**: heals him and his allies 3 per second, +1 per god, to 6 (a lower ceiling
    than the Völva's 8: he can switch, she cannot).
- **His verb, the crescendo**: switching songs plays the new one at double strength for 5 s, at most every 20 s.
- The 20-second songs and their cooldowns go. Gift: three minor meads, unchanged.

## The Smiðr (approved unseen)

His engine is **what he builds fights back**.

- **Passive, Craftsman**:
  - **Forge-skin**: his armour counts half again, and his gear never wears.
  - **His walls within 20 m take no weather or support wear** (Reinforce's effect, always on).
  - Woodcutting and pickaxe 50, +100 carry, stay.
- **Rungs, reordered:**
  - Choice: **Watch-post** (new, 120 s): a ballista (the game's `piece_turret`) rises where he stands, loaded,
    and shoots his enemies until destroyed or until he raises another; one at a time; never saved (a
    non-persistent ZDO, removed at run end). Its bolts improve per god (bone, then iron, then blackmetal). His
    answer to a raid, and his "two allies" stand in one place.
  - After Eikthyr: **Field Forge**, unchanged (90 s).
  - After the Elder: **Master's Minute**, unchanged.
  - Reinforce leaves the rungs (its effect is in the passive).
- Gift: hoe and cultivator, unchanged.
- **To verify in the plan**: the turret's ammo (the ZDO's ammo count and type), what it targets, and that a
  turret placed by code is not a player piece the world keeps.

## Tempering: the ladder after Act II (approved unseen)

One tempering per god from Bonemass to the Queen, so every act until the last teaches the way something:
**Bonemass** tempers rung I, **Moder** rung II, **Yagluth** rung III, **the Queen** the engine. Thresholds are
data (`ClassLadder`), like the rungs'.

| Way | Bonemass (rung I) | Moder (rung II) | Yagluth (rung III) | The Queen (engine) |
|---|---|---|---|---|
| Hunter | Packbrother calls three | Elemental Arrows: fire burns and frost slows half again as long | Unseen lasts 30 s | the pack regenerates 2 per second |
| Völva | Bonecaller raises three | Mending every 60 s | Thor's Wrath 9 m | Hearthlight's ceiling 8 → 12 |
| Berserker | Rend reaches half again as far | Blood Rage holds 25 s | Warcry 12 m | Fury to +75% (fifteen hits) |
| Húskarl | Shield Bash staggers everything in front | Shield Wall 30 s | Last Stand 10 s | Guard heals double |
| Skald | Marching Song +30% | War Song +35% | Bragi's ceiling 6 → 9 | two songs at once |
| Sæfari | Undertow 9 m | Stormcaller 30 s | Sea Legs 10 min | the land Ward no longer one tier weaker |
| Smiðr | Watch-post: two at a time | Field Forge stands 3 min | Master's Minute 2 min | Forge-skin: armour doubled |

Pressing three wolves against the cap of four is deliberate: with Menagerie's beast, the Hunter's retinue is full.

## The wheel's tilt (approved unseen)

Each way names four general boons that suit it; for that way their draw weight doubles. The wheel stays Odin's:
nothing is excluded, only tilted.

| Way | Favoured |
|---|---|
| Hunter | Fleet-footed, Farsight, Wayfarer, Bountiful |
| Völva | Hearty, Kindling, Second Wind, Quick Study |
| Berserker | Bloodthirst, Relentless, Sharpened, Reckless |
| Húskarl | Thick-skinned, Hardshell, Hearty, Tireless |
| Skald | Tireless, Fleet-footed, Stuffed, Second Wind |
| Sæfari | Wayfarer, Fleet-footed, Coldblooded, Farsight |
| Smiðr | Woodsman, Packmule, Mending Hands, Bountiful |

## The damage ceiling (approved unseen)

The boons' weapon multipliers multiply (`BoonEffects.RefreshWeaponDamage`): Sharpened 1.2, Glass Cannon 1.4,
Stoker 1.2, Forge-fed, War Song, and today Blood Rage 1.5. Held together, with Fury, a Berserker reaches
≈×4.5. **The product is capped at ×2.5.** Reckless (+50%, through its own path) counts toward it. The HUD's
BOONS page says when the ceiling is reached, so a fifth damage card does not look like a dud for no reason.

## Testing

Pure, in `RunMode/` and `Tests/run_tests.sh`, each written failing first:
- the star rule (gods, Shepherd, the ceiling of two), and Menagerie's roster by god;
- Hearthlight's and Bragi's rates by god; Fury's stacking, fading, and Bloodied's threshold; Guard's refunds;
- the Sæfari's Ward tiers (sea, land) and her prices; tempering by god for every way;
- the wheel's tilt; the damage ceiling.

Game-side, in the home test plan, one check per way: the engine felt in a fight, its verb, and its tempering
where reachable. And one for Shepherd: a wild boar's bite is unchanged while a Hunter holds it.

## Order of work

1. **Shepherd honest, the star rule, Menagerie by biome.** First, because Shepherd's shared-data write is a bug
   that reaches wild creatures today.
2. **The engines**, one way per task, Hunter-relative numbers.
3. **Tempering.**
4. **The wheel's tilt and the damage ceiling.**

## Out of scope, and left to tuning

- Multiclassing (`docs/valheim-classes-plan.md` §9.1).
- Every number here is a first guess against the yardstick; the play-test tunes them, as it did the ladder.
- Multiplayer: the saga is solo.
- The Deep North (Act VIII): no tempering past the Queen.
