# Act II Voices Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Act II two speakers (the barrow-keeper and Haldor) with errands that carry the act's question, and trim its chores.

**Architecture:** Pure gates and names go in `RunMode/` (`SagaNames`, `StepPredicates`) where the harness tests them. A new `SagaSpeaker` base holds the plumbing Act I's speakers each copy; `BarrowKeeper` is its first subclass. `HaldorVoice` decorates the game's own `Trader`. `RunService` wires them and edits Act II's chain.

**Tech Stack:** C# / .NET 4.7.2 against Valheim 1.0.16 (Unity 6000.0.75); Mono msbuild on the Mac; `Tests/run_tests.sh` (Roslyn csc).

**Spec:** `docs/superpowers/specs/2026-10-05-act-two-voices-design.md`

## Global Constraints

- Branch `feature/run-mode`. Build: `msbuild Valheim.sln -p:Configuration=Debug -v:minimal` must be clean; `Tests/run_tests.sh` must print `ALL PASS`.
- Every line a character says obeys the bible's rule: **no line the world does not back.**
- Speakers wear the `Ghost` prefab, non-persistent ZDO, tamed, dressed via `CreatureDressing.ApplyWhenSettled` (never at spawn).
- Prices name items by SHARED name: `$item_*` tokens for vanilla, display text for saga clones (`SagaItems.RescuedLightName`).
- Global keys persist with the world: Haldor's key is run-scoped (`saga_haldor_<seed>`) and removed at run start and end.
- Act I's speakers (`HuntersShade`, `Thjalfi`, `Thane`) are NOT modified.
- Act II stays at 23 steps (3 cut, 3 added).

## Review Focus

1. A player who reaches "Find the Elder's altar" by any route gets the pin; before Haldor's ask is done, no Act II altar pin appears even while `bf-tomb`/`bf-haldor` (also DiscoverLocation) are current — pinned by the `AltarDiscovery` predicate test.
2. A resume mid-act re-teaches the helm only if `bf-keeper-light` is done — the recipe gate reads `StepDone`, already tested generically; the id change is checked by the run-start recipe validator.
3. Abandoning a run leaves no `saga_haldor_*` key in the world and Haldor's vanilla talk restored — `HaldorKey`/`IsHaldorKey` tests pin the key shape the cleanup matches on.
4. The keeper's events are carried by real steps, or he vanishes after the first line (the Thjalfi bug) — `ValidateSpawnEvents` gains both events.
5. The light price resolves to a real item — `ValidateQuestPrices` gains the keeper's price.

---

### Task 1: Pure names and gates

**Files:**
- Modify: `ICanShowYouTheWorld/RunMode/SagaNames.cs`
- Modify: `ICanShowYouTheWorld/RunMode/StepPredicates.cs`
- Test: `Tests/StepPredicateTests.cs`

**Interfaces — Produces:**
- `SagaNames.KeeperFound`, `SagaNames.KeeperPaid`, `SagaNames.HaldorTold` (string consts)
- `SagaNames.HaldorKeyPrefix = "saga_haldor_"`, `string SagaNames.HaldorKey(int seed)`, `bool SagaNames.IsHaldorKey(string key)`
- `StepPredicates.Keeper/KeeperFind/KeeperPayment/HaldorAsk(IReadOnlyList<QuestTrack>)`, `StepPredicates.AltarDiscovery(IReadOnlyList<QuestTrack>, string altarLocation)`

- [ ] **Step 1: Write the failing tests** (append inside `StepPredicateTests.Run()`)

