# Sea danger: the sea answers the heat

2026-10-08. Designed with the owner, section by section. Built natively in a session, from a plan that comes
after this spec is reviewed.

## 1. Intent

The owner: "Is there anything about spawning more monsters around the boat as we sail? It would be nice to have
waters be more treacherous", then "Lets do the heat based sea danger".

Today the saga spawns nothing at sea. Its sea content is two "sail for N seconds" tasks, the ship fittings and
the winds. Vanilla's own danger is mild: a Serpent spawns only at night or in rain and storms, one at a time
(`docs/catalog/game-catalog.md`), and the Ashlands sea has its Bonemaw.

**Success means:**
- sailing at high heat feels dangerous, and a voyage can still be finished;
- heat 0 is vanilla's sea, so the danger is something the player earned;
- nothing spawns on land, nothing piles up into a swarm, and nothing is left behind in the world;
- the dial can be tuned from the config after play-testing, without a new build;
- it can be tested on the owner's MacBook with the dev keys, without a mouse.

## 2. When it happens

**Only during a run, from Act II (act index 1).**

**At sea** means all of these:
- the player is aboard a ship (`Ship.GetLocalShip()` is not null);
- the ground under the ship is at least 1 m below the water level (the world generator's height, so not
  beached or in a shallow bay);
- the ship is moving: its horizontal speed is at least 1 m/s (sail or oars, not drifting at a dock).

**The voyage:**
- A voyage starts on the first second at sea.
- The **first 60 s are quiet**: no roll.
- The voyage ends after **120 s** not at sea (off the ship, ashore, or stopped). The next voyage is quiet
  again for its first minute.

**The dial:**
- `h = clamp(heat / FullHeat, 0, 1)`. At `h = 0` nothing rolls.
- The target average gap between encounters is `G = 1 / (PeakPerMinute × h)` minutes, **counting the
  cooldown**:
  - with the defaults (`FullHeat` 40, `PeakPerMinute` 0.5): 2 min at heat 40 or more, 4 min at heat 20, 8 min
    at heat 10, 20 min at heat 4.
- Rolls happen every **10 s**, outside the cooldown, at a rate that delivers `G`:
  - `rate = 1 / max(G − C, 10 s)`, where `C` is the cooldown;
  - `chance per roll = 1 − exp(−rate × 10 s)`.
- **Cooldown `C`:** 90 s after each encounter arrives.
- **Limit:** at most **two encounters** alive at once. A group counts as one encounter. An encounter ends when
  all its creatures are dead, despawned, or more than 300 m away.

## 3. What comes

**Choosing the creature:**
- Sample the land around the ship: 16 directions at 150 m and 16 at 300 m. A sample is land of its biome when
  the ground there is more than 1 m above the water level.
- **Flyer coasts** are the biomes found that belong to acts already reached (current act or earlier):

  | Coast biome | Creature (prefab) | From act |
  |---|---|---|
  | Mountain | Drake (`Hatchling`) | IV (index 3) |
  | Plains | Deathsquito (`Deathsquito`) | V (index 4) |
  | Mistlands | Gjall (`Gjall`) | VI (index 5) |
  | Ashlands | Fallen Valkyrie (`FallenValkyrie`) | VII (index 6) |

- When there is at least one flyer coast, a flyer comes with chance **2/3**, from one of those coasts picked at
  random. Otherwise the sea creature comes.
- **The sea creature:** a Bonemaw (`BonemawSerpent`) when the ship is on the Ashlands sea (the world generator's
  biome at the ship is AshLands) and Act VII is reached; otherwise a Serpent (`Serpent`).
- **A coast of an act not yet reached sends the sea creature** (no spoilers: no Gjall in Act II).
- The Deep North (the epilogue) has no flyer entry; its waters send Serpents.

**How many, and how strong** ("high heat" is `h ≥ 2/3`):

| Creature | Count | At high heat |
|---|---|---|
| Serpent, Bonemaw, Gjall, Fallen Valkyrie | 1 | 1 |
| Drake | 1 | 2 |
| Deathsquito | 2 | 3 |

- **Stars,** through the game's own `Character.SetLevel`, set at full health:
  - level 1 (no star) for `h < 1/3`;
  - level 2 (one star) for `h < 2/3`;
  - level 3 (two stars) above that.

**Arrival:**
- **Sea creatures surface 50–70 m ahead**, within 45° of the ship's heading, at the water level, on open
  water. "Open water" is the same test as "at sea": ground at least 1 m below the water level. Up to 8 tries
  for a spot; if none is found, this roll is skipped.
- **Flyers come in from the coast's side**, 40–60 m from the ship, about 15 m above the water.
- **Each creature is set to hunt the player** (`MonsterAI.SetHuntPlayer(true)`, the switch raids use) and
  alerted (`BaseAI.Alert()`).
- **Not saved into the world:** `ZDO.Persistent = false`, as for the Herald deer and the contest pack. They
  vanish on a reload or when the area unloads.
