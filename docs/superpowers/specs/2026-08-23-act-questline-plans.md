# Questline plans, Acts II–VII

Written 2026-08-23, at `0.221.12-run.alpha56.1`. **Planning only — nothing here
is implemented.**

Act I is finished and playable. This is the shape of the other six, written
while what the game actually contains is fresh.

**The saga is SEVEN acts, not five.** It was scoped to the five "mainland"
bosses; Valheim also ships the Mistlands (the Queen) and the Ashlands (Fader).
The Deep North got its boss in Valheim 1.0 (noted 2026-09-12, from the 1.0.12
assembly: `GP_DeepNorth`, a "frozen king" item token, a `SE_Crowned` status
effect and a crown mode on the player). **Act VIII exists as a PLACEHOLDER** —
see the section at the end — until its boss prefab, altar location and defeat
key have been read out of a 1.0 game. The saga is therefore EIGHT acts; VI and
VII were built thin on the same day, so the table stays one-to-one with the
game's boss order.

Acts VI and VII are planned here but should not be BUILT until II–V have been
played. They are also where "asset data this assembly cannot verify" gets much
worse: prefab names that deep are far less certain, and the registry-dump
discipline below matters more there than anywhere.

## The pattern Act I established

Each act gets **three tracks**: `hunt` (kills, ending on the boss), `craft`
(the tier's building and material work), and **one track that is the act's
character**. Act I's is `hearth`. The third track is where an act stops being
"the tier before the next boss" and becomes a place you lived.

```
ACT I    Meadows      HEARTH    the homestead
ACT II   Black Forest FORGE     the bronze economy
ACT III  Swamp        MARSH     brewing, crypts, boats
ACT IV   Mountains    PEAK      wolves, silver, the high cold
ACT V    Plains       STEADING  the second homestead, at scale
ACT VI   Mistlands    LANTERN   light as currency, and as the only way to see
ACT VII  Ashlands     ASH       the end of it
```

### Consequence to design around

`ActDefinition.SeatingFor` carries an unfinished track forward when the new act
does not reuse its id. With five distinct third-track ids, **every unfinished
one accumulates**. Reaching Act V with a half-done hearth, forge and marsh means
six visible tracks.

Options, decide before building Act II:

1. **Accept it.** Nothing is ever lost, and a player who ignored the forge can
   finish it later. Risk: the panel becomes a list.
2. **Cap carried tracks** to the most recent one or two.
3. **Reuse one id** (`side`) for every act's third track, so a new act's own
   replaces the last — losing carry-over, which was added deliberately.

Recommendation: **1, with a cap of two carried**, so at most five rows show.
**Implemented in alpha64** as `ActDefinition.MaxCarriedTracks = 2`, taken from the
nearest acts backwards. With seven acts the cap matters more, not less.

---

## Act II — The Black Forest (the Elder)

**Character:** the first real industry. Act I fed you; this one equips you.

**FORGE** (`forge`)
| Step | Measure |
|---|---|
| Build a charcoal kiln | `BuildPiece Smelter` — matches kiln, smelter and blast furnace alike |
| Build a forge | needs a distinct category; `CraftingStation` + level, or a new piece test |
| Upgrade the forge (2) | `BuildPiece StationUpgrade`, target 2 |
| Craft your first bronze | `StatDelta Crafts`, or `CollectItem` on bronze |
| Raise a cart | `BuildPiece` on `Vagon` |
| A pair of portals | `BuildPiece Teleport`, target 2 — one is useless |
| Find Haldor | `DiscoverLocation` — the trader camp, with a Herald-style bearing |

Haldor earns its place here: the Black Forest is where he lives, and Act I now
grants the rod rather than sending you shopping for it.

**HUNT:** greydwarves → skeletons → a troll → the Elder.
The troll should be a named event like the Herald if the machinery allows.

**CRAFT:** copper, tin, the smelter (already carries the 30 surtling cores),
bronze gear, stone building.

**Risks:** the cart needs bronze nails, so it must come after bronze. The forge
needs copper. Order matters more here than anywhere — this is the act where the
trophy mistake would repeat.

---

## Act III — The Swamp (Bonemass)

**Character:** preparation. The only boss that is decided before the fight.

**MARSH** (`marsh`)
| Step | Measure |
|---|---|
| Build a fermenter | `BuildPiece Fermenter` |
| Brew poison resistance mead | `CollectItem` on the mead |
| Build a karve | `BuildPiece Ship` |
| Sail the marshes | `StatDelta DistanceSail` |
| Open a crypt | `DiscoverLocation` on the sunken crypt |
| Iron enough to stand in | `CollectItem` on iron |

**This is where `RequiresTrackComplete` is used.** It exists, is tested, is
guarded by `ValidateActs`, and has no user — it was kept for exactly this.
Bonemass's altar waits on `marsh`, because a player who arrives without poison
mead is not fighting Bonemass, they are dying to him.

> *The marsh does not forgive the unprepared.*

Unlike Act I's gate, this one is honest: the preparation genuinely decides the
fight, rather than being a cozy detour the boss could cut short.

**HUNT:** draugr → blobs → leeches → a wraith → Bonemass.

**CRAFT:** iron tools, the stonecutter, iron gear.

**Risks:** the crypt key comes from the Elder — already granted as `bf-elder`'s
reward, now correctly named `CryptKey`. Fermenter needs bronze.

---

## Act IV — The Mountains (Moder)

**Character:** the cold, and the first pets worth having.

**PEAK** (`peak`)
| Step | Measure |
|---|---|
| Brew frost resistance mead | `CollectItem` |
| Tame a wolf | `StatDelta CreatureTamed` |
| Raise a pack (3 wolves) | `PlayerState TamedNearby` — the pen measure, reused |
| Build a windmill | `BuildPiece Windmill` |
| Silver from the deep rock | `CollectItem` |
| Find a dragon egg | `CollectItem` |

Taming wolves pays off three earlier things at once: the pen from Act I, the
Shepherd boon, and `TamedNearby`. That is the reward for having built the
machinery generically.

**HUNT:** wolves → drakes → a stone golem → a fenring → Moder.

**CRAFT:** wolf armour, silver gear, obsidian arrows.

**Risks:** silver needs the wishbone, which is Bonemass's drop — so the act
depends on Act III's boss having actually been felled, not merely reached.
Windmill needs iron.

---

## Act V — The Plains (Yagluth)

**Character:** the homestead again, but at scale. Deliberately rhymes with
Act I, which is the point of ending here.

**STEADING** (`steading`)
| Step | Measure |
|---|---|
| Plant barley and flax | `BuildPiece Plant`, a larger target |
| Build a windmill and grind flour | `BuildPiece Windmill` + `CollectItem` |
| Build a spinning wheel | new piece test |
| Tame a lox | `StatDelta CreatureTamed` |
| A herd (3 lox) | `PlayerState TamedNearby` |
| Set a table for a feast | `PlayerState FoodSlotsFilled` at the highest tier |

Ending the saga on "sit down to a meal in a house you built" — the same step
Act I opens with, five biomes later — is the closing note the whole mode has
been arguing for.

**HUNT:** fulings → deathsquitos → a berserker → Yagluth.

**CRAFT:** black metal, padded armour, needle arrows.

**Risks:** the spinning wheel needs the artisan table, which needs **Moder's
trophy** — so it is genuinely Act V and not earlier. Barley and flax only grow
in the Plains.

---

## The throughline

The story ties across all seven without a handover. Act I gave the saga a theme
by accident — the deer give up their light and something takes it — and the
theme turned out to be general enough to carry the whole game, because it is
not about a MacGuffin. It is about **what each thing in the world does with
light**.

| Act | What it does with light |
|---|---|
| I | **Steals** it. Greydwarves harvest the herd one carcass at a time. |
| II | **Feeds** on it. The forest grows on what was carried off; the Elder is what it was carried TO. |
| III | **Traps** it. Nothing in the fen ever gives its light up, which is why nothing there stays down — and Bonemass is a heap of everything that never let go. |
| IV | **Loses** it. The mountains are where light does not reach. Moder guards eggs: light that has not woken yet. |
| V | **Industrialised** it, and fell. The Plains are a ruin because someone did all of this at scale before you did, and Yagluth is what was left standing. |
| VI | **Carries** it. The dvergr are the only ones who got it right — a wisp in a lantern, not a light taken from something. |
| VII | **Burns.** |

The player's own arc runs underneath it: you begin taking lights back one at a
time and end carrying the last one into fire.

Two things this buys, beyond tidiness:

- **Every act's atmosphere follows from its verb.** The greydwarf packs, the
  forest answering the axe, the fen raising its dead — those were designed
  independently and each turns out to be that act's relationship with light. IV
  and V do not have their atmosphere yet, and the verb says what it should be:
  something about absence, and something about the ruins of an earlier harvest.
- **No handover is needed at act V.** The seam that would have needed one is
  where the "mainland" bosses stop, and the theme crosses it — the Mistlands is
  the ANSWER to Act I rather than a new subject, because it is the one place
  that carries light instead of taking it.

Where it is thinnest: **Act IV**. The mountains have the least obvious
relationship to any of this, which is exactly why its title is currently
describing an intention rather than something built. If the arc breaks anywhere
it breaks there, and the honest fix is to build IV's atmosphere first and let
the title follow, rather than the other way round.

---

## The titles, as one arc

Acts are named for their story, never their biome and never their boss. With
seven, the arc is about LIGHT — which is what Act I turned out to be about the
moment the deer started giving it up.

```
I    The Stolen Light      who is taking it
II   Where the Light Goes  and where it ends up
III  Nothing Stays Buried
IV   The White Silence
V    The Golden Ruin
VI   A Light to Carry      the Mistlands: light is the only way to see
VII  The Last Light        the end
```

Act V was called "The Last Harvest" while the saga was five acts long, which
read as a finale. It is a middle now, so it takes the Plains' own image — golden
grass over a fallen civilisation — and the finale moves to VII, where it
bookends the theft the saga opens with.

---

## Act VI — The Mistlands (the Queen)

**Character:** you cannot see. The mist is a second kind of darkness, and Act I
was the first — night you can wait out, mist you cannot.

**LANTERN** (`lantern`)
| Step | Measure |
|---|---|
| Carry a wisplight | `CollectItem`, or a PlayerState reading for the equipped light |
| Build an eitr refinery | new piece test |
| Build a black forge | new piece test |
| Build a galdr table | new piece test |
| Find a dvergr outpost | `DiscoverLocation` |
| Refine eitr | `CollectItem` |

The wisp is the payoff of the whole light arc: in Act I one led you somewhere,
in Act VI you carry one and it is the only reason you can walk. If a single
step in this act earns its place, it is that one.

**HUNT:** seekers → a gjall → a soldier → the Queen.
**CRAFT:** black marble, soft tissue, eitr gear.

**Risks:** almost everything here is Mistlands-era asset data. Nothing should be
written without a registry dump first — see below.

---

## Act VII — The Ashlands (Fader)

**Character:** the end. Nothing here is being built to last.

**ASH** (`ash`)
| Step | Measure |
|---|---|
| Survive the crossing | `ReachBiome`, plus a landing |
| Mine flametal | `StatDelta MineHits` |
| Build a forge that can work it | new piece test |
| Hold the ground (no-death minutes) | `NoArmorMinutes`'s shape, inverted |

Deliberately the SHORTEST third track. The Ashlands is where a saga should stop
adding systems and let the ones it has finish; a homestead track here would be a
lie, since nothing you build in the Ashlands is meant to survive.

**HUNT:** charred → an asksvin → a morgen → Fader.

**Risks:** the newest content in the game, and the least certain names.

---

## Act VIII — The Deep North (placeholder, 2026-09-12)

**What the 1.0.12 assembly says** (read from the IL, not remembered): a guardian
power `GP_DeepNorth` with `SetPowerDeepNorth`/`UsePowerDeepNorth` stats; an
item message key `$item_frozenking_drop`; `SE_Crowned` and `Player.SetCrownMode`
saved on the character as `crowned`; the offering bowl reworked to spawn the
boss at a distance and take offerings from item stands. Deep snow that slows
by depth (`SnowRoller`), snow that builds up on pieces and damages them
(`Game.m_snowDamage`, world keys `NoHeavySnow`/`AllHeavySnow`), fireplaces that
melt it, slippery/skating movement on `Character`, grappling points, aurora
shader parameters, a `DeepNorth` build category and a `StoneCircle` global key.

**What it cannot say:** the boss prefab, its altar's location name, and the
global key its death sets. All three are asset data. The placeholder uses
stand-ins from `SagaNames` (`__deep_north_boss`, `__deep_north_altar`,
`__deep_north_defeated`), the act is flagged `ActDefinition.Placeholder` so the
run-start validator skips it and says so once, and the run-start log now prints
a **"Boss registry"** — every `OfferingBowl` prefab with the boss it spawns and
the key it sets, plus every boss-looking location name. Read those lines from a
1.0 game, put the real names into `RunService.Bosses` and `DeepNorthChain`,
drop the flag.

**Title, as a placeholder:** "What the Cold Keeps" — *Ice does not take light.
It keeps it. Find out from whom.* The story bible's rule that only Act VII
answers the shortage is left standing; the Deep North's answer is not written
until someone has played it.

**Chain, as built:** reach the Deep North → defeat what the cold keeps (a
stand-in kill that can never complete). Nothing else, on purpose.

## Item quests — one per act (started 2026-09-12)

Owner: *"an actual quest where we can collect materials, and maybe craft it at
the Act 1 top crafting station … Later, we'll add an Act item quest for every
act, once we prove the first one."*

**The shape (owner, same day):** *"talk to someone, find a bunch of mats for
that person, get a recipe. Then fulfill that recipe to craft the item."* Three
steps on the CRAFT track, late, after the bench upgrades:

1. **Find the shade** (`mq-shade-find`, PlayerEvent) — the someone. A hunter
   who died before the bow was strung, wearing the Ghost prefab (the lights'
   fallback body, no shipped asset), pale and faintly lit, standing 7–11 m from
   the claimed bed AFTER DARK only — the act's rule, "nothing you seek walks in
   the light", dismisses it at dawn and brings it back at dusk. Non-persistent,
   like the Herald; re-made whenever wanted, night, and none standing. The
   strip carries a bearing to it while this step is live. `HuntersShade.cs`.
   Why a shade: the bible's ravens never help and the Meadows have no living
   human, so the teacher is one who did not finish.
2. **Bring the shade what it lacked** (`mq-shade-bring`, PlayerEvent) — the
   mats for the person, NOT the bow's own makings: ten flint and five leather
   scraps, "the quiver it never filled". The interact takes them from the pack
   and the shade speaks through the game's rune panel (`TextViewer`, the lore
   stones' UI). *Get a recipe* is literal: `SagaRecipes` registers the bow
   recipe while this step is DONE (`StepPredicates.StepDone`, derived from
   the tracks, so a resume re-teaches it and nothing is saved).
3. **String Thor's bow** (`mq-bow`, CollectItem for the result, so however it
   was made counts) — the recipe at the WORKBENCH, the Meadows' top station
   (the forge is Act II's): 10 wood, 10 resin, 6 deer hide, for the saga's own
   item (below). Until the same evening it produced vanilla's Finewood bow;
   that version was played through end to end on 2026-09-12 (owner: "I think I
   completed the quest") before the item was made its own.

The recipe is registered for the run's length into `ObjectDB.m_recipes` and
removed when the run ends — a recipe is a plain list entry, re-checked every
poll because a world load rebuilds the database.

**The interact lives on a CHILD object with its own trigger collider.** The
game finds hover text and interacts with `GetComponentInParent` from the
collider the crosshair hit; a Character is itself Hoverable, on the root, and
added first, so a component on the root loses the prompt to the creature's
name. Not yet proven in play — the lights' Ghost fallback never ran because
the Wisp prefab exists — so if the prompt shows only the name, this is where.

**Thor's bow — the saga's first item of its own** (owner: *"a quest to
complete a bow that'll make short work of Eikthyr. Maybe it will spawn
lightning on impact"*). Built the same evening, `RunMode/Unity/SagaItems.cs`:

- The Finewood bow prefab is CLONED at runtime under an inactive holder that
  survives scene loads (so its ZNetView never registers a ZDO), renamed
  `Saga_ThorsBow`, given its own shared data (checked by reference, with a
  MemberwiseClone fallback — shared data is shared, and tuning the original
  would retune every Finewood bow in the world), a display name (plain text,
  not a `$` token), a description, and **20 lightning damage, +4 per level**
  on top of the Finewood bow's 32 pierce.
- It is registered with BOTH registries — ObjectDB (recipes, inventories) and
  ZNetScene (dropped items) — and re-registered EVERY FRAME whenever either
  instance changes, because the player's pack loads a few frames after
  ObjectDB does and an unresolved name is silently dropped from the save.
  Not run-only: the bow outlives the run that strung it.
- **Lightning on impact** is done from the mod's side, not in data: the game
  plays a projectile's hit effects from the ARROW, so a bow cannot carry one.
  While Thor's bow is the weapon in hand, every arrow the player has in flight
  is given a spawn-on-hit of the game's own lightning effect (the Herald's
  candidate list; the log says which resolved). The projectile's owner and
  weapon are private and read by reflection — the only reflection in the mod
  besides the two registry tables.
- **The one thing it cannot survive is the mod being absent**: the item is
  then an unknown name and is lost from any pack or world it was in. Accepted
  for a personal mod; written here so nobody is surprised.

Not yet verified in play: the clone's shared data being its own (the log
reports it), the bow loading back from a save, and the flash on impact.

**Undecided (owner):** where the player FINDS the quest's hint. For now it is
the standing Hint under the step like every other, plus the shade's own words;
the quest-book idea in RESUME.md is the candidate home if the hint should be
discovered rather than shown.

**Not done, and the next decision:** a NEW item — its own name, stats, icon —
rather than a new recipe for an existing one. Cloning a prefab at runtime is
possible (copy the `ItemDrop`'s shared data, rename, register with `ObjectDB`
AND `ZNetScene`), but any such item left in a world or an inventory is an
unknown object the moment the mod is absent, and a new LOOK needs an
AssetBundle — the same decision `CreatureDressing` is waiting on. Prove the
recipe in play first.

## Verification discipline

Every act above must go through what Act I learned the hard way. In order:

1. **Recipe tier.** Is each step craftable *in that act*? "Hang a trophy" was
   valid, measurable and impossible, and because a track is a linear chain it
   stalled six steps behind it.
2. **Registry dump.** Never commit a threshold or a species count from memory.
   The fishing steps were wrong three ways at once and one log line settled all
   of them. Add a dump for whatever family the act leans on.
3. **Stat semantics.** A `PlayerStatType` member existing does not mean it
   counts what its name says. `FoodEaten` counts meals *refused*.
4. **One impossible step blocks a track.** This is the design's sharpest edge.
   Consider whether an act's third track should be shorter than the hearth's
   eleven for that reason alone.

## Open questions

- Should the troll, wraith, golem and berserker each get a Herald-style named
  event, or does that cheapen the Herald?
- Does the third track want to be *optional* in later acts — pure side content
  that carries forward — rather than a peer of hunt and craft?
- Heat is still untuned across the whole saga. Content plans assume it will be;
  they do not depend on it.