```csharp
        // --- 2026-10-05: Act II's voices ---------------------------------------------------
        var keeperFind = new List<QuestTrack> { Track("craft", Step("bf-keeper", ChallengeKind.PlayerEvent, SagaNames.KeeperFound)) };
        var keeperPay = new List<QuestTrack> { Track("craft", Step("bf-keeper-light", ChallengeKind.PlayerEvent, SagaNames.KeeperPaid)) };
        Check.That(StepPredicates.KeeperFind(keeperFind) && StepPredicates.Keeper(keeperFind), "the keeper is wanted while his first step is live");
        Check.That(StepPredicates.KeeperPayment(keeperPay) && StepPredicates.Keeper(keeperPay), "and while his payment is");
        Check.That(!StepPredicates.KeeperPayment(keeperFind) && !StepPredicates.KeeperFind(keeperPay), "the two phases are not confused");

        var ask = new List<QuestTrack> { Track("hunt", Step("bf-haldor-ask", ChallengeKind.PlayerEvent, SagaNames.HaldorTold)) };
        Check.That(StepPredicates.HaldorAsk(ask), "Haldor's ask is live on its step");
        Check.That(!StepPredicates.HaldorAsk(keeperFind), "and not on the keeper's");

        // The pin waits for the ALTAR's discovery, not any discovery: the tomb and the trader are
        // DiscoverLocation steps too, and keying on the kind alone pinned the Elder while you were
        // still looking for the burial chambers.
        var tomb = new List<QuestTrack> { Track("craft", Step("bf-tomb", ChallengeKind.DiscoverLocation, "Crypt2")) };
        var trader = new List<QuestTrack> { Track("hunt", Step("bf-haldor", ChallengeKind.DiscoverLocation, "Vendor_BlackForest")) };
        var altar = new List<QuestTrack> { Track("hunt", Step("bf-find", ChallengeKind.DiscoverLocation, "GDKing")) };
        Check.That(!StepPredicates.AltarDiscovery(tomb, "GDKing"), "no altar pin while the tomb is the discovery");
        Check.That(!StepPredicates.AltarDiscovery(trader, "GDKing"), "nor while the trader is");
        Check.That(StepPredicates.AltarDiscovery(altar, "GDKing"), "the pin comes with the altar's own step");
        Check.That(!StepPredicates.AltarDiscovery(altar, null), "a null altar name pins nothing");
        Check.That(!StepPredicates.AltarDiscovery(new List<QuestTrack> { Track("hunt", Step("bf-find", ChallengeKind.DiscoverLocation, "GDKing"), blocked: true) }, "GDKing"),
                   "a blocked track's step is not live");

        // Haldor's key: run-scoped, lower case, and recognisable for cleanup.
        Check.That(SagaNames.HaldorKey(12345) == "saga_haldor_12345", "Haldor's key carries the run seed");
        Check.That(SagaNames.HaldorKey(-7) == SagaNames.HaldorKey(-7) && SagaNames.HaldorKey(-7) != SagaNames.HaldorKey(7),
                   "a negative seed is its own key");
        Check.That(SagaNames.HaldorKey(-7) == SagaNames.HaldorKey(-7).ToLowerInvariant(), "and lower case, as the game stores keys");
        Check.That(SagaNames.IsHaldorKey(SagaNames.HaldorKey(99)) && SagaNames.IsHaldorKey("SAGA_HALDOR_1"), "cleanup recognises any of them");
        Check.That(!SagaNames.IsHaldorKey("defeated_gdking") && !SagaNames.IsHaldorKey(null), "and nothing else");
```

- [ ] **Step 2: Run, expect a compile failure** — `Tests/run_tests.sh` → `'StepPredicates' does not contain a definition for 'KeeperFind'`.

- [ ] **Step 3: Implement** — in `SagaNames` after `ThaneFound`:

