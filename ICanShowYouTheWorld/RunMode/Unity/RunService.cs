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

        private const float BossPollIntervalSeconds = 1f;
        private const float AutosaveIntervalSeconds = 5f;
        private const float KillHookGraceSeconds = 60f;
        private const int ConsecutiveFailuresBeforeNotice = 5;

        private const string FrozenNotice = "Run paused — world not loaded.";
        private const string WrongWorldNotice = "Run paused — this is not the world the run started in.";
        private const string KillHookNotice = "Kill hook never fired — kill challenges will not progress.";

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

        private bool _active;
        private float _elapsed;
        private float _pollTimer;
        private float _saveTimer;
        private float _noArmorSeconds;
        private float _graceElapsed;
        private bool _killHookWarned;
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
        /// The grace window runs from StartRun (or resume), not from mod init: the injected
        /// death hook can only prove itself once something dies, so a fresh run assumes it
        /// works and finds out within the first minute.
        /// </summary>
        public bool KillHookAvailable => GameEvents.HookInstalled || _graceElapsed < KillHookGraceSeconds;

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

                // A used world may already have kills. Those bosses are excluded from splits;
                // if the FINAL boss is already down there is no run to be had.
                var preDefeated = new HashSet<string>();
                foreach (var boss in Bosses)
                {
                    if (SafeGetGlobalKey(zone, boss.defeatKey)) preDefeated.Add(boss.defeatKey);
                }

                if (preDefeated.Contains(_cfg.RunFinalBossKey))
                {
                    Message("The final boss is already dead on this world — start Run Mode on a fresh one.");
                    return;
                }

                // --- Committed: from here on we mutate state. ---
                ForceGodModeOff();

                _loggedFailures.Clear();
                _consecutiveTickFailures = 0;
                _killHookWarned = false;
                _frozen = false;
                HudNotice = null;

                _worldId = WorldIdentifier();
                _graceElapsed = 0f;

                _rngSeed = Environment.TickCount;
                _rng = new Random(_rngSeed);

                BuildEngines(BuildChallengePool());

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

                _restorePending = !_worldModifiers.RestoreAll();
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
                if (!_active || _challenges == null) return;

                if (_heat.Heat < _cfg.RunRerollHeatCost)
                {
                    HudNotice = $"Not enough heat to reroll (need {_cfg.RunRerollHeatCost:0.#}).";
                    return;
                }

                // Charge only on a reroll that actually happened — a rejected slot index
                // or an exhausted pool should not cost heat.
                if (!_challenges.Reroll(slot)) return;

                _heat.Remove(_cfg.RunRerollHeatCost);
                _worldModifiers.ApplyHeat(_heat.Heat, _cfg);
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
                // original rates; keep trying until ZoneSystem is back.
                if (_restorePending && _worldModifiers.RestoreAll()) _restorePending = false;

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
            _graceElapsed += dt;
            _challenges?.Tick(dt);
            _boons?.Tick(dt);
            _boonEffects.Tick(dt);

            WarnIfKillHookDead();
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
            else if (HudNotice == FrozenNotice || HudNotice == WrongWorldNotice)
            {
                HudNotice = null;
            }
        }

        /// <summary>
        /// Once the grace window closes without the injected hook ever firing, say so.
        /// Kill challenges already in the active set are left alone; re-drawing the pool
        /// mid-run is deliberately deferred.
        /// </summary>
        private void WarnIfKillHookDead()
        {
            if (_killHookWarned || KillHookAvailable) return;
            if (_challenges == null || !_challenges.Active.Any(a => a.Def.Kind == ChallengeKind.KillPrefab)) return;

            _killHookWarned = true;
            HudNotice = KillHookNotice;
            Debug.LogWarning("[ICanShowYouTheWorld] " + KillHookNotice);
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

            foreach (var boss in Bosses)
            {
                if (_accountedBossKeys.Contains(boss.defeatKey)) continue;
                if (!SafeGetGlobalKey(zone, boss.defeatKey)) continue;

                _accountedBossKeys.Add(boss.defeatKey);
                _splitLabels.Add(boss.display);
                _splitTimes.Add(_elapsed);

                try { PermanentRecord.RecordBossKill(Player.m_localPlayer, boss.defeatKey); }
                catch (Exception ex) { LogOnce("record-boss", ex); }

                Message($"{boss.display} down — {FormatTime(_elapsed)}");

                if (boss.defeatKey == _cfg.RunFinalBossKey) finished = true;
            }

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

        // --- Lifecycle helpers ---

        private void FinishRun()
        {
            LastScore = RunScore.Compute(
                _cfg.RunParTimeMinutes * 60f, _elapsed, _heat.Heat, _cfg.RunHeatScoreWeight);

            try { PermanentRecord.RecordScore(Player.m_localPlayer, LastScore); }
            catch (Exception ex) { LogOnce("record-score", ex); }

            _restorePending = !_worldModifiers.RestoreAll();
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
            _killHookWarned = false;
            _trackedPlayer = null;
            HudNotice = null;
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
        /// The full v1 pool. Because the grace window is reset by StartRun and resume, this is
        /// optimistic in practice — the filtered branch below only bites if the grace window is
        /// ever configured away. A run that turns out to have a dead hook gets a HUD notice from
        /// <see cref="WarnIfKillHookDead"/> instead; the active set is not re-drawn mid-run.
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

            var state = RunStorage.TryLoad(name);
            if (state == null) return;

            _pendingResume = state;
            TryResumePending();
        }

        private void TryResumePending()
        {
            var state = _pendingResume;
            string world = WorldIdentifier();
            if (world == null) return; // World still coming up; try again next tick.

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
            _killHookWarned = false;
            _frozen = false;
            HudNotice = null;

            _worldId = string.IsNullOrEmpty(s.worldId) ? world : s.worldId;
            _graceElapsed = 0f;

            _rngSeed = s.rngSeed;
            _rng = new Random(_rngSeed);

            BuildEngines(BuildChallengePool());

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

        internal static List<ChallengeDefinition> DefaultPool() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id = "k-greydwarf", Kind = ChallengeKind.KillPrefab, Param = "Greydwarf", Target = 10, HeatReward = 2, Display = "Kill 10 Greydwarves" },
            new ChallengeDefinition { Id = "k-skeleton",  Kind = ChallengeKind.KillPrefab, Param = "Skeleton",  Target = 10, HeatReward = 2, Display = "Kill 10 Skeletons" },
            new ChallengeDefinition { Id = "k-troll",     Kind = ChallengeKind.KillPrefab, Param = "Troll",     Target = 1,  HeatReward = 3, Display = "Slay a Troll" },
            new ChallengeDefinition { Id = "k-draugr",    Kind = ChallengeKind.KillPrefab, Param = "Draugr",    Target = 8,  HeatReward = 3, Display = "Kill 8 Draugr" },
            new ChallengeDefinition { Id = "alt-150",     Kind = ChallengeKind.ReachAltitude, Param = "", Target = 150, HeatReward = 2, Display = "Climb to 150m altitude" },
            new ChallengeDefinition { Id = "alt-90",      Kind = ChallengeKind.ReachAltitude, Param = "", Target = 90,  HeatReward = 1, Display = "Climb to 90m altitude" },
            new ChallengeDefinition { Id = "c-wood",      Kind = ChallengeKind.CollectItem, Param = "$item_wood",  Target = 100, HeatReward = 1, Display = "Hold 100 Wood" },
            new ChallengeDefinition { Id = "c-stone",     Kind = ChallengeKind.CollectItem, Param = "$item_stone", Target = 100, HeatReward = 1, Display = "Hold 100 Stone" },
            new ChallengeDefinition { Id = "c-mushroom",  Kind = ChallengeKind.CollectItem, Param = "$item_mushroomcommon", Target = 20, HeatReward = 1, Display = "Hold 20 Mushrooms" },
            new ChallengeDefinition { Id = "naked-5",     Kind = ChallengeKind.NoArmorMinutes, Param = "", Target = 5, HeatReward = 3, Display = "Wear no armor for 5 minutes" },
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