- Kills count like any kill (tasks, the Hunter's Eye, the death hook).

## 4. Voice and tie-ins

**Speech:**
- **The first encounter of a run:** the raven speaks once (`TrySpawnRaven`, falling back to `Message`):
  > "The sea has found your wake. The hotter you burn, the more of it comes. A ward at the helm keeps the
  > worst of it off."

  From then on, the helm offers the **ward** (below).

  The flag is saved with the run, like the god's wind's, so a reload does not repeat it.
- **Every encounter:** one top-left message, by creature:

  | Creature | Message |
  |---|---|
  | Serpent | "Something rises off the bow." |
  | Bonemaw | "The boiling sea gives up a Bonemaw." |
  | Drake | "Drakes come down off the mountains." |
  | Deathsquito | "Something whines over the water." |
  | Gjall | "A Gjall drifts out over the water." |
  | Fallen Valkyrie | "A fallen valkyrie rises over the ash coast." |

  The owner may reword any of these.
- **The log:** every encounter writes one `Player.log` line: what, how many, the stars, the heat, `h`, the
  current `G`, and why it came (rolled, the horn, or dev).

**The ward: a defense bought at the helm** (owner, 2026-10-08: "How are we going to defend these raids? ... Maybe a
skill you could buy", then "Arsenal + a ward fitting"):
- **What you carry stays the main defense:** the bow, Thor's bow, the ways' abilities.
- **The ward is a new ship fitting, `Ward` I–III,** bought with coins from the `Shift`+`E` card like the others.
- **When it is offered:** only once the raven has spoken its sea line. That is the same pattern as Fire-tar,
  which waits for the Ashlands act, so it is offered when the player knows why they would want it.
- **What it does:** while the player is aboard the ship, a pulse every **3 s** strikes every hostile creature
  within the radius of the ship. That includes the saga's, vanilla's serpents, and flyers over the deck.
  - It is **lightning** damage, the saga's storm motif, through the ordinary damage path, so kills count.
  - It never hurts players, tamed creatures, or the saga's speakers (they are immune).
  - **It is passive,** so it needs no key (laptop keys are scarce), and it helps every way, melee ones too.

  | Tier | Price | Radius | Damage per pulse |
  |---|---|---|---|
  | I | 100 | 20 m | 20 |
  | II | 250 | 25 m | 40 |
  | III | 450 | 30 m | 70 |

  - Tier I kills deathsquitos outright and wears a drake down in about 15 s.
  - Against a starred serpent or a Bonemaw it is support, not the answer: at tier III, a two-star Serpent
    (1,200 health) takes about 50 s to fall to the ward alone.
  - The numbers are first numbers, like every fitting's, to be tuned in play.
- **A small spark on each target,** if the lightning effect the deer herd uses exists. Effect names are asset
  data, so no spark is better than a guessed one that fails loudly.
- **The card can now show five lines** (Sail, Hull, Wind-horn, Ward, Fire-tar). It already divides its width by
  the count. `Choice5` is `Keypad 5`, or `U` on the laptop. The narrower columns get checked on the Mac.

**Tie-ins:**
- **The Wind-horn draws them.** Blowing it at sea with heat above 0 makes the next roll certain. That still
  respects the quiet minute, the cooldown and the limit. The flag clears when it is used or when the voyage
  ends.
- **The god's wind and the Hull fitting get no new rules.** Speed outruns a serpent, and the fitting already
  cuts hull damage.
- **No HUD element.**

**Dev and MacBook testing:**
- **`Shift`+`B` (dev ship key) while already aboard a ship at sea** calls an encounter at once:
  - chosen as normal for the place;
  - stars and counts from the current heat;
  - ignoring the dial, the quiet minute, the cooldown and the limit.
- Away from a ship, `Shift`+`B` still builds one.
- It is listed in the dev keys window (`DevKeyTable`) and tagged `MACBOOK-TEMP` with the ship key. If it is
  wanted after the temporary keys go, it moves to a permanent dev key then.

## 5. Architecture

**`RunMode/SeaDanger.cs`, pure, no Unity; unit-tested:**
- **Its own small enums:**
  - `SeaCoast { Mountain, Plains, Mistlands, Ashlands }`;
  - `SeaCreature { Serpent, Bonemaw, Drake, Deathsquito, Gjall, FallenValkyrie }`.
- **The dial:** `Dial(heat, fullHeat)` gives `h`. `ChancePerRoll(h, peakPerMinute, cooldownSeconds, rollSeconds)`
  is 0 when `h` is 0.
- **Strength:** `Level(h)` gives 1, 2 or 3. `Count(creature, h)`.
- **The voyage clock, `Voyage`:**
  - `Tick(now, atSea)` returns whether a roll is due now;
  - it tracks the voyage start, the quiet minute, the 120 s end, the cooldown, the live encounter count, and
    the horn flag;
  - `Arrived(now)`, `Ended()` (one encounter is over), `HornBlown()`.
- **The choice:** `Choose(coastsNear, onAshlandsSea, actIndex, rng)` returns `(creature, prefab, count,
  message)` with the act gating and the 2/3 rule. `rng` is injected as a `Func<double>`.

**`RunMode/ShipFittings.cs`, extended (pure, already tested):**
- `FittingKind.Ward`, with `MaxTier` 3, and `ShipFittingState.Ward`;
- `WardRadius(tier)`, `WardDamage(tier)`, and `WardPulseSeconds` = 3;
- `Offers(state, fireTarTold, wardTold)` offers the next Ward tier only when `wardTold`;
- `Bought` and `Summary` know the ward ("Sail II · Hull I · Ward I").

**`RunMode/Unity/SeaWatch.cs`, the game side:**
- **Called once a second** from `RunService`'s poll, beside `PollWinds`.
- **Reads:** `Ship.GetLocalShip()`, the ship's body velocity, `WorldGenerator.instance.GetHeight` and
  `GetBiome` for the ship and the ring, and `ZoneSystem.instance.m_waterLevel`.
- **Rolls** with `SeaDanger` and an injected RNG.
- **Spawns:**
  - from `ZNetScene.instance.GetPrefab(name)`;
  - `SetLevel` at full health;
  - `MonsterAI.SetHuntPlayer(true)` and `BaseAI.Alert()`;
  - `ZDO.Persistent = false`.
- **Keeps** each encounter's creatures, to know when it ends (`ReferenceEquals` against destroyed objects,
  per the codebase's rule).
- **The ward pulse,** every 3 s while the player is aboard and the Ward tier is at least 1:
  - finds hostile characters within the radius of the ship (`Character.GetCharactersInRange`);
  - skips players, tamed creatures and the saga's speakers;
  - hits each with a lightning `HitData` through `Character.Damage`, the same path the dev slay uses.
- **Talks to `RunService`** through callbacks: say, raven, log.
- **Never throws to its caller:** an exception is logged once, and the tick skips.

**Hooks in `RunService`:**
- the poll call;
- the horn telling SeaWatch it was blown;
- `DevShip`, which asks SeaWatch to force an encounter when aboard at sea;
- the run-state flag `seaRavenTold`, which also unlocks the ward's offer;
- the run-state field `shipWard`, beside `shipSail`, `shipHull`, `shipFireTar` and `shipWindHorn`;
- the self-check;
- the config values.

**Config** (`Core/Configuration.cs`, the run JSON):

| Setting | Default |
|---|---|
| `runSeaDanger` | `true` |
| `runSeaFullHeat` | `40` |
| `runSeaPeakPerMinute` | `0.5` |

**Self-check:**
- One line, "Sea creatures", over the six prefab names: OK, or MISSING with each missing name.
- A missing creature is replaced by a Serpent.
- If the Serpent itself is missing, the sea danger is off for the run, with one log line.

## 6. Testing

**Pure (`Tests/SeaDangerTests.cs`):**
- **The dial:**
  - `h` clamps;
  - the chance is 0 at heat 0 and rises with heat;
  - the average gaps 2, 4 and 8 min come out of the rate formula at heat 40, 20 and 10. Check the formula,
    not a simulation.
- **Strength:** the level thresholds, and the counts at and above high heat.
- **The voyage:**
  - no roll in the first 60 s;
  - a roll is due every 10 s after that;
  - the 120 s end makes the next voyage quiet again;
  - the cooldown, and the limit of two;
  - the horn makes the next roll certain within all three.
- **The ward** (`ShipFittingsTests`):
  - not offered before the raven's line, offered after it;
  - the tiers in order, with their prices;
  - the radius and damage per tier;
  - `Bought` capped at III;
  - the summary.
- **The choice** (fixed `rng`):
  - each coast and its act gate, and an unreached coast sending a Serpent;
  - the 2/3 split;
  - the Bonemaw on the Ashlands sea only with Act VII reached.

**On the Mac:**
- `Shift`+`B` to a ship, then `Shift`+`B` again at sea;
- with god mode (`0`) and slay (`fn`+`Backspace`), see each creature arrive, hunt, and not be saved, by
  reloading;
- buy Ward I at the helm (coins from `Z`, out of the stash), and see a deathsquito or a drake fall to it;
- check that five lines fit the fittings card.

**Home test plan:** a new boat check, **38b**.

## 7. Docs

- `docs/SAGA-WALKTHROUGH.md`: the boats part.
- `dist/windows/DEV-MODE.md`: `Shift`+`B` aboard.
- `RESUME.md`, the home test plan's 38b, and a `HANDOFF_WINDOWS.md` task.
- `CLAUDE.md`'s self-check note: the "Sea creatures" line.

## 8. Not in scope

- New creatures, models or textures.
- Rewards for sea kills beyond the game's own drops.
- Other players' ships.
- Leeches.
- A HUD element.
- Despawning at run end: they vanish when the area unloads.
- A sea for the Deep North beyond Serpents.

## 9. Open after play-testing

- `FullHeat` and `PeakPerMinute` (the heat curve as a whole is still untuned).
- The ward's prices, radius, damage and pulse.
- The 2/3 flyer share.
- The message and raven wording.
- Whether flyers' AI behaves over open water. It is untested in vanilla, and the reason the log line says
  what came.