```csharp
        /// <summary>The barrow-keeper's two events: first spoken to, and paid a light.</summary>
        public const string KeeperFound = "KeeperFound";
        public const string KeeperPaid = "KeeperPaid";

        /// <summary>Haldor took the troll's trophy and told where the couriers go.</summary>
        public const string HaldorTold = "HaldorTold";

        /// <summary>
        /// Haldor's acceptance sets a GLOBAL key (Trader.UseItem), and global keys are saved with the
        /// world. So the key names the run, and every key with this prefix is cleared at run start and
        /// end - a replay on the same world must be able to ask him again.
        /// </summary>
        public const string HaldorKeyPrefix = "saga_haldor_";

        public static string HaldorKey(int seed) =>
            HaldorKeyPrefix + (seed < 0 ? "n" + (-(long)seed) : seed.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public static bool IsHaldorKey(string key) =>
            !string.IsNullOrEmpty(key) && key.StartsWith(HaldorKeyPrefix, System.StringComparison.OrdinalIgnoreCase);
```

In `StepPredicates` after `Thane`:

```csharp
        /// <summary>The barrow-keeper should be standing for one of his own steps.</summary>
        public static bool Keeper(IReadOnlyList<QuestTrack> tracks) => KeeperFind(tracks) || KeeperPayment(tracks);

        public static bool KeeperFind(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.KeeperFound);

        public static bool KeeperPayment(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.KeeperPaid);

        public static bool HaldorAsk(IReadOnlyList<QuestTrack> tracks) =>
            Live(tracks).Any(d => d.Kind == ChallengeKind.PlayerEvent && d.Param == SagaNames.HaldorTold);

        /// <summary>
        /// The act's boss altar is the live discovery. The map pin waits for exactly this - not for
        /// any DiscoverLocation step, since burial chambers and traders are found the same way.
        /// </summary>
        public static bool AltarDiscovery(IReadOnlyList<QuestTrack> tracks, string altarLocation) =>
            !string.IsNullOrEmpty(altarLocation) &&
            Live(tracks).Any(d => d.Kind == ChallengeKind.DiscoverLocation && d.Param == altarLocation);
```

- [ ] **Step 4: Run** `Tests/run_tests.sh` → `ALL PASS`.
- [ ] **Step 5: Commit** `feat(run): Act II's voices — the pure names and gates, tested`.

### Task 2: The altar pin keys on the altar

**Files:** Modify `ICanShowYouTheWorld/RunMode/Unity/RunService.cs` (`RefreshActPin`, `DiscoveryStepIsCurrent`).

- [ ] **Step 1:** In `RefreshActPin`, move the boss lookup above the gate and replace `if (!DiscoveryStepIsCurrent()) return;` with `if (_challenges == null || !StepPredicates.AltarDiscovery(_challenges.Tracks, boss.locName)) return;`. Delete `DiscoveryStepIsCurrent` if it has no other caller (it has none today).
- [ ] **Step 2:** Build clean. Every act's find step's `Param` equals its `Bosses` `locName` (checked 2026-10-05: Eikthyrnir, GDKing, Bonemass, Dragonqueen, GoblinKing, Mistlands_DvergrBossEntrance1, FaderLocation).
- [ ] **Step 3: Commit** `fix(run): the altar pin waits for the altar's own step, not any discovery`.

### Task 3: `SagaSpeaker`, the shared base

**Files:** Create `ICanShowYouTheWorld/RunMode/Unity/SagaSpeaker.cs`.

**Interfaces — Produces:**
- `internal sealed class SagaSpeakerTalk : MonoBehaviour, Interactable, Hoverable` with `SagaSpeaker Owner`.
- `internal abstract class SagaSpeaker` — abstract `string Name`, `CreatureDressing.Look Look`, `Vector3? ChooseSpot(Player)`, `string Greeting`, `string HoverText(Player)`, `bool OnInteract(Humanoid, bool alt)`; protected `void Stand(Player, bool wanted)`, `bool TryPay(Inventory, (string token,int amount,string label)[])`, `string PriceProgress(Inventory, ...)`; public `Say(string)`, `Spot()`, `Position()`, `Standing`, `Dismiss()`, `Reset()`, `Bearing(Player, string farLabel)`.

