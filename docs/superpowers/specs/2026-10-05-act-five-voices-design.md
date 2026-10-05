# Act V gets voices — the harvester, Hildir's last chest, and the steading's feast

Written 2026-10-05, the fourth act given voices that day (Acts II–IV:
`2026-10-05-act-{two,three,four}-voices-design.md`). Reuses `SagaSpeaker` and `TraderVoice`.

## Why

Act V, *The Golden Ruin*, is where the saga ends by default (`runFinalBossKey` is Yagluth's; his step
pays "The saga is complete"). It had eight steps, no speaker, and a plain-recipe last Stormsworn
piece. Its answer to the shortage is **industrialise it**: Yagluth's people harvested light at scale,
and the plains are what is left when a harvest runs dry. The fulings still keep a quota on an empty
field. The old plan (`2026-08-23-act-questline-plans.md`) wanted Act V to rhyme with Act I — the
homestead again, at scale, ending on a meal.

## Decisions taken (owner, 2026-10-05)

| Question | Answer |
|---|---|
| Who carries the act | **A harvester's ghost + Hildir** |
| The harvester's ask | **A meal from the field** — eat what was grown instead of harvesting it |
| What he gives | **The Stormsworn mantle**, the set's last piece |
| Hildir's thread | **Her third chest, from the Sealed Tower** |
| Third track | **STEADING**: the windmill, Hildir's chest, a feast at your own table |

## The story

| Beat | What the player learns |
|---|---|
| The harvester | Somebody did exactly this before, at scale, and never once sat down |
| Eating from the field | The one thing the harvesters never did with what they took |
| The feast at home | Act I's first meal, five biomes later — the hearth was the answer all along |
| Yagluth (exists) | The harvest's last hand closes on nothing |

### The harvester

One of Yagluth's own people. **Unnamed.** The Ghost in a sixth palette: **dull gold**, grain gone
grey, a low wheat-coloured light — the golden ruin's colour, faded.

- **Where:** at the plains stone ring nearest the player (the megaliths; location name guessed as
  `StoneHenge*`, confirmed by the run-start location registry, which gains "Henge" and "Stone").
  No time or weather gate: he stands where the work was.
- **Wanted:** from `pl-arrive` done until the run ends.
- **Phases:** Speak (`pl-harvester` live) → Ask (`pl-meal` live) → Idle → After (Yagluth dead).
- **First words (meaning):** they did what the player has been doing — took the light from the herd,
  the forest, the fields — and did it at scale: cut the country into straight lines and stored all
  of it. They never once sat down. The fulings still keep the quota; nobody told them it ended.
- **His ask — eat from the field:** speak to him with all three food slots full and at least one of
  them a plains food (`Bread`, `LoxPie`, `FishWraps` — `Player.Food.m_name`, prefab names). Short of
  that, he says what he wants to see. Done, `HarvesterFed` → `pl-meal`.
- **What he gives:** the **Stormsworn mantle**; its recipe gate moves from `pl-berserker` to `pl-meal`
  (cost unchanged: artisan table, 4 lox pelt, 10 needles, 6 silver).
- **After Yagluth:** one closing remark, written to stand as the saga's ending when Yagluth is the
  final boss: the field can be eaten from now.

### Hildir, again

Her `TraderVoice` configuration gains Act V lines (what a harvest leaves behind) alongside Act IV's;
the voice is wanted in Act V or while a STEADING step of hers is live. Her errand: her **third
chest, from the Sealed Tower** — selected from her own give entries by a name naming the tower, else
the one numbered 3, exactly as the cavern's (2).

## The chain

### CRAFT

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Claim Moder's power | StatDelta | `pl-power` |
| 2 | Reach the Plains | ReachBiome | `pl-arrive` |
| 3 | **Speak with the harvester** | PlayerEvent `HarvesterFound` | **new** `pl-harvester` |
| 4 | **Eat from the field** | PlayerEvent `HarvesterFed` | **new** `pl-meal` |
| 5 | Cure the Stormsworn mantle | CollectItem | `pl-storm`; recipe now taught by him |

### HUNT — unchanged

Break the plains → kill 2 Fuling Berserkers → find Yagluth's altar → defeat Yagluth.

### STEADING — new third track (`steading`)

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Build a windmill | BuildPiece | `pl-windmill`, **moved** from CRAFT |
| 2 | **Bring back Hildir's chest from the Sealed Tower** | PlayerEvent `HildirTowerChest` | **new** `st-chest` |
| 3 | **A feast at your own table** | PlayerEvent `SteadingFeast` | **new** `st-feast` — three foods, at least one plains food, while `Player.IsSafeInHome()` |

Optional heat, never a gate on Yagluth. **8 → 12 steps.**

## The code

- **`Harvester`** — new, on `SagaSpeaker`; spot from the nearest `StoneHenge*` location (each
  numbered variant tried; first found wins), ring search for footing.
- **`CreatureDressing.Harvester()`** — dull gold.
- **`PlainsMeal`** helper — `int PlainsFoods(Player)`, `bool FullTable(Player)` over `GetFoods()`.
- **`HildirVoice`** — Act V talk; `TowerChest(gives)` selector.
- **`RunService`** — chain edits; `steading` track; mantle gate; `PollHarvester`; Hildir's poll
  extended to Act V / STEADING; the feast check on the 1 Hz poll while `st-feast` is live;
  validators (spawn events `HarvesterFound`, `HarvesterFed`, `HildirTowerChest`, `SteadingFeast`);
  registry keywords.
- **Atlas** — a STEADING lane colour.

## Verification in the first run

1. The stone ring's location name (registry), and that the harvester stands somewhere reachable.
2. Plains foods' prefab names — logged when the meal check first sees a full table.
3. Hildir's Sealed Tower chest entry (the "Hildir accepts" lines).

## Not in this work

- The earlier speakers returning for a finale (the reunion idea) — a later design.
- Acts VI–VIII, which are stand-ins.
