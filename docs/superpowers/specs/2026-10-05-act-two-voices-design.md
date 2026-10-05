# Act II gets voices — the barrow-keeper, Haldor, and a chain with reasons

Written 2026-10-05, agreed with the owner in one brainstorming session. Builds on the story
bible (`2026-08-27-story-bible.md`), the questline plans (`2026-08-23-act-questline-plans.md`)
and Act I's speakers (`HuntersShade`, `Thjalfi`, `Thane`).

## Why

Owner, after playing Act II: the story is thin. Asked what made it so, he picked all four:

1. **Nobody to talk to.** Act I has three speakers; Act II has none — Haldor is a shop.
2. **Steps are chores.** Sign, cart, ten seeds, beehive, four penned boar: vanilla progression
   with a checkbox and no reason.
3. **No item-quest journey.** Act I's bow was person → gift → recipe → craft. Act II's
   Stormsworn helm is an open recipe at the forge.
4. **The light story stalls.** The act is called *Where the Light Goes*, and after the couriers
   nothing more is revealed until the Elder burns.

**Success:** Act II has two people in it, each with an errand that matters to the act's
question; the item quest has Act I's shape; the chores that remain have a reason in someone's
words; and the act reveals its answer in stages instead of all at the boss.

## Decisions taken (owner, 2026-10-05)

| Question | Answer |
|---|---|
| Who carries the act | **Haldor plus one new speaker** |
| The new speaker | **The barrow-keeper** |
| What the keeper asks | **A light from the couriers** |
| Haldor's thread | **He has seen where the light goes** |
| The chores | **Trim and re-voice** |
| How speakers are built | **A shared `SagaSpeaker` base, used by the keeper only** — Act I's three are left alone |
| The Elder's altar pin | **Moves to Haldor** — no pin until his lead |

## The story

Everything here must agree with Act II's `ChapterClose`: the light went *nowhere* — spent on
keeping deadwood standing. Nobody in this act knows that before the Elder burns.

The act now reveals in four moves, one per beat:

| Beat | What the player learns |
|---|---|
| The couriers (exists) | The light is **carried** — at night, by named splinters |
| The barrow-keeper | Some light was **kept back** — buried by the dead, still burning |
| Haldor | **Where** it is carried — to the oldest trees, and the altar among them |
| The Elder (exists) | It was **spent** — and the world is darker for his death |

### The barrow-keeper

One of the dead of the burial chambers. **Unnamed**, like the shade and the thane — only
Thjalfi has a name, because somebody wrote his down.

- **Where:** at the door of the burial chamber (`Crypt2`) nearest the player, standing on
  level dry ground just outside it. **No time or weather gate**: the dead below ground do not
  keep the sky's hours. He is the one Act II speaker found by going somewhere, not waiting for
  something — the shade wants dark, Thjalfi rain, the thane day; the keeper wants a place.
