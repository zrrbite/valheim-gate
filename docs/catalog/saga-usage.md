# What the saga already uses from vanilla Valheim

Inventory as of `f2b63ee` (`1.0.16-run.2026-10-06`, branch `feature/run-mode`). Read-only survey.
Sources: `ICanShowYouTheWorld/RunMode/**` (mainly `Unity/RunService.cs`: `Bosses` l.25, `Acts()` l.10653,
chains l.11343-12820, `QuestRewards` l.12827, `BossSpoils` l.8290, side-task pool l.10330-10640),
`SagaItems.cs`, `SagaRecipes.cs`, the speaker classes, `TraderVoice.cs`/`HaldorVoice.cs`,
`CreatureDressing`, `DeerHerd`, `TheGatherer`, `TheBreaker`, `StolenLights`, `SpiritChase`,
`ForestWatch`, `FenWatch`, `SagaRaids`, `SagaDreams`, `SagaWinds`, `Shipwright`, `BoonEffects`,
`BossVigor`, `WorldModifiers`; docs `docs/superpowers/RESUME.md`, `specs/2026-08-27-story-bible.md`,
the six `specs/2026-10-05-act-*-voices-design.md`, `docs/THE-SAGA.md`, `docs/SAGA-WALKTHROUGH.md`.

Role vocabulary: **speaker body**, **kill target** (chain step), **pool target** (heat side-task,
not the story), **step location**, **speaker spot**, **ask** (something a speaker/trader takes),
**recipe ingredient**, **source mesh** (saga item wears its model), **station**, **reward item**,
**trader voice**, **map pin**, **effect** (VFX/SFX/status effect), **system** (a vanilla mechanism
the saga drives).

---

## 1. Inventory

### 1a. The gods and their altars (the spine: one act per boss)

| Boss (prefab) | Altar location | Defeat key | Act | Roles |
|---|---|---|---|---|
| `Eikthyr` | `Eikthyrnir` | `defeated_eikthyr` | I | kill target (`mq-eikthyr`), step location (`mq-find`), map pin (Boss pin via `Game.DiscoverClosestLocation` when the find step opens), act boundary, dream key |
| `gd_king` (The Elder) | `GDKing` | `defeated_gdking` | II | same; altar pin is Haldor's reward (`bf-haldor-ask` → `bf-find`) |
| `Bonemass` | `Bonemass` | `defeated_bonemass` | III | same |
| `Dragon` (Moder) | `Dragonqueen` | `defeated_dragon` | IV | same |
| `GoblinKing` (Yagluth) | `GoblinKing` | `defeated_goblinking` | V | same; default final boss (`DefaultFinalBossKey`) |
| `SeekerQueen` | `Mistlands_DvergrBossEntrance1` | `defeated_queen` | VI | same (only reachable when `runFinalBossKey` is moved past Yagluth) |
| `Fader` | `FaderLocation` | `defeated_fader` | VII | same; Act VII's close is the saga's ending when Fader is last |
| `FrozenKing_p3` (3rd form) | `DN_Bossroom` | `defeated_frozenking_p3` | VIII | kill target (`dn-boss`) only. `FrozenKing` (chained, form 1) and `FrozenKing_p2` (summons `FrozenKing_P2_Summon_Eikthyr`…`_Fader`) are KNOWN but unused beyond the self-check |

Every boss also: `BossVigor` scales its max HP to the run's banked power (ZDO-stamped original);
`BossSpoils` hands out food after each kill; the Waystone boon recharges; class rungs are taught
"where the god falls" (`TeachClassRung`); `SagaDreams` keys dreams on the defeat keys.

