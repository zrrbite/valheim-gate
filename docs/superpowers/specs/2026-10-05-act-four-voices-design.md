# Act IV gets voices — the frozen one, and Hildir's cold

Written 2026-10-05, the third act given voices that day, after Act II
(`2026-10-05-act-two-voices-design.md`) and Act III (`2026-10-05-act-three-voices-design.md`). It
reuses their machinery: `SagaSpeaker` for the new speaker and the trader voice (`TraderVoice`,
built in Act III's work) for Hildir.

## Why

Act IV, *The White Silence*, had eight steps, no speaker, a plain-recipe Stormsworn piece, and its
story in two openings. The bible's answer for the act is **it is already gone**: the mountains froze
when their light ran out, and Moder guards eggs — light that has not woken. The act's own close
already says the thing worth building towards: what the mountain was keeping *was still warm*.

## Decisions taken (owner, 2026-10-05)

| Question | Answer |
|---|---|
| Who carries the act | **Hildir + a frozen speaker** |
| The frozen one's beats | **Fire to wake him, an egg for his ask** |
| What he gives | **The Stormsworn greaves**, taught |
| Hildir's thread | **Her frost-cave chest as a saga step** — her own vanilla quest |
| Where Hildir's errand lives | **A new third track, PEAK**, so it never holds up Moder |

## The story

| Beat | What the player learns |
|---|---|
| The frozen one | Someone climbed to see the light come back, and froze at the treeline still believing |
| Waking him | A fire is enough to reach a light that is only frozen, not gone |
| The egg | Light that has not woken yet — the thing he climbed for, and what Moder guards |
| Hildir | The cold does not steal. It keeps. |
| Moder (exists) | The mountain gives up what it kept, and it is still warm |

### The frozen one

**Unnamed.** The Ghost, in a fifth palette: frost-white body (Value up, Saturation gone) with a
**warm amber core** — the light still warm inside. Warm where the shade is cold blue; nobody mistakes
the two.

- **Where:** at the treeline — the nearest MOUNTAIN ground to the player's claimed bed (the bed's
  position fed to `BiomeCompass`'s nearest-edge search), then a short ring search for footing. The
  epigraph is *"Above the treeline, even light freezes."* He is exactly there.
- **Wanted:** from `mt-arrive` done until the run ends.
- **Phases:**
  - **Frozen** — `mt-frozen` live, no fire. Use: *"He does not move. The ice is thick."* Hover says
    so. The strip's bearing reads "Someone stands in the ice at the treeline".
  - **Waking** — `mt-frozen` live and a **burning `Fireplace` within 5 m** of him (any fire the
    player builds: campfire, hearth, brazier — the `Fireplace` component, polled at 1 Hz near him
    only). Use: his first speech; `FrozenWoken` → `mt-frozen` done.
  - **Ask** — `mt-egg` live: *one dragon egg*, carried down to him. Taken from the pack
    (`$item_dragonegg`, by shared name, validated). `FrozenPaid` → `mt-egg` done.
  - **Idle / After** — an idle line; after Moder, one remark: the mountain gave up what it kept, and
    it was warm — he was right, and it did not help him.
- **First speech (meaning):** he climbed when the light started going, to see it come back. He
  stopped here, where the cold begins, and has been waiting. He will not believe it is gone. He asks
  to SEE a light that has not woken — an egg, from the peaks above.
- **What it costs:** an egg weighs 200 and cannot go through a portal, so carrying one DOWN is a
  real haul — and it is one of the three Moder's summoning wants. That is the trade, deliberately.
- **What he gives:** the **Stormsworn greaves**; their recipe gate moves from `mt-silver` to
  `mt-egg`. (Their cost — silver — still makes `mt-silver` matter.)

### Hildir

The game's own trader, voiced for Act IV as Haldor is for Act II and the Bog Witch for Act III.

- **Idle talk (meaning):** the cold does not steal, it keeps — nothing up there rots, nothing up
  there is used. She keeps her camp where it is warm and sends others up into it. Her shop and her
  own quest dialogue stay hers.
- **Found:** `pk-hildir` completes when the player comes within a few metres of her (the voice's
  proximity event, as the Bog Witch's).
- **Her errand:** her own vanilla quest — the chest lost in the **Howling Cavern**, the mountain frost
  cave (fenrings and cultists). Returning it is `pk-chest`.
- **How it is detected:** Hildir's own `m_useItems` entries are asset data: each lost chest is a
  `TraderUseItem` whose `m_setsGlobalKey` is set when she accepts it. The voice LOGS all of them on
  attach (prefab name, key) and selects the Howling Cavern's by its prefab name; `pk-chest` completes
  when that key is set. `Minimap.PinType.Hildir2` confirms the game numbers the chests 1–3, and
  `Room.Theme.CaveHildir` that the cave is real; neither names the key, hence the runtime read.
- **Already returned before the run?** The key is world state, so a world that already returned the
  chest completes `pk-chest` on sight. Accepted: the work was done, by this player, in this world.

## The chain

### CRAFT

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Claim Bonemass' power | StatDelta | `mt-power` |
| 2 | Reach the Mountains | ReachBiome | `mt-arrive` |
| 3 | **Wake the frozen one** | PlayerEvent `FrozenWoken` | **new** `mt-frozen`; hint: build a fire beside him |
| 4 | Bring up 15 Silver Ore | CollectItem | `mt-silver` |
| 5 | **Carry an egg down to him** | PlayerEvent `FrozenPaid` | **new** `mt-egg`; hint: the peaks, heavy, no portal |
| 6 | Work the Stormsworn greaves | CollectItem | `mt-storm`; recipe now taught by him |

### HUNT — unchanged

Hunt the white silence → kill 2 Stone Golems → find Moder's altar → defeat Moder.

### PEAK — new third track (`peak`)

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | **Find Hildir** | PlayerEvent `HildirMet` | **new** `pk-hildir`; she is in the Meadows — Homeward is the cheap way |
| 2 | **Bring back her chest from the Howling Cavern** | PlayerEvent `HildirChest` | **new** `pk-chest` |

A third track, not a gate: the trip home and the cave are optional heat, the dual-path dial the
owner named in Act I. `peak` joins `TrackTable` (the carried-tracks cap of two still applies).

**8 steps → 12.** Four more completions, so Act IV's questline heat rises by four steps' worth —
the act was the thinnest in the saga.

## The code

- **`FrozenOne`** — new, on `SagaSpeaker`. Spot from `BiomeCompass` (mountain, from the bed);
  phases above; a `FireNear(spot, 5f)` poll over `Fireplace` instances, only while `mt-frozen` is
  live; price `("$item_dragonegg", 1, "dragon egg")`.
- **`CreatureDressing.Frozen()`** — the fifth palette.
- **`TraderVoice`** (from Act III) — a third configuration: Hildir, matched on "hildir"; talk lines;
  proximity event; and a new option, **watch a vanilla give entry** by prefab-name match, reporting
  when its key is set. Her entries are logged on attach.
- **`RunService`** — chain edits; `peak` track; greaves gate; `PollFrozenOne`, Hildir's voice in
  Act IV; validators (spawn events `FrozenWoken`, `FrozenPaid`, `HildirMet`, `HildirChest`; the egg
  price); the location registry line gains "Hildir" and "Cave".
- **`StepPredicates`** — `Frozen*`, `HildirFind`, `HildirChest`, tested.

## Verification in the first run

1. The frozen one's spot is reachable — logged with distance from the bed and the biome found.
2. Hildir's `m_useItems` — the chest prefabs and their keys (logged on attach); the selector picks
   the cavern's.
3. Whether her vanilla quest gates the cavern chest on an earlier one (if so, the hint says so).
4. `$item_dragonegg` — the price validator.

## Documentation

Story bible glossary (the frozen one, Hildir in the saga), Act IV's "as built" paragraph, atlas
regenerated and republished, `THE-SAGA.md`'s Act IV chapter rewritten, RESUME and a HANDOFF task.

## Not in this work

- Moving Act I's speakers onto `SagaSpeaker`.
- Wolves and frost mead from the old PEAK plan — they stay ideas; this PEAK is Hildir's.
