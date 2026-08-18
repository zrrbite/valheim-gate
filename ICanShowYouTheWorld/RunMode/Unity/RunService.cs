using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;
using ICanShowYouTheWorld.Services;
using UnityEngine;
using Random = System.Random;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Central orchestrator for Run Mode. Owns the heat model, challenge engine, boon engine
    /// and world modifiers, and is the only Run Mode component that talks to the live game:
    /// it polls boss global keys, player altitude, inventory and armor, drives autosave, and
    /// restores an interrupted run after a reload.
    ///
    /// Every code path that can be reached from the Unity game loop swallows its exceptions —
    /// a broken run must never take the game down with it.
    /// </summary>
    public class RunService : IRunService
    {
        // --- Boss table: (location prefab name, display name, global defeat key) ---
        private static readonly (string locName, string display, string defeatKey)[] Bosses =
        {
            ("Eikthyrnir",  "Eikthyr",   "defeated_eikthyr"),
            ("GDKing",      "The Elder", "defeated_gdking"),
            ("Bonemass",    "Bonemass",  "defeated_bonemass"),
            ("Dragonqueen", "Moder",     "defeated_dragon"),
            ("GoblinKing",  "Yagluth",   "defeated_goblinking"),
        };

        /// <summary>Used when <see cref="IConfiguration.RunFinalBossKey"/> names something that isn't a boss.</summary>
        private const string DefaultFinalBossKey = "defeated_goblinking";

        private const float BossPollIntervalSeconds = 1f;
        private const float AutosaveIntervalSeconds = 5f;
        private const int ConsecutiveFailuresBeforeNotice = 5;

        private const string FrozenNotice = "Run paused — world not loaded.";
        private const string WrongWorldNotice = "Run paused — this is not the world the run started in.";
        private const string AbandonWrongWorldNotice = "Load the run's world to abandon it.";
        private const string CorruptSaveNotice =
            "Run save could not be read — file kept as .corrupt; run cannot be resumed.";

        private readonly IGameAPI _game;
        private readonly IConfiguration _cfg;
        private readonly WorldModifiers _worldModifiers = new WorldModifiers();
        private readonly HashSet<string> _loggedFailures = new HashSet<string>();

        // --- Boon effect seams, wired to BoonEffects in the constructor below. ---
        internal Action<string> ApplyBoonEffect = _ => { };
        internal Action<string> UnapplyBoonEffect = _ => { };
        internal Action UnapplyAllBoonEffects = () => { };

        private readonly BoonEffects _boonEffects;

        // --- Run state ---
        private HeatModel _heat = new HeatModel();
        private ChallengeEngine _challenges;
        private BoonEngine _boons;
        private Random _rng;
        private int _rngSeed;
        private string _worldId;

        /// <summary>
        /// The boss whose defeat key ends the run, resolved from config once per run
        /// (see <see cref="ResolveFinalBossKey"/>) so a typo can't silently make the run unfinishable.
        /// </summary>
        private string _finalBossKey = DefaultFinalBossKey;

        private bool _active;
        private float _elapsed;
        private float _pollTimer;
        private float _saveTimer;
        private float _noArmorSeconds;
        private bool _frozen;

        /// <summary>
        /// Player.m_localPlayer as of the last tick. Death recreates the Player component with
        /// fresh fields, silently wiping fleet's speed boost and sharp's item snapshots even
        /// though the boon is still held — a reference change here (while active) is a respawn,
        /// and triggers a re-apply of every held passive against the new player.
        /// </summary>
        private Player _trackedPlayer;

        private readonly List<string> _splitLabels = new List<string>();
        private readonly List<float> _splitTimes = new List<float>();

        /// <summary>Ids of the NoArmorMinutes challenges currently active, so a newly drawn one starts from zero.</summary>
        private readonly HashSet<string> _noArmorChallengeIds = new HashSet<string>();

        /// <summary>Boss keys that must not produce a split: already true when the run began, or already recorded.</summary>
        private readonly HashSet<string> _accountedBossKeys = new HashSet<string>();

        private bool _resumeAttempted;
        private RunSaveState _pendingResume;
        private bool _restorePending;

        /// <summary>
        /// World the deferred restore belongs to. World modifiers are global keys saved WITH the
        /// world, so a restore owed to world A must never be flushed into world B — the retry in
        /// <see cref="TickInner"/> waits for this world to come back. Survives <see cref="EndRun"/>
        /// (which clears <see cref="_worldId"/>) precisely because the debt outlives the run.
        /// Null means "unknown world" (legacy state), which is allowed to restore anywhere.
        /// </summary>
        private string _restoreWorldId;

        private int _consecutiveTickFailures;

        /// <summary>Score of the most recently finished run; surfaced by the HUD until dismissed.</summary>
        internal float LastScore;

        /// <summary>Transient notice for the HUD. Null when there is nothing to say.</summary>
        internal string HudNotice;

        public RunService(IGameAPI game, IConfiguration cfg)
        {
            _game = game;
            _cfg = cfg;

            // _boons doesn't exist yet at construction time — captured by reference, resolved
            // lazily whenever BoonEffects actually needs the held set.
            _boonEffects = new BoonEffects(() => _boons?.Held, UndefeatedBossLocations);
            ApplyBoonEffect = _boonEffects.Apply;
            UnapplyBoonEffect = _boonEffects.Unapply;
            UnapplyAllBoonEffects = _boonEffects.UnapplyAll;

            // Subscribed for the lifetime of the service; the handler no-ops while inactive.
            GameEvents.OnCharacterDied += OnCharacterDied;
        }

        // --- IRunService ---

        public bool IsRunActive => _active;

        /// <summary>Exposed for the HUD, which must hide reroll controls while frozen (see RunWindow).</summary>
        internal bool IsFrozen => _frozen;
        public float ElapsedSeconds => _active ? _elapsed : 0f;
        public float Heat => _active ? _heat.Heat : 0f;
        public ChallengeEngine Challenges => _active ? _challenges : null;
        public BoonEngine Boons => _active ? _boons : null;

        public float CurrentScore =>
            _active
                ? RunScore.Compute(_cfg.RunParTimeMinutes * 60f, _elapsed, _heat.Heat, _cfg.RunHeatScoreWeight)
                : LastScore;

        public IReadOnlyList<string> Splits
        {
            get
            {
                var list = new List<string>(_splitLabels.Count);
                for (int i = 0; i < _splitLabels.Count; i++)
                {
                    float t = i < _splitTimes.Count ? _splitTimes[i] : 0f;
                    list.Add($"{_splitLabels[i]}  {FormatTime(t)}");
                }
                return list;
            }
        }

        /// <summary>
        /// Whether kill challenges can make progress. Answered by reading Character.OnDeath's IL
        /// (once, cached) rather than by waiting to see whether anything dies: the old grace clock
        /// declared the hook dead after 60 quiet seconds, so a peaceful opening minute produced a
        /// false warning on a perfectly working install. HookInstalled is still checked first —
        /// if the hook has already fired, no probe is needed.
        /// </summary>
        public bool KillHookAvailable => GameEvents.HookInstalled || GameEvents.ProbeHookInstalled();

        public string LobbySummary()
        {
            try
            {
                return PermanentRecord.GetSummary(Player.m_localPlayer);
            }
            catch (Exception ex)
            {
                LogOnce("lobby-summary", ex);
                return "Bosses: —  Best: —  Runs: —";
            }
        }

        public void StartRun()
        {
            try
            {
                // --- Validate first: a refused start must change nothing. ---
                if (_active)
                {
                    Message("A run is already in progress.");
                    return;
                }

                // A loaded-but-unresumed run belongs to another world, and its save file holds the
                // ONLY copy of that world's original modifier values. Run state is per-character,
                // so starting here would overwrite it and strand those rates permanently.
                if (_pendingResume != null)
                {
                    string pendingWorld = ReadableWorldName(_pendingResume.worldId);
                    string busy = $"An unfinished run exists on world '{pendingWorld}' — " +
                                  "load that world to resume or abandon it first.";
                    Announce(busy);
                    HudNotice = busy;
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    // ShowMessage routes through the local player, so it would go nowhere here.
                    Announce("Run Mode needs a spawned character.");
                    return;
                }

                var zone = ZoneSystem.instance;
                if (zone == null)
                {
                    Announce("Run Mode could not reach the world (ZoneSystem missing).");
                    return;
                }

                string finalBossKey = ResolveFinalBossKey();

                // A used world may already have kills. Those bosses are excluded from splits;
                // if the FINAL boss is already down there is no run to be had.
                var preDefeated = new HashSet<string>();
                foreach (var boss in Bosses)
                {
                    if (SafeGetGlobalKey(zone, boss.defeatKey)) preDefeated.Add(boss.defeatKey);
                }

                if (preDefeated.Contains(finalBossKey))
                {
                    Message("The final boss is already dead on this world — start Run Mode on a fresh one.");
                    return;
                }

                // --- Committed: from here on we mutate state. ---
                // Legacy cheats first: Immunity/Gift's OFF toggles gate on CheatCommands.GodMode
                // and are bracketed to run with it temporarily on if needed (see
                // ForceLegacyCheatsOff) — ForceGodModeOff must come after so the final state is
                // god-mode-off either way.
                ForceLegacyCheatsOff();
                ForceGodModeOff();

                _loggedFailures.Clear();
                _consecutiveTickFailures = 0;
                _frozen = false;
                HudNotice = null;

                _worldId = WorldIdentifier();
                _finalBossKey = finalBossKey;

                _rngSeed = Environment.TickCount;
                _rng = new Random(_rngSeed);

                BuildEngines(BuildChallengePool());

                // Gate the pool to this world's progression before the first Tick deals anything.
                // preDefeated is the same snapshot _accountedBossKeys is seeded from, so the
                // ceiling and the splits can never disagree about what was already dead.
                _challenges.MaxTier = MaxTierForDefeatedCount(preDefeated.Count);

                _heat = new HeatModel();
                _elapsed = 0f;
                _pollTimer = 0f;
                _saveTimer = 0f;
                _noArmorSeconds = 0f;
                _noArmorChallengeIds.Clear();
                _splitLabels.Clear();
                _splitTimes.Clear();
                _accountedBossKeys.Clear();
                foreach (var key in preDefeated) _accountedBossKeys.Add(key);
                LastScore = 0f;

                RevealBosses(player);

                _worldModifiers.ApplyBaseline(_cfg);
                _worldModifiers.ApplyHeat(0f, _cfg);
                _restorePending = true;
                _restoreWorldId = _worldId;

                _trackedPlayer = player;

                _active = true;
                _resumeAttempted = true;
                _pendingResume = null;

                SaveState();

                Debug.Log($"[ICanShowYouTheWorld] Run Mode started (seed={_rngSeed}, world={_worldId}, " +
                          $"pre-defeated={_accountedBossKeys.Count}).");
                Message("Run Mode started. Good luck.");
            }
            catch (Exception ex)
            {
                LogOnce("start-run", ex);
                Announce("Run Mode failed to start — see the log.");
            }
        }

        public void AbandonRun()
        {
            try
            {
                if (!_active) return;

                // Abandoning is a write to the WORLD: RestoreAll pushes the run's captured
                // originals back into global keys, which Valheim saves with whatever world is
                // loaded. Doing that from a different world would stamp world A's rates onto
                // world B and then delete the only record of them. Refuse and stay frozen —
                // the run (and its state file) survive until its own world is loaded.
                string world = WorldIdentifier();
                if (_worldId != null && (world == null || world != _worldId))
                {
                    HudNotice = AbandonWrongWorldNotice;
                    Announce(AbandonWrongWorldNotice);
                    return;
                }

                _restorePending = !_worldModifiers.RestoreAll();
                _restoreWorldId = _restorePending ? _worldId : null;
                SafeUnapplyAllBoonEffects();
                DeleteState();
                EndRun();

                Debug.Log("[ICanShowYouTheWorld] Run Mode run abandoned.");
                Message("Run abandoned.");
            }
            catch (Exception ex)
            {
                LogOnce("abandon-run", ex);
            }
        }

        public void RerollChallenge(int slot)
        {
            try
            {
                if (!_active || _frozen || _challenges == null) return;

                // An above-tier challenge is a dead slot: content the world hasn't unlocked, so
                // it can't be completed, and at 0 heat the cost check below would trap the player
                // with it forever. Clearing it is free, and can't be farmed into cheap rerolls:
                // every draw and reroll is tier-filtered, so a fresh one is never above tier.
                // Reaching this at all takes a challenge dealt before the current gating applied —
                // a save predating the ladder, or a world whose progression was rolled back.
                bool free = _challenges.IsAboveTier(slot);

                if (!free && _heat.Heat < _cfg.RunRerollHeatCost)
                {
                    HudNotice = $"Not enough heat to reroll (need {_cfg.RunRerollHeatCost:0.#}).";
                    return;
                }

                // Charge only on a reroll that actually happened — a rejected slot index
                // or an exhausted pool should not cost heat.
                if (!_challenges.Reroll(slot)) return;

                if (!free)
                {
                    _heat.Remove(_cfg.RunRerollHeatCost);
                    _worldModifiers.ApplyHeat(_heat.Heat, _cfg);
                }

                SaveState();
            }
            catch (Exception ex)
            {
                LogOnce("reroll", ex);
            }
        }

        public void Tick(float dt)
        {
            try
            {
                TickInner(dt);
                _consecutiveTickFailures = 0;
            }
            catch (Exception ex)
            {
                LogOnce("tick", ex);

                _consecutiveTickFailures++;
                if (_consecutiveTickFailures >= ConsecutiveFailuresBeforeNotice)
                {
                    HudNotice = "Run Mode error — check Player.log.";
                }
            }
        }

        // --- Tick internals ---

        private void TickInner(float dt)
        {
            if (!_active)
            {
                // A restore deferred because the world was gone still owes the world its
                // original rates; keep trying until THAT world is back. Flushing it into
                // whichever world happens to load next would write one world's rates into
                // another's save permanently.
                if (_restorePending && RestoreWorldLoaded())
                {
                    if (_worldModifiers.RestoreAll())
                    {
                        _restorePending = false;
                        _restoreWorldId = null;
                    }
                }

                TryResume();
                return;
            }

            // Freeze rather than run against a missing or foreign world: no elapsed time,
            // no polling, no autosave (which would otherwise overwrite good state with
            // half-loaded nonsense).
            if (ZoneSystem.instance == null || Player.m_localPlayer == null)
            {
                SetFrozen(true, FrozenNotice);
                return;
            }

            string world = WorldIdentifier();
            if (_worldId != null && world != null && world != _worldId)
            {
                SetFrozen(true, WrongWorldNotice);
                return;
            }

            SetFrozen(false, null);
            DetectRespawnAndReapplyPassives();

            _elapsed += dt;
            _challenges?.Tick(dt);
            _boons?.Tick(dt);
            _boonEffects.Tick(dt);

            HandleBoonOfferInput();
            HandleBoonActivationInput();

            _pollTimer += dt;
            if (_pollTimer >= BossPollIntervalSeconds)
            {
                float pollDt = _pollTimer;
                _pollTimer = 0f;
                PollBosses();

                // PollBosses may have finished the run.
                if (_active) PollMeasures(pollDt);
            }

            if (!_active) return;

            _saveTimer += dt;
            if (_saveTimer >= AutosaveIntervalSeconds)
            {
                _saveTimer = 0f;
                SaveState();
            }
        }

        private void SetFrozen(bool frozen, string notice)
        {
            if (frozen == _frozen) return;

            _frozen = frozen;
            if (frozen)
            {
                HudNotice = notice;
                Debug.Log($"[ICanShowYouTheWorld] Run Mode frozen: {notice}");
            }
            else if (HudNotice == FrozenNotice || HudNotice == WrongWorldNotice || HudNotice == AbandonWrongWorldNotice)
            {
                HudNotice = null;
            }
        }

        private void HandleBoonOfferInput()
        {
            if (_boons == null || _boons.CurrentOffer.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.Keypad1)) _boons.Pick(0);
            else if (Input.GetKeyDown(KeyCode.Keypad2)) _boons.Pick(1);
            else if (Input.GetKeyDown(KeyCode.Keypad3)) _boons.Pick(2);
        }

        /// <summary>
        /// Keypad4/5/6 activate held wind/ember/way while a run is active. Gated on there being
        /// no boon offer pending, matching the brief; the offer keys (1/2/3) don't overlap with
        /// these anyway, so this is a UX choice, not a conflict-avoidance one.
        /// </summary>
        private void HandleBoonActivationInput()
        {
            if (_boons == null || _boons.CurrentOffer.Count > 0) return;

            if (Input.GetKeyDown(KeyCode.Keypad4)) TryActivateHeldBoon("wind");
            else if (Input.GetKeyDown(KeyCode.Keypad5)) TryActivateHeldBoon("ember");
            else if (Input.GetKeyDown(KeyCode.Keypad6)) TryActivateHeldBoon("way");
        }

        private void TryActivateHeldBoon(string boonId)
        {
            var held = _boons.Held.FirstOrDefault(h => h.Def.Id == boonId);
            if (held == null) return; // Not held — key does nothing.

            if (!_boonEffects.Activate(boonId))
            {
                Message(_boonEffects.LastActivationMessage ?? $"{held.Def.Display} not ready.");
            }
        }

        /// <summary>
        /// Death recreates Player.m_localPlayer with fresh fields — fleet's speed boost and
        /// sharp's item snapshots vanish even though the boon is still held. A reference change
        /// while a run is active (as opposed to null→player at start/resume, which isn't a
        /// respawn) re-applies every held PASSIVE boon against the new player/gear. Actives
        /// (wind/ember/way) carry no player-field state, so they're excluded — see
        /// <see cref="ReapplyPassiveBoonEffects"/>.
        /// </summary>
        private void DetectRespawnAndReapplyPassives()
        {
            var player = Player.m_localPlayer;

            // Deliberately ReferenceEquals, not ==: Player derives from UnityEngine.Object, whose
            // overloaded == treats a destroyed-but-not-yet-null-in-C#-terms object as "== null".
            // By the time a fresh Player exists, _trackedPlayer holds exactly that — a destroyed
            // object — so Unity's == would report "still the same (null) player" and this would
            // never fire. ReferenceEquals compares the actual C# reference, which is what "did the
            // component instance change" really means here.
            if (ReferenceEquals(player, _trackedPlayer)) return;

            bool isRespawn = !ReferenceEquals(_trackedPlayer, null);
            _trackedPlayer = player;
            if (!isRespawn || player == null) return;

            ReapplyPassiveBoonEffects();
        }

        /// <summary>Re-runs Apply for every held passive boon (fleet/sharp/pack) — used after a respawn and NOT after a resume (way's charge is persisted separately; re-running Apply("way") there would grant a free charge).</summary>
        private void ReapplyPassiveBoonEffects()
        {
            if (_boons == null) return;

            foreach (var h in _boons.Held.ToList())
            {
                if (!h.Def.IsPassive) continue;

                try { ApplyBoonEffect?.Invoke(h.Def.Id); }
                catch (Exception ex) { LogOnce("boon-reapply-passive", ex); }
            }
        }

        private void PollBosses()
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return;

            bool finished = false;
            bool progressed = false;

            foreach (var boss in Bosses)
            {
                if (_accountedBossKeys.Contains(boss.defeatKey)) continue;
                if (!SafeGetGlobalKey(zone, boss.defeatKey)) continue;

                _accountedBossKeys.Add(boss.defeatKey);
                progressed = true;
                _splitLabels.Add(boss.display);
                _splitTimes.Add(_elapsed);

                try { PermanentRecord.RecordBossKill(Player.m_localPlayer, boss.defeatKey); }
                catch (Exception ex) { LogOnce("record-boss", ex); }

                Message($"{boss.display} down — {FormatTime(_elapsed)}");

                if (boss.defeatKey == _finalBossKey) finished = true;
            }

            // A boss just fell — the next biome's challenges become drawable from here on.
            // Existing actives are untouched; only future draws and rerolls see the new ceiling.
            if (progressed) RefreshMaxTier();

            if (finished) FinishRun();
        }

        private void PollMeasures(float pollDt)
        {
            var player = Player.m_localPlayer;
            if (player == null || _challenges == null) return;

            _challenges.ReportMeasure(ChallengeKind.ReachAltitude, string.Empty, player.transform.position.y);

            var inventory = player.GetInventory();
            if (inventory != null)
            {
                // Only the item names actually being asked for — CountItems is a full scan.
                var wanted = _challenges.Active
                    .Where(a => a.Def.Kind == ChallengeKind.CollectItem && !string.IsNullOrEmpty(a.Def.Param))
                    .Select(a => a.Def.Param)
                    .Distinct();

                foreach (var itemName in wanted)
                {
                    _challenges.ReportMeasure(ChallengeKind.CollectItem, itemName, inventory.CountItems(itemName));
                }

                if (_challenges.Active.Any(a => a.Def.Kind == ChallengeKind.CollectFood))
                {
                    _challenges.ReportMeasure(ChallengeKind.CollectFood, string.Empty, CountFood(inventory));
                }
            }

            // A newly drawn no-armor challenge must not inherit a timer the player banked
            // before it existed, or it completes the moment it is dealt.
            var noArmorNow = _challenges.Active
                .Where(a => a.Def.Kind == ChallengeKind.NoArmorMinutes)
                .Select(a => a.Def.Id)
                .ToList();

            if (noArmorNow.Any(id => !_noArmorChallengeIds.Contains(id))) _noArmorSeconds = 0f;

            _noArmorChallengeIds.Clear();
            foreach (var id in noArmorNow) _noArmorChallengeIds.Add(id);

            // GetBodyArmor() is the aggregate of every equipped item's m_shared.m_armor,
            // so "no armor" is simply a zero total.
            bool noArmor = player.GetBodyArmor() <= 0f;
            _noArmorSeconds = noArmor ? _noArmorSeconds + pollDt : 0f;
            _challenges.ReportMeasure(ChallengeKind.NoArmorMinutes, string.Empty, _noArmorSeconds / 60f);
        }

        /// <summary>
        /// Total stack count of every food item carried. "Food" is m_shared.m_food > 0 — the
        /// health an item restores when eaten, which is what separates real food from mead and
        /// other consumables that heal nothing. Counts stacks, not slots, so 2x10 Cooked Meat is
        /// 20 and not 2.
        ///
        /// Only called when a CollectFood challenge is actually active: this walks the whole
        /// inventory, and it runs on the poll timer.
        /// </summary>
        private static float CountFood(Inventory inventory)
        {
            var items = inventory.GetAllItems();
            if (items == null) return 0f;

            int total = 0;
            foreach (var item in items)
            {
                if (item?.m_shared == null || item.m_shared.m_food <= 0f) continue;
                total += item.m_stack;
            }
            return total;
        }

        // --- Lifecycle helpers ---

        private void FinishRun()
        {
            LastScore = RunScore.Compute(
                _cfg.RunParTimeMinutes * 60f, _elapsed, _heat.Heat, _cfg.RunHeatScoreWeight);

            try { PermanentRecord.RecordScore(Player.m_localPlayer, LastScore); }
            catch (Exception ex) { LogOnce("record-score", ex); }

            _restorePending = !_worldModifiers.RestoreAll();
            _restoreWorldId = _restorePending ? _worldId : null;
            SafeUnapplyAllBoonEffects();
            DeleteState();

            float finalElapsed = _elapsed;
            EndRun();

            Debug.Log($"[ICanShowYouTheWorld] Run Mode run finished in {FormatTime(finalElapsed)} " +
                      $"— score {LastScore:0.###}.");
            Message($"Run complete! {FormatTime(finalElapsed)} — score {LastScore:0.###}");
        }

        /// <summary>Tears down the active-run state, leaving LastScore and the splits for the HUD.</summary>
        private void EndRun()
        {
            _active = false;

            if (_challenges != null) _challenges.Completed -= OnChallengeCompleted;
            if (_boons != null)
            {
                _boons.Gained -= OnBoonGained;
                _boons.Lost -= OnBoonLost;
            }

            _challenges = null;
            _boons = null;
            _pollTimer = 0f;
            _saveTimer = 0f;
            _noArmorSeconds = 0f;
            _noArmorChallengeIds.Clear();
            _accountedBossKeys.Clear();
            _worldId = null;
            _frozen = false;
            _trackedPlayer = null;
            HudNotice = null;
        }

        /// <summary>
        /// The challenge tier ceiling for a world with <paramref name="defeated"/> of the five run
        /// bosses already down: one tier of headroom above what's cleared. A fresh world therefore
        /// offers Meadows and Black Forest content, and each boss kill opens the next biome.
        /// </summary>
        private static int MaxTierForDefeatedCount(int defeated) => defeated + 1;

        /// <summary>
        /// Re-reads world progression and re-gates the challenge pool. Counts ALL five defeat keys
        /// that are true, pre-existing kills included: the ladder tracks what the WORLD has opened
        /// up, not what this run has personally killed.
        ///
        /// A missing ZoneSystem leaves the ceiling where it is rather than tightening it — a
        /// transient null must not suddenly make already-dealt challenges above-tier.
        /// </summary>
        private void RefreshMaxTier()
        {
            if (_challenges == null) return;

            var zone = ZoneSystem.instance;
            if (zone == null) return;

            _challenges.MaxTier = MaxTierForDefeatedCount(
                Bosses.Count(b => SafeGetGlobalKey(zone, b.defeatKey)));
        }

        private void BuildEngines(List<ChallengeDefinition> pool)
        {
            _challenges = new ChallengeEngine(pool, _rng, _cfg.RunChallengeRefillSeconds);
            _challenges.Completed += OnChallengeCompleted;

            _boons = new BoonEngine(DefaultBoons(), _rng, _cfg.RunBoonOfferTimeoutSeconds);
            _boons.Gained += OnBoonGained;
            _boons.Lost += OnBoonLost;
        }

        /// <summary>
        /// The full v1 pool, minus kill challenges when the death hook isn't installed. The
        /// probe behind KillHookAvailable reads the game's IL, so the answer is known for certain
        /// here at StartRun/resume — this is the ONE place the player is told, and there is no
        /// mid-run re-check to contradict it.
        /// </summary>
        private List<ChallengeDefinition> BuildChallengePool()
        {
            var pool = DefaultPool();
            if (KillHookAvailable) return pool;

            HudNotice = "Kill hook unavailable — kill challenges disabled this run.";
            Debug.LogWarning("[ICanShowYouTheWorld] " + HudNotice);
            return pool.Where(d => d.Kind != ChallengeKind.KillPrefab).ToList();
        }

        private void RevealBosses(Player player)
        {
            var game = Game.instance;
            if (game == null) return;

            foreach (var boss in Bosses)
            {
                try
                {
                    // showMap:false — five discoveries in a row should not fling the map open five times.
                    game.DiscoverClosestLocation(
                        boss.locName, player.transform.position, boss.display,
                        (int)Minimap.PinType.Boss, false);
                }
                catch (Exception ex)
                {
                    LogOnce("discover-" + boss.locName, ex);
                }
            }
        }

        /// <summary>
        /// Ends with god mode OFF on both switches. The player's actual god mode is the legacy
        /// static CheatCommands.GodMode (Keypad0 → Player.SetGodMode); ICombatService keeps its
        /// own flag, and nothing keeps the two in sync, so both have to be cleared.
        ///
        /// Uses the side-effect-free setters, never the toggles: ToggleGodMode also resets the
        /// forsaken power cooldown and refills every food timer, which would hand the player a
        /// buff at the exact moment cheats are supposed to stop.
        /// </summary>
        private void ForceGodModeOff()
        {
            try
            {
                CheatCommands.SetGodMode(false);
            }
            catch (Exception ex)
            {
                LogOnce("godmode-off-legacy", ex);
            }

            try
            {
                // Resolved lazily: RunService is constructed alongside the other services,
                // so the container may not have handed out ICombatService yet at ctor time.
                var combat = ModBootstrap.GetService<ICombatService>();
                combat?.SetGodMode(false);
            }
            catch (Exception ex)
            {
                LogOnce("godmode-off-service", ex);
            }
        }

        /// <summary>
        /// Clears any legacy cheat toggles the player left on before the run started —
        /// Renewal, AoE Renewal, Cloak of Flames, Guardian's Gift, Immunity.
        ///
        /// Renewal/AoE Renewal/Cloak are periodic-only (PeriodicManager just checks the flag
        /// each tick) with no persistent state, so the flag-only setters are enough — using the
        /// ToggleX methods would only add an unwanted Show() toast and, for AoE Renewal/Cloak, a
        /// CheatVisualizer ring.
        ///
        /// Immunity and Guardian's Gift are different: they apply real, persistent state to the
        /// player (Immunity overwrites every HitData.DamageModifier to Immune; Gift buffs HP,
        /// stamina/eitr regen, carry weight, and equipped-item durability/armor). A flag-only
        /// clear would leave that state applied for the whole run, so these two are routed
        /// through their real ToggleX methods to run the OFF/revert branch.
        ///
        /// Both toggles gate on CheatCommands.GodMode (RequireGodMode) and silently no-op if
        /// it's off — which it may already be, independent of whether god mode is still on right
        /// now (the player could have toggled Immunity/Gift on, then god mode off, before the
        /// run started). WithLegacyGodModeBracket brackets god mode on just long enough for the
        /// synchronous toggle call. This method runs BEFORE ForceGodModeOff in StartRun, so the
        /// final state is god-mode-off regardless of which branch this bracket takes.
        ///
        /// BoonEffects rides the same statics (notably AOERenewalActive/CloakActive) to drive
        /// wind/ember boons mid-run, so this only runs once at StartRun — never mid-run — and
        /// boons flip their own flags back on afterward as they're granted.
        /// </summary>
        private void ForceLegacyCheatsOff()
        {
            try
            {
                CheatCommands.SetRenewalActive(false);
                CheatCommands.SetAoeRenewalActive(false);
                CheatCommands.SetCloakActive(false);
            }
            catch (Exception ex)
            {
                LogOnce("legacy-cheats-off-flags", ex);
            }

            try
            {
                if (CheatCommands.immunityActive) WithLegacyGodModeBracket(CheatCommands.ToggleImmunity);
            }
            catch (Exception ex)
            {
                LogOnce("legacy-cheats-off-immunity", ex);
            }

            try
            {
                if (CheatCommands.GiftActive) WithLegacyGodModeBracket(CheatCommands.ToggleGuardianGift);
            }
            catch (Exception ex)
            {
                LogOnce("legacy-cheats-off-gift", ex);
            }
        }

        /// <summary>
        /// Brackets a synchronous legacy-toggle call with CheatCommands.GodMode temporarily on,
        /// if it wasn't already — mirrors BoonEffects.WithLegacyGodModeBracket (private to that
        /// class, so mirrored here rather than shared). weTurnedOn is latched before calling
        /// SetGodMode(true) so the finally still restores it even if the bracketed action itself
        /// throws partway through.
        /// </summary>
        private static void WithLegacyGodModeBracket(Action action)
        {
            if (action == null) return;

            bool weTurnedOn = false;
            try
            {
                if (!CheatCommands.GodMode)
                {
                    weTurnedOn = true;
                    CheatCommands.SetGodMode(true);
                }
                action();
            }
            finally
            {
                if (weTurnedOn) CheatCommands.SetGodMode(false);
            }
        }

        // --- Engine event handlers ---

        private void OnChallengeCompleted(ChallengeDefinition def)
        {
            try
            {
                _heat.Add(def.HeatReward);
                _worldModifiers.ApplyHeat(_heat.Heat, _cfg);
                _boons?.CreateOffer();

                Message($"Challenge complete: {def.Display}  (+{def.HeatReward:0.#} heat)");
            }
            catch (Exception ex)
            {
                LogOnce("challenge-complete", ex);
            }
        }

        private void OnBoonGained(BoonDefinition def)
        {
            try { ApplyBoonEffect?.Invoke(def.Id); }
            catch (Exception ex) { LogOnce("boon-apply", ex); }
        }

        private void OnBoonLost(BoonDefinition def)
        {
            try { UnapplyBoonEffect?.Invoke(def.Id); }
            catch (Exception ex) { LogOnce("boon-unapply", ex); }
        }

        private void OnCharacterDied(Character c)
        {
            try
            {
                if (!_active || _frozen || c == null) return;

                if (c == Player.m_localPlayer)
                {
                    _heat.Remove(_cfg.RunDeathHeatPenalty);
                    _worldModifiers.ApplyHeat(_heat.Heat, _cfg);

                    // RemoveLatest raises Lost, which unapplies the effect.
                    var lost = _boons?.RemoveLatest();
                    Message(lost != null
                        ? $"Death: -{_cfg.RunDeathHeatPenalty:0.#} heat, lost {lost.Def.Display}."
                        : $"Death: -{_cfg.RunDeathHeatPenalty:0.#} heat.");

                    SaveState();
                    return;
                }

                if (c.IsPlayer() || c.IsTamed()) return;

                _challenges?.ReportKill(PrefabNameOf(c));
            }
            catch (Exception ex)
            {
                LogOnce("character-died", ex);
            }
        }

        private static string PrefabNameOf(Character c)
        {
            var go = c.gameObject;
            return go == null ? string.Empty : go.name.Replace("(Clone)", string.Empty);
        }

        // --- Persistence ---

        private void TryResume()
        {
            // Loaded but waiting for the right world to come up — recheck without touching disk.
            if (_pendingResume != null)
            {
                TryResumePending();
                return;
            }

            if (_resumeAttempted) return;
            if (Player.m_localPlayer == null || ZoneSystem.instance == null) return;

            string name = CharacterName();
            if (name == null) return;

            // One shot: whatever happens below, don't hit the disk again every frame.
            _resumeAttempted = true;

            bool corrupt;
            var state = RunStorage.TryLoad(name, out corrupt);
            if (state == null)
            {
                // "No file" is the normal case and says nothing. "File there but unreadable" is
                // a loss the player must hear about: RunStorage has quarantined it as .corrupt
                // rather than deleting it, because it holds the only copy of the world's
                // original modifier values — those rates are now stuck at the run's inflated
                // ones until the file is repaired by hand.
                if (corrupt)
                {
                    HudNotice = CorruptSaveNotice;
                    Announce(CorruptSaveNotice);
                }
                return;
            }

            _pendingResume = state;
            TryResumePending();
        }

        private void TryResumePending()
        {
            var state = _pendingResume;
            string world = WorldIdentifier();
            if (world == null) return; // World still coming up; try again next tick.

            // ZoneSystem and the player must be up too, not just ZNet. This method re-runs every
            // tick once _pendingResume is set, so it bypasses the checks in TryResume that only
            // guard FIRST entry — and WorldIdentifier() is ZNet-driven, so it can go non-null a
            // tick or more before either of these exists. Both halves of the wait are load-bearing;
            // resuming early breaks a different thing each way, and neither self-heals:
            //
            //  - No ZoneSystem: RefreshMaxTier has no global keys to read, so it no-ops and MaxTier
            //    stays at int.MaxValue — tier gating silently off for the whole resumed run.
            //    Nothing corrects it later, because _accountedBossKeys is seeded from the save, so
            //    already-dead bosses never make PollBosses report progress.
            //
            //  - No player: RestoreFrom's ReapplyPassiveBoonEffects finds none (the Apply* methods
            //    bail on a null Player.m_localPlayer), so held passives are never applied. The
            //    respawn detector won't rescue them either — _trackedPlayer is set to null here, so
            //    when the real player appears DetectRespawnAndReapplyPassives reads
            //    isRespawn = !ReferenceEquals(null, null) = false and reapplies nothing.
            //
            // Just keep waiting — this is a per-tick retry already, and matches StartRun's guard.
            if (ZoneSystem.instance == null || Player.m_localPlayer == null) return;

            if (!string.IsNullOrEmpty(state.worldId) && state.worldId != world)
            {
                // Another world's run. Leave the file alone — the player may load that world later.
                if (HudNotice != WrongWorldNotice)
                {
                    HudNotice = WrongWorldNotice;
                    Debug.Log($"[ICanShowYouTheWorld] Saved run belongs to world '{state.worldId}', " +
                              $"current world is '{world}'; not resuming.");
                }
                return;
            }

            _pendingResume = null;

            // A save with no captured originals predates that field. Guessing vanilla 1f would
            // permanently rewrite any world using Valheim's own world-modifier presets, so the
            // only safe answer is to refuse the resume.
            if (state.modifierKeys == null || state.modifierKeys.Count == 0)
            {
                string stale = CharacterName();
                if (stale != null) RunStorage.Delete(stale);
                Announce("Run save from an older version — cannot resume; run discarded.");
                return;
            }

            try
            {
                RestoreFrom(state, world);
                Debug.Log($"[ICanShowYouTheWorld] Run Mode run resumed at {FormatTime(_elapsed)}.");
                Message($"Run resumed — {FormatTime(_elapsed)}.");
            }
            catch (Exception ex)
            {
                string name = CharacterName();
                Debug.LogError($"[ICanShowYouTheWorld] Failed to resume run for '{name}', discarding state: {ex}");
                if (name != null) RunStorage.Delete(name);

                // RestoreFrom may have partially applied boon effects (BuildEngines/RestoreHeld
                // succeeded before whatever threw) — unwind them rather than leaving a cheat
                // toggle or a snapshot boost stuck on with no active run to own it.
                SafeUnapplyAllBoonEffects();
                EndRun();
            }
        }

        private void RestoreFrom(RunSaveState s, string world)
        {
            _loggedFailures.Clear();
            _consecutiveTickFailures = 0;
            _frozen = false;
            HudNotice = null;

            _worldId = string.IsNullOrEmpty(s.worldId) ? world : s.worldId;
            _finalBossKey = ResolveFinalBossKey();

            _rngSeed = s.rngSeed;
            _rng = new Random(_rngSeed);

            BuildEngines(BuildChallengePool());

            // Read from the live world, not from s.defeatedBossKeys: that saved set is the
            // run's split bookkeeping (and may hold keys outside the boss table), whereas the
            // ceiling is a property of the world as it stands right now.
            RefreshMaxTier();

            _heat = new HeatModel();
            _heat.Add(s.heat);

            _elapsed = s.elapsedSeconds;
            _pollTimer = 0f;
            _saveTimer = 0f;
            _noArmorSeconds = 0f;
            _noArmorChallengeIds.Clear();

            _splitLabels.Clear();
            _splitTimes.Clear();
            if (s.splitLabels != null) _splitLabels.AddRange(s.splitLabels);
            if (s.splitTimes != null) _splitTimes.AddRange(s.splitTimes);

            // Everything in the saved list is already accounted for: pre-existing kills and
            // splits already taken alike. Neither should produce a second split.
            _accountedBossKeys.Clear();
            if (s.defeatedBossKeys != null)
            {
                foreach (var key in s.defeatedBossKeys) _accountedBossKeys.Add(key);
            }

            _challenges.RestoreActive(Zip(s.activeChallengeIds, s.activeChallengeProgress));
            _boons.RestoreHeld(Zip(s.heldBoonIds, s.heldBoonCooldowns), BuildRestoreCharges(s));

            // RestoreHeld is silent by design, so reapply effects for whatever survived — but
            // only the snapshot/buff-type passives (fleet/sharp/pack): their live player/item
            // state doesn't persist across a reload. Actives (wind/ember/way) keep their
            // persisted cooldown/charges as-is; re-running Apply("way") here would hand out a
            // free charge on every resume.
            ReapplyPassiveBoonEffects();

            _trackedPlayer = Player.m_localPlayer;

            // Seed the world's TRUE pre-run values before touching any global key. Without
            // this, ApplyBaseline would capture the run's own inflated rates as "original"
            // and RestoreAll would make them permanent — Valheim saves valued global keys
            // with the world. Saves lacking these are refused before we ever get here.
            _worldModifiers.ImportOriginals(s.modifierKeys, s.modifierValues);

            _worldModifiers.ApplyBaseline(_cfg);
            _worldModifiers.ApplyHeat(_heat.Heat, _cfg);
            _restorePending = true;
            _restoreWorldId = _worldId;

            _active = true;
        }

        /// <summary>
        /// Charges to hand BoonEngine.RestoreHeld, aligned by index to heldBoonIds. A save
        /// written before heldBoonCharges existed (null) — or, defensively, one shorter than the
        /// id list — carries no charge data at all; defaulting everything to 0 would silently
        /// zero out an unused Waystone charge and permanently dead-end that boon for the rest of
        /// the run. Only "way" gets the generous default of 1 (its starting grant); every other
        /// boon genuinely does start at 0 charges, saved data or not.
        /// </summary>
        private static List<int> BuildRestoreCharges(RunSaveState s)
        {
            if (s.heldBoonIds == null) return null;

            bool hasFullData = s.heldBoonCharges != null && s.heldBoonCharges.Count >= s.heldBoonIds.Count;
            var result = new List<int>(s.heldBoonIds.Count);

            for (int i = 0; i < s.heldBoonIds.Count; i++)
            {
                result.Add(hasFullData ? s.heldBoonCharges[i] : (s.heldBoonIds[i] == "way" ? 1 : 0));
            }
            return result;
        }

        private static IEnumerable<KeyValuePair<string, float>> Zip(List<string> ids, List<float> values)
        {
            if (ids == null) yield break;

            for (int i = 0; i < ids.Count; i++)
            {
                yield return new KeyValuePair<string, float>(
                    ids[i], values != null && i < values.Count ? values[i] : 0f);
            }
        }

        private void SaveState()
        {
            string name = CharacterName();
            if (name == null) return; // No profile this tick — skip rather than throw.

            var active = _challenges?.Active ?? (IReadOnlyList<ActiveChallenge>)new List<ActiveChallenge>();
            var held = _boons?.Held ?? (IReadOnlyList<HeldBoon>)new List<HeldBoon>();

            RunStorage.Save(name, new RunSaveState
            {
                elapsedSeconds = _elapsed,
                heat = _heat.Heat,
                defeatedBossKeys = _accountedBossKeys.ToList(),
                splitLabels = _splitLabels.ToList(),
                splitTimes = _splitTimes.ToList(),
                activeChallengeIds = active.Select(a => a.Def.Id).ToList(),
                activeChallengeProgress = active.Select(a => a.Progress).ToList(),
                heldBoonIds = held.Select(h => h.Def.Id).ToList(),
                heldBoonCooldowns = held.Select(h => h.CooldownRemaining).ToList(),
                heldBoonCharges = held.Select(h => h.Charges).ToList(),
                rngSeed = _rngSeed,
                worldId = _worldId,
                modifierKeys = _worldModifiers.ExportOriginalKeys(),
                modifierValues = _worldModifiers.ExportOriginalValues()
            });
        }

        private void DeleteState()
        {
            string name = CharacterName();
            if (name == null) return;
            RunStorage.Delete(name);
        }

        /// <summary>Active character's profile name, or null if it cannot be determined right now.</summary>
        private string CharacterName()
        {
            try
            {
                var game = Game.instance;
                if (game == null) return null;

                var profile = game.GetPlayerProfile();
                if (profile == null) return null;

                string name = profile.GetName();
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception ex)
            {
                LogOnce("character-name", ex);
                return null;
            }
        }

        /// <summary>
        /// Stable identity of the loaded world, or null if no world is loaded. GetWorldName()
        /// is null-safe in game code and empty when there is no world, which is what guards
        /// the GetWorldUID() call below (it dereferences m_world unchecked).
        /// </summary>
        private string WorldIdentifier()
        {
            try
            {
                var znet = ZNet.instance;
                if (znet == null) return null;

                string name = znet.GetWorldName();
                if (string.IsNullOrEmpty(name)) return null;

                return znet.GetWorldUID().ToString(CultureInfo.InvariantCulture) + ":" + name;
            }
            catch (Exception ex)
            {
                LogOnce("world-id", ex);
                return null;
            }
        }

        // --- Misc helpers ---

        /// <summary>
        /// The configured final-boss key if it names one of the five bosses, otherwise Yagluth.
        /// A typo in the config must not brick the mode: with an unknown key nothing would ever
        /// match in <see cref="PollBosses"/> and the run could never finish, so warn and fall back.
        /// </summary>
        private string ResolveFinalBossKey()
        {
            string key = _cfg.RunFinalBossKey;
            if (Bosses.Any(b => b.defeatKey == key)) return key;

            Announce($"runFinalBossKey '{key}' is not a known boss key — " +
                     $"using {DefaultFinalBossKey} (Yagluth) for this run.");
            return DefaultFinalBossKey;
        }

        /// <summary>
        /// True when a deferred restore may be flushed: either it belongs to the world that is
        /// loaded right now, or its world was never recorded (legacy state), which is the only
        /// case where restoring anywhere is the lesser evil.
        /// </summary>
        private bool RestoreWorldLoaded()
        {
            if (_restoreWorldId == null) return true;

            string world = WorldIdentifier();
            return world != null && world == _restoreWorldId;
        }

        /// <summary>Human-readable half of a "UID:name" world identifier, for messages.</summary>
        private static string ReadableWorldName(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return "unknown";

            int sep = identifier.IndexOf(':');
            return sep >= 0 && sep + 1 < identifier.Length ? identifier.Substring(sep + 1) : identifier;
        }

        private static bool SafeGetGlobalKey(ZoneSystem zone, string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return zone.GetGlobalKey(key);
        }

        /// <summary>Location prefab names for bosses not yet defeated on the current world — feeds the "way" boon's altar search.</summary>
        private IEnumerable<string> UndefeatedBossLocations()
        {
            var zone = ZoneSystem.instance;
            if (zone == null) yield break;

            foreach (var boss in Bosses)
            {
                if (!SafeGetGlobalKey(zone, boss.defeatKey)) yield return boss.locName;
            }
        }

        private void SafeUnapplyAllBoonEffects()
        {
            try { UnapplyAllBoonEffects?.Invoke(); }
            catch (Exception ex) { LogOnce("boon-unapply-all", ex); }
        }

        /// <summary>On-screen HUD message. Silently does nothing when there is no local player.</summary>
        private void Message(string text)
        {
            try { _game?.ShowMessage(text, MessageType.Center); }
            catch { /* HUD messages are never worth failing a run over. */ }
        }

        /// <summary>Log + console, for things the player must see even with no character in the world.</summary>
        private void Announce(string text)
        {
            Debug.Log("[ICanShowYouTheWorld] " + text);
            try { Console.instance?.Print(text); }
            catch { /* console may not exist yet */ }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>Logs a given failure site once per run so a per-frame fault can't flood the log.</summary>
        private void LogOnce(string site, Exception ex)
        {
            if (!_loggedFailures.Add(site)) return;
            Debug.LogError($"[ICanShowYouTheWorld] Run Mode '{site}' failed (further occurrences suppressed): {ex}");
        }

        // --- v1 content pools (config-driven pools are v2) ---

        /// <summary>
        /// The v1 challenge pool. Tier is world-progression gating (0 Meadows, 1 Black Forest,
        /// 2 Swamp, 3 Mountain, 4 Plains) — see <see cref="MaxTierForDefeatedCount"/>. Without it
        /// "Kill 8 Draugr" could be dealt before Eikthyr is down: unreachable for hours, and with
        /// the reroll heat cost a 0-heat player couldn't clear it either, so the slot just died.
        /// </summary>
        internal static List<ChallengeDefinition> DefaultPool() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id = "k-greydwarf", Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Greydwarf", Target = 10, HeatReward = 2, Display = "Kill 10 Greydwarves" },
            new ChallengeDefinition { Id = "k-skeleton",  Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Skeleton",  Target = 10, HeatReward = 2, Display = "Kill 10 Skeletons" },
            new ChallengeDefinition { Id = "k-troll",     Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Troll",     Target = 1,  HeatReward = 3, Display = "Slay a Troll" },
            new ChallengeDefinition { Id = "k-draugr",    Tier = 2, Kind = ChallengeKind.KillPrefab, Param = "Draugr",    Target = 8,  HeatReward = 3, Display = "Kill 8 Draugr" },
            new ChallengeDefinition { Id = "alt-150",     Tier = 3, Kind = ChallengeKind.ReachAltitude, Param = "", Target = 150, HeatReward = 2, Display = "Climb to 150m altitude" },
            new ChallengeDefinition { Id = "alt-90",      Tier = 1, Kind = ChallengeKind.ReachAltitude, Param = "", Target = 90,  HeatReward = 1, Display = "Climb to 90m altitude" },
            new ChallengeDefinition { Id = "c-wood",      Tier = 0, Kind = ChallengeKind.CollectItem, Param = "$item_wood",  Target = 100, HeatReward = 1, Display = "Hold 100 Wood" },
            new ChallengeDefinition { Id = "c-stone",     Tier = 0, Kind = ChallengeKind.CollectItem, Param = "$item_stone", Target = 100, HeatReward = 1, Display = "Hold 100 Stone" },
            new ChallengeDefinition { Id = "c-food",      Tier = 0, Kind = ChallengeKind.CollectFood, Param = "", Target = 20, HeatReward = 1, Display = "Hold 20 food items" },
            new ChallengeDefinition { Id = "naked-5",     Tier = 0, Kind = ChallengeKind.NoArmorMinutes, Param = "", Target = 5, HeatReward = 3, Display = "Wear no armor for 5 minutes" },
        };

        internal static List<BoonDefinition> DefaultBoons() => new List<BoonDefinition>
        {
            new BoonDefinition { Id = "fleet", Display = "Fleet-footed", IsPassive = true },
            new BoonDefinition { Id = "sharp", Display = "Sharpened",    IsPassive = true },
            new BoonDefinition { Id = "pack",  Display = "Packleader",   IsPassive = true },
            new BoonDefinition { Id = "wind",  Display = "Second Wind",  IsPassive = false, CooldownSeconds = 120f },
            new BoonDefinition { Id = "ember", Display = "Emberskin",    IsPassive = false, CooldownSeconds = 180f },
            new BoonDefinition { Id = "way",   Display = "Waystone",     IsPassive = false },
        };
    }
}