- [ ] **Step 1:** Write the file (full code as committed; the body is the thane's spawn/greet/say/dismiss lifted verbatim with the name, look and spot made abstract).
- [ ] **Step 2:** Build clean. **Step 3: Commit** `feat(run): SagaSpeaker — one copy of the plumbing every speaker carried`.

### Task 4: `BarrowKeeper`

**Files:** Create `ICanShowYouTheWorld/RunMode/Unity/BarrowKeeper.cs`; modify `CreatureDressing.cs` (add `Keeper()` look: grave-ash grey, green core-fire glow).

- Phase `Speak | Pay | Idle | After`; `Price = { (SagaItems.RescuedLightName, 1, "light") }`.
- Spot: `ZoneSystem.instance.FindClosestLocation("Crypt2", player pos, out loc)`, then a 6–14 m ring around `loc.m_position` on generated terrain above the waterline with the thane's flatness test; fallback the door + 8 m. Cached per run.
- Lines: as in the spec, final text in the file.
- [ ] Build clean; commit `feat(run): the barrow-keeper, at the burial chamber's door`.

### Task 5: `HaldorVoice`

**Files:** Create `ICanShowYouTheWorld/RunMode/Unity/HaldorVoice.cs`.

- `Tick(Player player, bool wanted, string key)`: when wanted, find the nearest `Trader` whose `m_name` contains "haldor" (case-insensitive) within 80 m; on first attach save `m_randomTalk`, swap in `Talk`, append a `Trader.TraderUseItem { m_prefab = TrophyForestTroll's ItemDrop, m_removesItem = true, m_dialog = RevealLine, m_setsGlobalKey = key }`. When not wanted, `Detach()`.
- `Detach()`: if the trader still exists, restore the talk list and remove the appended entry.
- `static void ClearKeys(ZoneSystem zone)`: `RemoveGlobalKey` every key where `SagaNames.IsHaldorKey`.
- [ ] Build clean; commit `feat(run): Haldor's voice — the couriers' road, for a troll's head`.

### Task 6: Act II's chain and the wiring

**Files:** Modify `RunService.cs`, `SagaRecipes.cs`.

- Chain: `bf-haldor` gets `Track = HuntTrackId` and moves after `bf-intercept`, with an Opening; new `bf-haldor-ask` after it; new `bf-keeper` after `bf-crypt`; new `bf-keeper-light` after `bf-bronze`; delete `bf-sign`, `bf-bees`, `bf-herd`; Openings on `bf-smelter`, `bf-bronze`, `bf-cart`, `bf-portal`; `bf-plant` reward loses the queen and its text loses the hive.
- Rewards table: drop `bf-sign`, `bf-bees`, `bf-herd`; `bf-plant` → `CarrotSeeds 20` only.
- `SagaRecipes` storm-helm: `RequiresStepDone = "bf-keeper-light"`, new `TaughtLine`.
- Wiring: `_keeper` field + construction beside `_thane`; `PollBarrowKeeper` and `PollHaldor` after `PollThane`; reset at run start; `EndRun` resets the keeper, detaches Haldor, clears keys; `SpawnGatingEvents` + `KeeperFound`, `KeeperPaid`, `HaldorTold`; `QuestPrices` + the keeper; run-start log of the troll's trophy drop chance and Haldor's `m_name` when first seen.
- [ ] Build clean, tests pass; commit `feat(run): Act II's chain — the keeper, Haldor's ask, three chores cut`.

### Task 7: Docs, build, ship

- Story bible: glossary **The barrow-keeper**, **Haldor (in the saga)**; Act II "as built" paragraph; thane entry's rung list (Eikthyr, then the Elder).
- `python3 Scripts/saga_atlas.py`, republish to the atlas URL in RESUME.md.
- RESUME.md bullet; HANDOFF_WINDOWS task with the play list; progress noted.
- Tag `$(bash Scripts/nextversion.sh)`, `bash Scripts/setversion.sh`, build, deploy to the Mac, commit `build:`, push branch and tag.