**Guardian powers** (vanilla stones + boss trophy): every act from II to VII opens with
`X-power` = StatDelta `SetGuardianPower` ("Claim Eikthyr's power" … "Claim the Queen's power"), with a
hint describing the power. No Act VIII power step (Fader's power is never claimed).
`GP_Moder` is reused by `SagaWinds` as the icon/name for the **god's wind** and the **Wind-horn**
(two runtime SEs carrying `SailingPower`, the attribute Moder's power has).

**Summon items**: `TrophyDeer` (Eikthyr) is central to Act I; `WitheredBone` (`sw-find`, `sw-cull`),
`DragonEgg` (`mt-find`, `mt-cull`, and the frozen one's ask), `GoblinTotem` (`pl-find`,
`pl-berserker`) are handed out as rewards. `AncientSeed` is deliberately handed out NOWHERE
(comments l.12940, l.13019). The Queen's Sealbreaker is only alluded to ("a key the dvergr made");
Fader's bell and the Frozen King's summon are not mentioned.

**Boss drops** re-granted as step rewards (duplicating the vanilla drop): `PickaxeAntler`
(Eikthyr; not `HardAntler`), `CryptKey` (Elder), `Wishbone` (Bonemass; also named in `mt-silver`'s
hint), `DragonTear` (Moder, plus 2 from `mt-cull`). Yagluth's, the Queen's and Fader's drops: none.

### 1b. Speakers' bodies (vanilla creatures, tamed, immune, recoloured by `CreatureDressing`)

| Speaker | Body prefab (fallbacks) | Act | Where (vanilla place used) | Gate | Ask (vanilla items) | Gives |
|---|---|---|---|---|---|---|
| Hunter's shade (`HuntersShade`) | `Ghost` | I | near the claimed bed | night | `$item_flint` ×10, `$item_leatherscraps` ×5 | Thor's bow shape + 1 rescued light |
| Thjalfi | `Ghost` | I | a shoreline search (no location) | rain | `$item_stone` ×20 + 1 rescued light | raises the Storm-Anvil (`incinerator`) at his feet |
| The thane | `Ghost` | I (+return after gods) | ring 60-120 m round the bed (no vanilla graves used) | day | none | the seven ways (classes) |
| Barrow-keeper | `Ghost` | II | door of nearest `Crypt2` | none (place) | 1 rescued light (a courier's) | Stormsworn helm recipe |
| Drowned one | `Draugr` | III | door of nearest `SunkenCrypt4` | none | none (then asks to be killed: `__the_drowned`) | Stormsworn cuirass recipe; a light rises where he dies |
| Frozen one | `Ghost` | IV | nearest Mountain ground to the bed (`BiomeCompass`) | a burning `Fireplace` within 5 m | `$item_dragonegg` ×1 | Stormsworn greaves recipe |
| Harvester | `Ghost` | V | `StoneHenge1`-`6` (else Plains land) | none | eat a full table incl. `Bread`/`LoxPie`/`FishWraps` (`PlainsMeal`) | Stormsworn mantle recipe |
| Lantern-keeper | `Dverger` | VI | `Mistlands_DvergrTownEntrance1/2`, `_Excavation1-3`, `_Harbour1`, `_Lighthouse1`, `_Viaduct1/2`, `_GuardTower1-3_new` | night (to free) | 1 rescued light OR `$item_wisp` | Borrowed Light recipe |
| Charred one | `Charred_Melee` (`Charred_Archer`, `_Twitcher`, `_Mage`) | VII | dry AshLands ground near landing | a fire beside him | Thor's bow (any bow), flametal ×10 (`FlametalNew`/`Flametal`, resolved), 3 lights (rescued or wisp) | Last Light |

All are `SetTamed(true)` + `MakeImmune` (all damage types). The `Ghost` therefore does five jobs:
six speaker bodies, a light-visual fallback, a pool kill target (`k-ghost`).

### 1c. Traders (the game's own `Trader`, re-voiced via `TraderVoice`; shop untouched, restored after)

| Trader | Act | What the saga does with her/him |
|---|---|---|
| **Haldor** (`Vendor_BlackForest`) | II only | `bf-haldor` DiscoverLocation; 4 swapped `m_randomTalk` lines (couriers, trolls); a GIVE entry for `TrophyForestTroll` that sets a run-scoped key → `bf-haldor-ask`, and his accept line puts the Elder's altar on the map. Coin rewards (`bf-haldor` 300, `mq-troll` 150) feed his shop. Thunder Stone (the vanilla gate for the Obliterator) is mentioned only in comments/bible. |
| **Bog Witch** (`bogwitch` match) | III only | `sw-witch` proximity (6 m) "Find the Bog Witch"; 5 talk lines (preparation; "the big one is decided before you ever see him" → `sw-mead`); alt-use **reforge** of the Stormward: Stormward + `$item_iron` ×10 + `$item_elderbark` ×10 → Ironbound Stormward (`sw-reforge`). Her shop, her cauldron and her own wares: unused. |
| **Hildir** (`hildir` match) | IV and V | `pk-hildir` proximity; two talk sets (cold "keeps" / what a harvest leaves); her OWN vanilla chest quests, read from her `m_useItems` give entries at runtime: chest 2 Howling Cavern (`pk-chest`, PEAK track) and chest 3 Sealed Tower (`st-chest`, STEADING track). Chest 1 (Smouldering Tomb, Black Forest) unused; she is never met before Act IV. |

### 1d. Named and staged creatures (vanilla prefab, saga identity)

| Name in saga | Prefab | Act | Role |
|---|---|---|---|
| Eikthyr's herd / night deer | `Deer` | I | kill target (`__night_deer` synthetic, `__day_deer` for `mq-daylight`); a light rises from a night kill; lightning VFX over the carcass |
| Eikthyr's Herald | `Deer` (starred, dressed) | I | kill target (`EikthyrHerald`); its fall forces the `army_eikthyr` raid |
| Contested-kill packs | `Greyling`, `Greydwarf` | I | spawned at a night deer kill while the hunt is live, to race you for the light |
| The Gatherer | `Greydwarf_Elite` (fallback `Greydwarf`) | I | kill target (`__the_gatherer`), dressed gold, drops its hoard of lights; preceded by the forced `army_theelder` raid |
| The Breaker | `Troll` (spawned in the Meadows, faction Demon, unstarred) | I | timed kill target (`mq-troll`, 15 min); drops troll hide; the forest fights it |
| Couriers of the Elder | `Greydwarf` (renamed, dressed, passive) | II | kill target for `bf-intercept` (a Stolen Light rises where one falls); the barrow-keeper's ask |
| The forest's watchers | `Greydwarf` | II | `ForestWatch`: chopping summons some (atmosphere only) |
| The fen's risen | `Skeleton` from a killed `Draugr` | III | `FenWatch`: chance a draugr rises again as bone (atmosphere only) |

### 1e. Kill targets in the chains (vanilla creatures as "cull" quests)

- I: `Boar` ×4, `Greyling` ×4 (`mq-cull`); `Troll`; `Eikthyr`.
- II: `Greydwarf` ×10, `Greydwarf_Shaman` ×2 (`bf-cull`); `Greydwarf_Elite` ×3 (`bf-brute`); `gd_king`.
- III: `Draugr` ×8, `Blob` ×5, `Leech` ×3 (`sw-cull`); `Abomination` ×1; `Bonemass`.
- IV: `Wolf` ×6, `Hatchling` ×4, `Fenring` ×3 (`mt-cull`); `StoneGolem` ×2; `Dragon`.
- V: `Goblin` ×10, `Deathsquito` ×5, `Lox` ×3 (`pl-cull`); `GoblinBrute` ×2; `GoblinKing`.
- VI: `Seeker` ×8, `Tick` ×5 (`mi-cull`); `SeekerQueen`.
- VII: `Charred_Melee` ×8, `Volture` ×3 (`as-cull`); `Fader`.
- VIII: `FrozenKing_p3` only.

**Pool (heat side-tasks, MENTIONED level):** `Greydwarf`, `Skeleton`, `Troll`, `Draugr`, `Deer`,
`Greyling`, `Ghost`, `Surtling`, `Wolf`, `Goblin`, `Lox`, `Deathsquito`, `Boar`, `Neck`, `Leech`,
`Fenring`, `Greydwarf_Elite`; plus "Heed Hugin 5 times" (`RavenTalk`).

Not referenced anywhere in `RunMode/` (grepped): `Serpent`, `Bat`, `Ulv`, `Cultist`, `BlobTar`,
`Growth`, `Wraith`, `Gjall`, `Hare`, `Morgen`, `FallenValkyrie`, `Valkyrie`. Also unused as foes:
hostile dvergr, `Asksvin` (only a Menagerie pet), the Deep North's fauna; `Surtling` only in the pool.

### 1f. Locations (non-boss)

| Location | Act | Role |
|---|---|---|
| `Crypt2` (burial chambers) | II | step location (`bf-tomb`); barrow-keeper's spot; cores inside (`bf-crypt`) |
| `Vendor_BlackForest` | II | step location (`bf-haldor`) |
| `SunkenCrypt4` | III | drowned one's spot; scrap iron (`sw-scrap`, hint only) |
| Bog Witch's camp | III | found by proximity to her `Trader`, not by location name |
| Hildir's camp | IV, V | proximity to her `Trader` |
| Howling Cavern (Hildir chest 2) | IV | via her give-entry key (`pk-chest`) |
| Sealed Tower (Hildir chest 3) | V | via her give-entry key (`st-chest`) |
| `StoneHenge1`-`6` | V | harvester's spot |
| Dvergr sites (12 names, above) | VI | lantern-keeper's spot |
| Vegvisirs | all | left working; a read Vegvisir's Boss pin counts for the god's wind. Never placed or read by the saga |

Biomes: `ReachBiome` opens each act (BlackForest, Swamp, Mountain, Plains, Mistlands, AshLands,
DeepNorth). `BiomeCompass` gives bearings. Thjalfi's shore, the thane's ring and the shade's bed are
saga-chosen spots, not vanilla locations.

### 1g. Building pieces and stations

| Piece | Act | Role |
|---|---|---|
| Workbench, hammer, axe (crafts), `StationUpgrade` ×2 (chopping block, tanning rack) | I | steps `mq-bench`, `mq-axe`, `mq-hammer`, `mq-upgrade` |
| Fire (campfire), cooking station, bed (spawn point), chest, roof pieces, chair/table (comfort) | I | HEARTH steps; fire also wakes the frozen one (IV) and lights the charred one's pyre (VII) |
| **Incinerator / Obliterator** → "Storm-Anvil" | I (+ repairs later) | station for Thor's bow and the Stormward (`AnvilCombines` appended to `m_conversions`), repair of both storm shields; raised by Thjalfi; renamed via `Piece.m_name` (lever hover still says Obliterator) |
| Item stand | II | `bf-trophy` (ItemStandUses) |
| Raft / any `Ship` | II | `bf-raft`; ship fittings + Wind-horn at the helm (`ShipControlls` hover); `m_ashlandsReady` for Fire-tar (VII) |
| Cart | II | `bf-cart` |
| Smelter | II | `bf-smelter` |
| Portal | II | `bf-portal` |
| Cultivated crops | II | `bf-plant` (10 seeds) |
| Forge (`forge`) lv1/lv2/lv4 | II-IV | station for Stormsworn helm, cuirass, greaves; also Field Forge boon spawns one |
| Fermenter, cauldron | III | `sw-fermenter`, `sw-mead` |
| Windmill | V | `pl-windmill` |
| Artisan table (`piece_artisanstation`) | V | Stormsworn mantle |
| Galdr table (`piece_magetable`) | VI | Borrowed Light |
| Workbench (`piece_workbench`) | class | Ulfr's axes (Berserker) |

### 1h. Items

**Source meshes (saga items are vanilla prefabs wearing new numbers; decision #2):**
Thor's bow ← `BowHuntsman` (`BowDraugrFang`, `BowFineWood`, `Bow`); Last Light ← `BowAshlands`
(`BowSpineSnap`, …); Stormward ← `ShieldFlametalTower` (many tower-shield fallbacks); Ironbound
Stormward ← `ShieldBlackmetalTower`; Stormsworn helm ← `HelmetBronze`; cuirass ← `ArmorIronChest`;
greaves ← `ArmorWolfLegs`; mantle ← `CapeLox`; Ulfr's axes ← `AxeBerzerkr`; Rescued light ← `Wisp`
item (`GreydwarfEye`, `SurtlingCore`); Borrowed Light ← `Demister`.

**Recipe ingredients / asks:**
Storm-Anvil bills: Wood, Resin, TrollHide, DeerHide (+3 lights) for the Stormward; Wood, Resin,
DeerHide, Flint (+3 lights) for the bow. Stormsworn: Bronze/TrollHide/Coal; Iron/Guck/LeatherScraps;
Silver/WolfPelt/WolfFang; LoxPelt/Needle/Silver. Borrowed Light: Wisp ×5, Silver ×12. Ulfr's axes:
Wood, Flint, LeatherScraps, DeerHide. Reforge: Iron, ElderBark. Last Light: flametal. Speaker asks:
Flint, LeatherScraps, Stone, DragonEgg, Wisp, TrophyForestTroll; `SurtlingCore` ×10 (`bf-crypt`),
`IronScrap` ×20, `Iron` ×10, `SilverOre` ×15, poison mead base (`sw-mead`) as CollectItem steps.

**Reward items (`QuestRewards`, `BossSpoils`):** gear (Bow, ArmorLeather*, HelmetLeather,
CapeDeerHide, ShieldWood, FishingRod, BowFineWood, PickaxeAntler, ShieldBronzeBuckler,
ArmorRoot*, ShieldIronTower, MaceIron, ArmorWolf*, SwordSilver, ArmorPadded*, SwordBlackmetal,
CapeLox), arrows (Wood, Flint, Bronze, Iron, Frost, Needle), meads (HealthMedium, PoisonResist,
FrostResist), foods (CookedMeat, Honey, Sausages, CarrotSoup, TurnipStew, SerpentStew,
WolfMeatSkewer, OnionSoup, LoxPie, Bread, MeatPlatter, SeekerAspic, BloodPudding, Raspberry,
Mushroom, Carrot, LoxMeat), materials (Wood, Stone, Resin, Flint, DeerHide, TrollHide, Coal, Amber,
CopperOre, TinOre, Bronze, BronzeNails, IronNails, Iron, Silver, BlackMetal, FineWood,
SurtlingCore, GreydwarfEye, Crystal, WolfPelt, Barley, BarleyFlour, Thistle, CarrotSeeds,
FishingBait, Coins, Torch), summon items (TrophyDeer, WitheredBone, DragonEgg, GoblinTotem),
boss drops (CryptKey, Wishbone, DragonTear). Acts VI-VIII grant almost nothing beyond the power
step's food.

### 1i. Effects, systems and other vanilla surfaces

| Thing | Act | Role |
|---|---|---|
| **Hugin** (`Tutorial.m_ravenPrefab` + `Raven.AddTempText(..., "Hugin", munin:false)`) | all | the narrator: act-card `RavenLine` per act, `mq-errand` "Hear the raven out", the pale light, every recipe `TaughtLine`, class rungs, ship fittings, god's wind; pool task "Heed Hugin 5 times" |
| **Munin** | none | explicitly excluded (`munin:false`) |
| Raid `army_eikthyr` | I | forced at the Herald's carcass ("The meadows heard that") |
| Raid `army_theelder` | I | forced before the Gatherer arrives ("The forest is moving") |
| Dreams (`SleepText.m_dreamTexts`) | I-VI | vanilla dreams set aside during a run; 7 saga dreams keyed on defeat keys (none for VII/VIII) |
| Weather (`EnvMan` env name containing "thunder"; rain) | I | `mq-storm-vigil`; Thjalfi appears and the anvil works only in rain |
| Day/night | I, VI | shade at night, thane by day, lights rise only in the dark |
| `fx_eikthyr_stomp` / `vfx_lightning` / `lightningAOE` | I+ | Herald carcass lightning, Stormward discharge, Thor's bow strike, Thor's Wrath |
| Fire/frost VFX (`fx_fireball_staff_explosion`, `fx_shaman_fireball_expl`, `vfx_FireballHit`, `fx_DvergerMage_Fire_hit`, `fx_iceshard_hit`, `fx_DvergerMage_Ice_hit`, `vfx_ColdBall_Hit`, `vfx_dragon_ice_hit`, `vfx_frostarrow_hit`, sfx) | Hunter class | Elemental Arrows bursts |
| `GP_Moder` / `SailingPower` | II+ (once a god is pinned) | god's wind (prow within 40° of the altar) and Wind-horn |
| SEs `Burning`, `Rested`, custom `SE_Stats` | boons | Emberskin, Saga of Bragi, Hunter's hush |
| Creatures as boon summons: `Wolf` (Packbrother), `Skeleton` (Bonecaller), `Boar`/`Wolf`/`Lox`/`Hen`/`Chicken`/`Asksvin` (Menagerie) | classes | effect |
| World modifiers (`ResourceRate`, `SkillGainRate`, stamina rates, `EnemyDamage`, `EnemyLevelUpRate`, `NoBuildCost`) | all | heat and empowerment; originals restored |
| Map pins | all | Boss pin per act when the find step opens; unsaved dots for speakers |
| Skills window | classes | the way shown as a readout skill |
| Fishing, taming, foraging, comfort, rested, food slots, sleep, `TimeInBase`, `PlayerStatType` stats | I (mostly) | HEARTH and CRAFT step measures |

---

## 2. Depth of use, for the notable things

NONE / MENTIONED (a name in a list) / ONE BEAT (a single step or line) / A ROLE (recurring, characterful).

| Thing | Depth | Detail |
|---|---|---|
| Haldor | **A ROLE, Act II only** | voice, troll-head ask, the altar pin. Silent in every other act; his wares never asked for |
| Hildir | **A ROLE, Acts IV-V** | two voices, two of her three chests as optional tracks. Never met in the meadows/forest where vanilla puts her first; chest 1 unused |
| Bog Witch | **A ROLE, Act III only** | voice, find step, mead nudge, the reforge. Her shop/cauldron/feasts untouched |
| Obliterator (Storm-Anvil) | **A ROLE in Act I; ONE BEAT after** | the forge of Act I's two storm items, raised by Thjalfi, weather-gated. After Act I only the shield REPAIR uses it; the bible's "still to come" (Stormsworn bound there, Act III reforge there) went to the forge and the witch instead. Its vanilla coal conversions untouched |
| Hugin | **A ROLE (all acts)** | the saga's narrator; act cards, teachings, errand |
| Munin | **NONE** | excluded on purpose |
| Odin | **MENTIONED** | the frame (ravens his audit, boons his loans, Menagerie "Odin lends"), act raven lines, the myth's ending; never present. Vanilla's hooded wanderer unused |
| Valkyrie | **NONE** | not in code or docs. The story has the SEA deliver the player ("the sea put down a living person"), and Thjalfi stands on the shore for that reason |
| Dvergr | **A ROLE, Act VI** (thematically the arc's payoff) | lantern-keeper (`Dverger` body), dvergr sites, galdr table, Borrowed Light; dvergr VFX on arrows. Act VI is otherwise thin (Seekers, Ticks, the Queen); dvergr as a people/faction, their towns' hostility, black cores, Sealbreaker: MENTIONED at most |
| Trophies | **A ROLE for `TrophyDeer` (Act I)**; ONE BEAT for `TrophyForestTroll` (Haldor); boss trophies ONE BEAT per act (power steps); all others NONE | `bf-trophy` hangs any trophy |
| Boss items (drops/summons) | **MENTIONED** (rewards duplicating drops), except `DragonEgg` = **ONE BEAT** (the frozen one's ask, Moder's guarded light) and `Wishbone` named in a hint | Yagluth/Queen/Fader drops NONE |
| Bosses' powers | **ONE BEAT per act** (`X-power` step); **Moder's = A ROLE** (lent as the god's wind and the horn) | no other power's effect is used |
| Deep North / Frozen King | **MENTIONED** | Act VIII = reach the biome + kill `FrozenKing_p3`; names validated, no speaker, no story beyond the epigraph/chapter; forms 1-2 and the P2 god summons unused; decision #3 unresolved |
| Ghost (creature) | A ROLE (as a costume) | six speakers' bodies + light fallback; never a ghost as such |
| Draugr / Skeleton | A ROLE in Act III | drowned one's body, cull, FenWatch rising |
| Greydwarves | A ROLE in Acts I-II | contest packs, Gatherer, couriers, watchers, brutes, shamans |
| Troll | A ROLE (Act I-II) | the Breaker; its hide and head feed Stormward, helm, Haldor |
| Deer | A ROLE (Act I) | the herd, the Herald, the lights |
| Raids | ONE BEAT each (Act I) | `army_eikthyr`, `army_theelder`; no raids after Act I |
| Dreams | A ROLE (quiet) | one per act I-V, one after |

---

## 3. What each act leans on

- **I. The Stolen Light** (fullest act): Deer/Herald/lights (Wisp), Greydwarf contests and the Gatherer,
  the Breaker (Troll), Hugin's errand, the Obliterator as Storm-Anvil (Thjalfi, rain, thunderstorm
  vigil), three Ghost speakers, TrophyDeer, two forced raids, the whole hearth (fire, bed, comfort,
  fishing, taming), `Eikthyrnir`.
- **II. Where the Light Goes**: Greydwarf couriers/brutes/shamans, `Crypt2` + surtling cores +
  barrow-keeper, Haldor + troll trophy (altar pin), smelter/bronze/forge, raft, cart, portal, crops,
  item stand, ForestWatch, `GDKing`.
- **III. Nothing Stays Buried**: Draugr/Blob/Leech/Abomination, `SunkenCrypt4` + drowned one (Draugr
  body), Bog Witch (talk, mead, reforge with iron + ancient bark), fermenter/cauldron, sailing the
  fens, FenWatch skeletons, forge lv2, `Bonemass`.
- **IV. The White Silence**: wolves/drakes/fenrings/golems, silver + wishbone, frozen one (fire +
  DragonEgg), Hildir + Howling Cavern chest (PEAK), forge lv4, `Dragonqueen`.
- **V. The Golden Ruin**: fulings/deathsquitos/lox/berserkers, `StoneHenge` + harvester, plains foods
  (bread, lox pie, fish wraps), Hildir + Sealed Tower chest, windmill, artisan table, feast at home
  (STEADING), GoblinTotem, `GoblinKing`.
- **VI. A Light to Carry** (thin): Seekers/Ticks, dvergr sites + lantern-keeper (Dverger body), wisp,
  galdr table + Demister mesh (Borrowed Light), the Queen's door.
- **VII. The Last Light** (thin): Charred/Voltures, charred one (Charred body) on Ashlands shore, a
  fire, flametal, BowAshlands mesh (Last Light), Fire-tar ship fitting, `FaderLocation`.
- **VIII. What the Cold Keeps** (stand-in): DeepNorth biome, `FrozenKing_p3`, `DN_Bossroom`. Nothing else.

---

## 4. Constraints on any new use

**From the story bible (`specs/2026-08-27-story-bible.md`, "Rules this premise imposes"):**
1. Light rises only in the dark, everywhere, always (lore, not tuning). Code must keep it: the
   drowned one's light and the lantern-keeper's freeing wait for night.
2. The forest's creatures converge on light out of hunger; they carry, never destroy. Only the troll
   (the Breaker) breaks light; nothing taking a light may read as vandalism.
3. The player's own light is never mechanised.
4. Ravens appear at moments Odin would want witnessed, and **never help**.
5. Killing a god settles its hoard but never solves the shortage; only Act VII answers it.
6. **No line the world does not back**: every hint, whisper and message describes something that
   actually happens.
7. The story is told where the player is looking (carcasses, arrivals, deaths), never in a wall of
   text; the lobby paragraph is the only exposition allowed.
8. Three voices: the epigraph (present-tense order), a character (present tense, spoken), the
   chapter (past tense, BOOK only).
9. Gods only speak when defeated; Thjalfi may speak only because he is not a god.
10. Every speaker must read as a different person (distinct palette and gate: dark, weather, day,
    place); Thjalfi and the shade must never be dressed alike.
11. Named vanilla traders: only their idle talk changes; the counter stays their own; restored after.

**No spoilers (memory `saga-no-spoilers`; RESUME "Game facts"):** nothing the saga shows may name an
item, recipe, speaker or act before the story reaches it. Gate each entry on the step that tells
it (`StepLive`/`RecipeStepDone`), default to hidden. Bench recipes appear once Hugin announces them;
anvil shapes once `AnvilTaughtBy` says their step opened; repairs never. A new anvil shape with no
teacher stays hidden. Altar pins wait for the altar's discovery step ("the map is never ahead of the
questline"). The Wind-horn is deliberately not named for Moder (that would spoil her in Act II).

**Four decisions taken 2026-09-20 (RESUME):**
1. Act I's light economy stays exactly even (seven lights needed, seven available, the Gatherer's
   hoard the only slack).
2. **No AssetBundle pipeline. Reuse what ships.** Every saga asset is an existing prefab with
   different numbers; only the four `LevelEffects` shader properties (`_Hue`, `_Saturation`,
   `_Value`, `_EmissionColor`) may change a creature's look. No new meshes, no item tints, no
   in-world book.
3. The Deep North is an epilogue, not Act VIII: one beat, reachable only after Fader. (Still
   unresolved; the code has Act VIII.)
4. The feast: empty named high seats by default, one per felled god; a god comes only if something
   specific was done in its act. The empty path must be complete and winnable alone. Build order:
   the shade's seat, the five empty seats, then ONE god as an experiment (a boss fighting for you
   needs its faction flipped and can look ridiculous). Not yet built.

**Game facts (RESUME, 2026-10-05):** tamed = enemy of every monster, so every speaker is immune
(`SagaSpeaker.MakeImmune`); `DamageModifiers.m_nonPlayer` is a damage type; `WorldGenerator.GetBiome`
calls the boiling sea AshLands (need dry ground); hammer previews carry a `Fireplace` with no valid
`ZNetView`; a `StolenLights` release is a pickable item (a freed light must be the visual-only
`RisingLight`); `StepPredicates.StepDone` reads only the current act (use `RecipeStepDone` for
anything that outlives its act); never put a collider under a ship (`Ship.OnTriggerExit` throws the
player overboard); asset names are verified at RUN time (a new guessed name belongs in the
self-check); check the IL before trusting a name or field.

**Standing content rules (RESUME, build notes, act specs):**
- Thin is honest, absent is a bug; an ask that cannot be paid stalls an act, so every price has a way
  through (lights = rescued OR wisp; any bow if Thor's is gone).
- Stormsworn recipes cost no rescued lights; lights belong to Acts I-II.
- The chain asks for destinations, the pool asks for transport (no boat step in a chain).
- A build category may appear in ONE act only (`ValidateActs`).
- Anything a run grants must be given back, correctly; the world is left exactly as found (Vegvisirs
  left alone, world keys scoped to the run, trader talk restored, ship numbers restored).
- Dress creatures with `CreatureDressing.ApplyWhenSettled`, never at spawn.
- Atmosphere watchers fire on a chance, never every time ("a summon every time is a tax").
- Act VI's LANTERN track waits until someone has played the Mistlands; no hard gate on Bonemass;
  Act III's cartography-table step was cut.
- Storm shields mend at the Storm-Anvil only (alone in the box, in the rain); no bench repairs them.

---

## 5. Loose ends noticed in passing

- `bf-find`'s RewardText still says "the seeds are the troll's to give", but `mq-troll` (now Act I)
  pays no `AncientSeed` and the comments say so. A claim the code contradicts.
- `docs/SAGA-WALKTHROUGH.md` still heads Act III "as designed — not yet built" and VI-VII
  "stand-in"; the code has all three voiced.
- The bible's Storm-Anvil entry still lists "Stormsworn bound there, and the Stormward's Act III
  reforge" as still to come; both went elsewhere (forge, Bog Witch).
- Decision #3 (Deep North as epilogue) vs the coded Act VIII: unresolved, the owner's call.
