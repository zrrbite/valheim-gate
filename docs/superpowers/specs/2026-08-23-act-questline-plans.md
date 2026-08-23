# Questline plans, Acts II–V

Written 2026-08-23, at `0.221.12-run.alpha56.1`. **Planning only — nothing here
is implemented.**

Act I is finished and playable (26 steps, three tracks). This is the shape of
the other four, written while what the game actually contains is fresh.

## The pattern Act I established

Each act gets **three tracks**: `hunt` (kills, ending on the boss), `craft`
(the tier's building and material work), and **one track that is the act's
character**. Act I's is `hearth`. The third track is where an act stops being
"the tier before the next boss" and becomes a place you lived.

```
ACT I    Meadows     HEARTH    the homestead
ACT II   Black Forest FORGE    the bronze economy
ACT III  Swamp       MARSH     brewing, crypts, boats
ACT IV   Mountains   PEAK      wolves, silver, the high cold
ACT V    Plains      STEADING  the second homestead, at scale
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
