# Class balance: the rulings made while it was built, and what was left (2026-10-08)

The plan (`plans/2026-10-08-class-balance.md`) was run subagent-driven on `feature/run-mode`, as the owner asked:
"subagent-driven, run it now but don't build". Each task got its own implementer and reviewer, and opus reviewed the
whole branch at the end. **Task 13 (build, tag, deploy) did not run.** The plan waits for the play-test of the
current builds; the home test plan's checks 8f–8n are the class balance's own.

This file keeps what the run's ledger held: every decision taken on the owner's behalf, with what it costs if it is
wrong, and every finding parked rather than fixed. The ledger itself was scratch and is gone.

## Rulings

### Before Task 1
- `HearthlightPerSecond(9) == 8` was dropped from Task 4's tests. Nine gods is past the Queen, so the plan's own rule
  gave a different value. Cost if wrong: none.
- The wheel-tilt test's threshold is 1.3x, not 1.5x. The expected lead is about 1.6x, and 1.5x flakes on some seeds.
  Cost if wrong: a slightly weaker test of the same rule.

### Tasks 4–11
- **Task 4:** a way's rung keys come from its ladder (`ClassLadder.RungIndex`, tested), not from a hand-kept table.
  Cost if wrong: none.
- **Task 5, the Berserker:**
  - Fury's sentence on the card goes before the gift sentence, because every way's card ends with its gift. Cost:
    none.
  - Blood Rage and Warcry apply Fury on the keypress, not on the next second's tick. Cost: none.
  - Rage holds Fury until the rage ends, on one clock. Cost: none.
  - The card says "from halfway it feeds you". Cost: none.
- **Task 7, the Skald:**
  - The War Song uses one cached status-effect template per strength, rather than a new one per ally per second,
    which would leak. Cost: none.
  - The Marching Song's stamina is +50%, as the card says, not the plan's flat +4 (which was +80%). Cost: a one-line
    revert.
  - The Marching Song plays by itself once held, so "one song is always sung". Cost: none.
  - The march's +20% reaches the jog. Cost: none.
- **Task 8:** the Sæfari's fitting card is hers: half price, effects at her ship's tier, and no tier III of Sail, Hull or
  Ward offered to her (it would be a dead purchase). Cost: none.
- **Task 9, the Sæfari:**
  - At sea, Undertow strikes around her AND around her ship. Cost: none.
  - The seafarer card names her Ward on land. Cost: none.
  - Undertow's card says "but the gods", and it says so when only a god is near. Cost: none.
- **Task 10, the Smiðr:**
  - Forge-skin uses a cached template per armour factor. Cost: none.
  - The Watch-post refuses aboard, over open water, or with no ground ("No footing for a ballista here."), and spends
    no cooldown. Cost: none.
  - **His walls keep the support immunity (the owner confirmed it).** What he builds past the game's limits stands
    only while he is near, and the card says so.
- **Task 11:** no card tells the player about the wheel's tilt ("the wheel stays Odin's"). Cost if wrong: one line on
  THE WAY card.

### The final review
- **Thor's bow.** The weapon snapshot is now the one owner of the bow's damage, and the element is written through it.
  The weapon boons' refresh had been wiping the element and the Moder tempering. Own commit. Cost: none.
- **The summons' cooldowns.** Packbrother and Bonecaller **top up** to their count, refuse when full (spending no
  cooldown), and cool down when at least one came. Menagerie cools down. Before this, the three never cooled down at
  all. Cost if wrong: you wanted free resummons, or skeletons stacking to four.
- **A resume clears those three cooldowns**, because summoned company never survives a reload. Cost: a free call after
  each reload.
- **The Völva's and the Skald's tempering lift the rate to the new ceiling at once:** Hearthlight 12 at the Queen,
  Bragi 9 at Yagluth. The spec's +1 per god could never reach 12. Cost: a stronger late Völva; one number.
- **The Ward card keeps "aboard:"** for every way but the Sæfari's. Hers says "a tier weaker on land". Cost: none.
- **The Watch-post's ground check starts about 2 m above his feet**, so it no longer lands on the roof of a hall.
  Cost: none.