- **Body:** the Ghost, a fourth palette distinct from the other three (shade cold and dim,
  Thjalfi gold and large, thane moss-green). Proposed: grave-ash grey with a faint green
  core-fire glow — the colour of the fires in the chamber walls. Dressed through
  `CreatureDressing.ApplyWhenSettled` (the landmine: dressing at spawn leaks into
  `LevelEffects`' static cache).
- **When he speaks:** once the player has taken cores from the dead (`bf-crypt` done). Before
  that, he is not there — nothing to say to someone who has not taken anything yet.
- **First words (the meaning, not final text):** the cores were theirs. Every green fire in
  those walls is a light someone buried rather than let the little ones carry it off, and the
  dead have kept them lit since. He does not forbid the smelter — the living need fire — but
  something taken should be given back.
- **His ask:** one rescued light, carried down to him. Taken from the pack by the interact,
  exactly like Thjalfi's price (`SagaItems.RescuedLightName`, by display name).
- **What he gives:** the **Stormsworn helm**, taught. Its recipe is registered only while
  `bf-keeper-light` is done, the way Thor's bow's is (`StepPredicates.StepDone`, derived from
  the tracks, so a resume re-teaches it and nothing new is saved).
- **After the Elder falls:** one return remark, like the shade's — the buried lights are the
  only ones left burning in the forest.

### Haldor

The game's own trader, given a voice for Act II of a run and returned to himself afterwards.

- **Idle talk:** the couriers. They pass his camp every night, laden, and never stop for him.
- **Why he is never robbed:** splinters carry light and do not take goods. The one thing in
  the forest he fears is a **troll** — in the bible, the only thing that *breaks* light instead
  of carrying it.
- **His ask:** a **troll trophy** (`TrophyForestTroll`), given through his own give-item
  mechanism — the player selects the trophy from the hotbar or inventory while looking at him.
  The step's hint says exactly that, since vanilla never teaches it this early.
- **What he gives:** where the couriers go — the oldest trees feed first — and with it the
  **Elder's altar pin**. Said through his accept line (`TraderUseItem.m_dialog`).
- **The rest of his talk** (greet, goodbye, buy, sell) stays vanilla. Only what serves the
  story changes.

## The chain

### HUNT — the robbery leads to Haldor, and Haldor to the altar

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Thin the forest | KillPrefab (composite) | `bf-cull`, unchanged |
| 2 | Fell 3 Brutes | KillPrefab | `bf-brute`, unchanged |
| 3 | Rob the couriers (4 lights) | PlayerEvent | `bf-intercept`, unchanged |
| 4 | Find the trader | DiscoverLocation `Vendor_BlackForest` | `bf-haldor`, **moved** from FORGE (explicit `Track = HuntTrackId`) |
| 5 | **Bring Haldor a troll's trophy** | PlayerEvent | **new** `bf-haldor-ask` |
| 6 | Find the Elder's altar | DiscoverLocation `GDKing` | `bf-find`; **the pin appears only once step 5 is done** |
| 7 | Defeat the Elder | KillPrefab `gd_king` | `bf-elder`, unchanged |

Haldor is on HUNT on purpose: a hunt-only player would otherwise reach step 6 with no pin and
no reason. Waystone still skips the discovery legitimately, as it does in every act.

### CRAFT — the keeper sits inside the smelting

| # | Step | Kind | Notes |
|---|---|---|---|
| 1 | Reach the Black Forest | ReachBiome | `bf-arrive` |
| 2 | Mine (40 hits) | StatDelta | `bf-copper` |
| 3 | Find the burial chambers | DiscoverLocation `Crypt2` | `bf-tomb` |
| 4 | Take cores from the dead (10) | CollectItem | `bf-crypt` |
| 5 | **Speak with the barrow-keeper** | PlayerEvent | **new** `bf-keeper` |
| 6 | Build a smelter | BuildPiece | `bf-smelter`, re-voiced |
| 7 | Forge three things in bronze | StatDelta | `bf-bronze`, re-voiced |
| 8 | **Carry a light down to him** | PlayerEvent | **new** `bf-keeper-light`; hint says the couriers carry them, at night |
| 9 | Beat out the Stormsworn helm | CollectItem | `bf-storm`; recipe now **taught**, not open |
| 10 | Build a portal | BuildPiece | `bf-portal`, re-voiced |
| 11 | Plant a crop (10 seeds) | BuildPiece | `bf-plant`; reward text loses "a queen for a hive" |

Step 8 depends on the hunt track's couriers. It cannot stall: couriers run every night of
Act II, and lights only exist in Acts I–II, which is where this ask lives.

### FORGE — what is left

Claim Eikthyr's power (`bf-power`) → hang a trophy (`bf-trophy`) → raise a raft (`bf-raft`)
→ raise a cart (`bf-cart`, re-voiced).

### Cut

`bf-sign` (name your holding), `bf-bees` (beehive), `bf-herd` (four penned). Three out, three
in: **22 steps before and after, so Act II's questline heat is unchanged.** Cutting is safe
under the one-act-per-build-category rule (`ValidateActs`), which only constrains additions.

### Re-voicing

Through the existing `ChallengeDefinition.Opening` — said once, when the step becomes current.
Proposed meanings, final text at build time under the bible's three-voices rule:

- `bf-smelter`: the cores burn as well in a smelter as in a wall — know what you are burning.
- `bf-bronze`: the forest's first real industry; the living make things, the dead keep them.
- `bf-cart`, `bf-portal`: goods have to move, the way the couriers move theirs.

**No line the world does not back:** each must describe something that actually happens.

## The code

### `SagaSpeaker` — new, `RunMode/Unity/SagaSpeaker.cs`

The machinery Act I's three speakers each carry a copy of, once:

- the talk component on a CHILD object with its own trigger collider (the creature's own
  Hoverable on the root otherwise wins the prompt — see the item-quest notes);
- spawn the Ghost body when the player is near and the speaker is wanted, non-persistent,
  re-made as needed; dress through `ApplyWhenSettled`; dismiss;
- the greeting bubble once per phase within range;
- the bearing line for the strip, and the map pin through the existing `RefreshNpcPin`;
- paying a price from the pack by shared name, and joining `ValidateQuestPrices`.

A subclass supplies its spot, its gate, its lines, its price, its palette, and what it raises
when spoken to and when paid. **Act I's speakers are not moved onto it in this work** — they
are verified in play. Later acts' speakers use it; migrating Act I is a separate change once
the base has been played.

### `BarrowKeeper` — new, on `SagaSpeaker`

- **Spot:** the nearest `Crypt2` location instance, found the same way `bf-tomb`'s discovery
  finds it; then a flat-dry-ground ring search around the door, as `Thane.TryRing` does,
  because chamber doors sit in hillsides. Cached like `BossAltarIn`, never per frame.
- **Wanted:** from `bf-crypt` done until the run ends, in every act after it (like `PollThane`,
  since `StepDone` only reads the current act's chains). After `defeated_gdking` he gives the
  return remark once, then an idle line.
- **Raises:** `SagaNames.KeeperFound` on speaking (→ `bf-keeper`), `SagaNames.KeeperPaid` on
  payment (→ `bf-keeper-light`).
- **Recipe:** the Stormsworn helm's recipe moves into `SagaRecipes`, registered while
  `bf-keeper-light` is done.

### `HaldorVoice` — new, small, not a speaker

Attaches to the game's own `Trader` whose `m_name` is Haldor's, whenever one is loaded near
the player, in Act II of a run.

- **Talk:** saves `m_randomTalk` and swaps in the saga's lines; restores on act change, run end
  and when he unloads. Greet, goodbye, buy and sell are untouched.
- **The ask**, read from the IL of `Trader.UseItem` (1.0.16): `m_useItems` is a
  `List<TraderUseItem>` of `{ m_prefab, m_setsGlobalKey, m_dialog, m_removesItem }`, matched
  on the item's shared name. On a match he says `m_dialog`, sets `m_setsGlobalKey`, and removes
  one item if `m_removesItem`. If that key is already set he says
  `m_randomUseItemAlreadyRecieved` instead.
  - Append one entry: the troll trophy's `ItemDrop`, `m_removesItem = true`, `m_dialog` = his
    reveal line, `m_setsGlobalKey` = a **run-scoped** key (proposed `saga_haldor_<runseed>`).
  - `bf-haldor-ask` completes when that key is set (polled, like the boss keys).
  - **Global keys persist with the world save** — the same landmine `WorldModifiers` guards.
    The key is removed at run end and on abandon, and is scoped to the seed so a replay on the
    same world asks again.
  - The appended entry is removed when the voice detaches.
- **The pin:** Act II's altar pin is placed when `bf-haldor-ask` is done, not when the act
  opens (`_pinnedActIndex` logic gains a per-act gate). Other acts unchanged.

### `RunService`

New and cut steps; `bf-haldor` gets `Track = HuntTrackId`; `PollBarrowKeeper` beside
`PollThane`; `HaldorVoice` ticked in Act II; the pin gate; the run-end cleanup of Haldor's key
and his lists.

## Verification before building

Two facts the assembly cannot answer — both checked from the running game, logged at run start
the way the raid registry and carry weights are:

1. **The troll trophy's drop chance.** Read `Troll`'s `CharacterDrop.m_drops` for
   `TrophyForestTroll`. If it is low enough that one troll is not a fair ask, the hint says so
   plainly ("not every troll gives one"), or the ask changes — an owner call.
2. **Haldor's `m_name`** — the token to match (`$npc_haldor` is expected, unverified).

## Testing

- **Engine tests (pure):** Act II's track order (hunt ends haldor → ask → find → elder; craft
  has keeper → … → keeper-light → storm); the pin gate as a `StepPredicates` function; heat
  total unchanged; the three cut ids gone.
- **Run-start validators:** the troll trophy and the light resolve to real items; the new step
  ids pass `ValidateActs`.
- **Play only:** the keeper's placement at a hillside door; the palette; Haldor's lines and the
  give-item flow; the key cleared after a run ends; the pin appearing on his line.

## Documentation

- Story bible: glossary entries for **the barrow-keeper** and **Haldor's voice**; Act II's
  "as built" paragraph rewritten; and the thane's entry corrected — it still says the third
  rung comes after Bonemass (changed to the Elder the same day).
- Saga Atlas regenerated and republished to its existing URL.
- RESUME.md and a HANDOFF_WINDOWS task with the play list.

## Not in this work

- Moving Act I's speakers onto `SagaSpeaker`.
- Saga goods in Haldor's stock.
- Speakers for Acts III–V.
- Boats — the owner discussed boat enhancements in another session; nothing is recorded yet,
  and it gets its own design when it arrives.