- **The ballista still shoots every untamed non-player**, deer and a boar you are taming included. The game's turret
  has no hostility filter. Check 8l asks you about it. Cost if wrong: a boar being tamed gets shot.
- Things the final reviewer set aside, and why each stands:
  - multiplayer (the saga is solo);
  - `m_shared` for the player's own weapon (the plan's exception);
  - balance numbers (left to tuning);
  - a weapon drawn after a damage boon waits for the next change (pre-existing);
  - saga speakers inside heal ranges (no effect);
  - Bloodied counting burn ticks;
  - the ceiling line flickering.

## Your calls (also in the home test plan)

- **8h:** with Sharpened, Glass Cannon and Reckless held (x2.52, already past the x2.5 ceiling), does Fury still
  matter? Do you want a Fury meter on screen?
- **8k:** the Sæfari's land Ward works before the raven has told it. Does that read as an early reveal?
- **8l:** should the ballista spare deer and boars?

## Deferred minors (found, judged not worth a fix now)

### Behaviour
- **Weapon and Fury:**
  - Sharpened x Glass Cannon x Reckless already reaches the ceiling, so a Berserker holding all three gains nothing
    from Fury, Rage or the War Song (8h asks).
  - FuryMeter's fade arithmetic is right only because it fades after exactly 1 s.
  - Bloodied reaches foes within 30 m only at the last tick.
- **Logging:** a persistently failing way engine logs a warning every second.
- **The Hunter's pack:**
  - Pack regen heals a fixed amount per poll, and mends every tame within 30 m.
  - A Homeward leaves the pack behind, and the call may still be cooling.
- **Hearthlight:** its 1 s gate equals the poll period, so a pulse can occasionally be skipped.
- **The Húskarl's Guard:**
  - A mod-defined status effect, unproven until it runs in game (check 8i, "Guard on" in Player.log).
  - It heals before the block's outcome is known.
  - A same-frame tooltip could still heal once.
- **Fleet-footed** (pre-existing) never sped the jog.
- **The Skald:**
  - Companions match the player's walk speed, not the jog, so they may lag a marching Skald.
  - Allies keep the War Song up to 3 s after the songs end.
- **The Sæfari:**
  - Undertow's 6 m circle round the ship's pivot covers only the middle of a longship.
  - Her Ward card says "a tier weaker on land" even after the Queen lifts that.
- **The Smiðr:**
  - The Field Forge still checks the ground from 5 m above the spot, so it can land on a roof (pre-existing).
  - The ballista's bolt upgrades appear on no card (no-spoiler policy).
- **Respec:** laying the way down leaves the bow's x1.5 for up to a second.
- **8n's self-check** can read FALLBACK instead of OK on a slow start.
- **Multiplayer:**
  - "Your walls" includes any player's walls.
  - The ballista's and the walls' settings live on our instance only.
  - The War Song claims any tame within 15 m.

### Code, docs, tests
- **Duplication:**
  - The ceiling rule is computed twice (`RefreshWeaponDamage` and `WeaponProduct`), and its "reached" check is
    untested.
  - Tempering numbers live twice: the BOONS-page strings and the effect constants. Change both together.
  - `MenagerieBeasts` and `MenagerieRoster` are two lists, intersected.
- **Stale or loose docs:**
  - A few stale doc comments: "THREE boons multiply", `PollCompanionPassives`' "few-second pace", and
    `DespawnCompanion`'s note about unloaded ZDOs.
  - The "ward"/"Ward" capitalisation is mixed.
  - Tempering text uses straight apostrophes.
- **Loose structure:**
  - `TemperGods` is a public mutable array.
  - `SagaItems.ElementScale` is a public mutable static.
  - `SeaShipTier` hard-codes 3.
  - A fourth rung would land on rung 3 silently.
  - `elemental` is a magic string in `BoonKeys`.
  - `ClassLadder.Validate` would pass a favoured boon that belongs to a way (it would tilt nothing).
- **Test gaps:**
  - the x2.5 boundary;
  - `TemperShown` for the Queen;
  - the Wolf's 2-god boundary;
  - FuryMeter's `AddHalf` at 15, a hit during a hold, and catch-up after a long gap;
  - the pack trim;
  - the wheel-tilt draw beyond the first slot.
