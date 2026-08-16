using System;
using System.Collections.Generic;
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

        private readonly IGameAPI _game;
        private readonly IConfiguration _cfg;
        private readonly WorldModifiers _worldModifiers = new WorldModifiers();
        private readonly HashSet<string> _loggedFailures = new HashSet<string>();

        // --- Boon effect seams. Task 10 assigns these; until then they are no-ops. ---
        internal Action<string> ApplyBoonEffect = _ => { };
        internal Action<string> UnapplyBoonEffect = _ => { };
        internal Action UnapplyAllBoonEffects = () => { };

        // --- Run state ---
        private HeatModel _heat = new HeatModel();
        private ChallengeEngine _challenges;
        private BoonEngine _boons;
        private Random _rng;
        private int _rngSeed;

        private bool _active;
        private float _elapsed;
        private float _pollTimer;
        private float _saveTimer;
        private float _noArmorSeconds;
        private float _sinceFirstTick;

        private readonly List<string> _splitLabels = new List<string>();
        private readonly List<float> _splitTimes = new List<float>();

        /// <summary>Boss keys that must not produce a split: already true when the run began, or already recorded.</summary>
        private readonly HashSet<string> _accountedBossKeys = new HashSet<string>();

        private bool _resumeAttempted;

        /// <summary>Score of the most recently finished run; surfaced by the HUD until dismissed.</summary>
        internal float LastScore;

        /// <summary>Transient notice for the HUD (e.g. kill hook unavailable). Null when there is nothing to say.</summary>
        internal string HudNotice;

        public RunService(IGameAPI game, IConfiguration cfg)
        {
            _game = game;
            _cfg = cfg;

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

        public bool KillHookAvailable => GameEvents.HookInstalled || _sinceFirstTick < KillHookGraceSeconds;

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
                if (_active)
                {
                    Message("A run is already in progress.");
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    Message("Run Mode needs a spawned character.");
                    return;
                }

                // Cheats and runs don't mix — make sure god mode ends up OFF.
                ForceGodModeOff();

                var zone = ZoneSystem.instance;
                if (zone == null)
                {
                    Message("Run Mode could not reach the world (ZoneSystem missing).");
                    return;
                }

                // A used world may already have kills. Those bosses are excluded from splits;
                // if the FINAL boss is already down there is no run to be had.
                _accountedBossKeys.Clear();
                foreach (var boss in Bosses)
                {
                    if (SafeGetGlobalKey(zone, boss.defeatKey))
                    {
                        _accountedBossKeys.Add(boss.defeatKey);
                    }
                }

                if (_accountedBossKeys.Contains(_cfg.RunFinalBossKey))
                {
                    _accountedBossKeys.Clear();
                    Message("The final boss is already dead on this world — start Run Mode on a fresh one.");
                    return;
                }

                _rngSeed = Environment.TickCount;
                _rng = new Random(_rngSeed);

                BuildEngines(BuildChallengePool());

                _heat = new HeatModel();
                _elapsed = 0f;
                _pollTimer = 0f;
                _saveTimer = 0f;
                _noArmorSeconds = 0f;
                _splitLabels.Clear();
                _splitTimes.Clear();
                LastScore = 0f;

                RevealBosses(player);

                _worldModifiers.ApplyBaseline(_cfg);
                _worldModifiers.ApplyHeat(0f, _cfg);

                _active = true;
                _resumeAttempted = true;

                SaveState();

                Debug.Log($"[ICanShowYouTheWorld] Run Mode started (seed={_rngSeed}, " +
                          $"pre-defeated={_accountedBossKeys.Count}).");
                Message("Run Mode started. Good luck.");
            }
            catch (Exception ex)
            {
                LogOnce("start-run", ex);
                Message("Run Mode failed to start — see the log.");
            }
        }

        public void AbandonRun()
        {
            try
            {
                if (!_active) return;

                _worldModifiers.RestoreAll();
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
            }
            catch (Exception ex)
            {
                LogOnce("tick", ex);
            }
        }

        // --- Tick internals ---

        private void TickInner(float dt)
        {
            _sinceFirstTick += dt;

            if (!_active)
            {
                TryResume();
                return;
            }

            _elapsed += dt;
            _challenges?.Tick(dt);
            _boons?.Tick(dt);

            HandleBoonOfferInput();

            _pollTimer += dt;
            if (_pollTimer >= BossPollIntervalSeconds)
            {
                float pollDt = _pollTimer;
                _pollTimer = 0f;
                PollBosses();

                // PollBosses may have finished the run.
                if (_active)
                {
                    PollMeasures(pollDt);
                }
            }

            if (!_active) return;

            _saveTimer += dt;
            if (_saveTimer >= AutosaveIntervalSeconds)
            {
                _saveTimer = 0f;
                SaveState();
            }
        }

        private void HandleBoonOfferInput()
        {
            if (_boons == null || _boons.CurrentOffer.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.Keypad1)) _boons.Pick(0);
            else if (Input.GetKeyDown(KeyCode.Keypad2)) _boons.Pick(1);
            else if (Input.GetKeyDown(KeyCode.Keypad3)) _boons.Pick(2);
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

            _worldModifiers.RestoreAll();
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
            _accountedBossKeys.Clear();
        }

        private void BuildEngines(List<ChallengeDefinition> pool)
        {
            _challenges = new ChallengeEngine(pool, _rng, _cfg.RunChallengeRefillSeconds);
            _challenges.Completed += OnChallengeCompleted;

            _boons = new BoonEngine(DefaultBoons(), _rng, _cfg.RunBoonOfferTimeoutSeconds);
            _boons.Gained += OnBoonGained;
            _boons.Lost += OnBoonLost;
        }

        private List<ChallengeDefinition> BuildChallengePool()
        {
            var pool = DefaultPool();

            if (KillHookAvailable)
            {
                HudNotice = null;
                return pool;
            }

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

        private void ForceGodModeOff()
        {
            try
            {
                // Resolved lazily: RunService is constructed alongside the other services,
                // so the container may not have handed out ICombatService yet at ctor time.
                var combat = ModBootstrap.GetService<ICombatService>();
                if (combat != null && combat.GodMode)
                {
                    combat.ToggleGodMode();
                }
            }
            catch (Exception ex)
            {
                LogOnce("godmode-off", ex);
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
                if (!_active || c == null) return;

                if (c == Player.m_localPlayer)
                {
                    _heat.Remove(_cfg.RunDeathHeatPenalty);
                    _worldModifiers.ApplyHeat(_heat.Heat, _cfg);

                    // RemoveLatest raises Lost, which unapplies the effect.
                    var lost = _boons?.RemoveLatest();
                    if (lost != null)
                    {
                        Message($"Death: -{_cfg.RunDeathHeatPenalty:0.#} heat, lost {lost.Def.Display}.");
                    }
                    else
                    {
                        Message($"Death: -{_cfg.RunDeathHeatPenalty:0.#} heat.");
                    }

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
            if (_resumeAttempted) return;
            if (Player.m_localPlayer == null) return;

            string name = CharacterName();
            if (name == null) return;

            // One shot: whatever happens below, don't hit the disk again every frame.
            _resumeAttempted = true;

            var state = RunStorage.TryLoad(name);
            if (state == null) return;

            try
            {
                RestoreFrom(state);
                Debug.Log($"[ICanShowYouTheWorld] Run Mode run resumed at {FormatTime(_elapsed)}.");
                Message($"Run resumed — {FormatTime(_elapsed)}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to resume run for '{name}', discarding state: {ex}");
                RunStorage.Delete(name);
                EndRun();
            }
        }

        private void RestoreFrom(RunSaveState s)
        {
            _rngSeed = s.rngSeed;
            _rng = new Random(_rngSeed);

            BuildEngines(BuildChallengePool());

            _heat = new HeatModel();
            _heat.Add(s.heat);

            _elapsed = s.elapsedSeconds;
            _pollTimer = 0f;
            _saveTimer = 0f;
            _noArmorSeconds = 0f;

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

            RestoreChallenges(s);
            RestoreBoons(s);

            _worldModifiers.ApplyBaseline(_cfg);
            _worldModifiers.ApplyHeat(_heat.Heat, _cfg);

            _active = true;
        }

        private void RestoreChallenges(RunSaveState s)
        {
            if (s.activeChallengeIds == null || s.activeChallengeIds.Count == 0) return;

            // ChallengeEngine exposes its active list as IReadOnlyList over the backing List,
            // so the restore writes through that. Guarded: if the backing type ever changes,
            // the run simply redraws challenges instead of throwing.
            var active = _challenges.Active as List<ActiveChallenge>;
            if (active == null)
            {
                LogOnce("restore-challenges", new InvalidOperationException(
                    "ChallengeEngine.Active is no longer a writable List<ActiveChallenge>; challenges were redrawn."));
                return;
            }

            var byId = DefaultPool().ToDictionary(d => d.Id, d => d);

            active.Clear();
            for (int i = 0; i < s.activeChallengeIds.Count; i++)
            {
                if (!byId.TryGetValue(s.activeChallengeIds[i], out var def)) continue;

                active.Add(new ActiveChallenge
                {
                    Def = def,
                    Progress = i < s.activeChallengeProgress?.Count ? s.activeChallengeProgress[i] : 0f
                });
            }
        }

        private void RestoreBoons(RunSaveState s)
        {
            if (s.heldBoonIds == null || s.heldBoonIds.Count == 0) return;

            var held = _boons.Held as List<HeldBoon>;
            if (held == null)
            {
                LogOnce("restore-boons", new InvalidOperationException(
                    "BoonEngine.Held is no longer a writable List<HeldBoon>; held boons were dropped."));
                return;
            }

            var byId = DefaultBoons().ToDictionary(d => d.Id, d => d);

            held.Clear();
            for (int i = 0; i < s.heldBoonIds.Count; i++)
            {
                if (!byId.TryGetValue(s.heldBoonIds[i], out var def)) continue;

                held.Add(new HeldBoon
                {
                    Def = def,
                    CooldownRemaining = i < s.heldBoonCooldowns?.Count ? s.heldBoonCooldowns[i] : 0f
                });

                // Adding directly bypasses the Gained event, so reapply explicitly.
                try { ApplyBoonEffect?.Invoke(def.Id); }
                catch (Exception ex) { LogOnce("boon-reapply", ex); }
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
                rngSeed = _rngSeed
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

        // --- Misc helpers ---

        private static bool SafeGetGlobalKey(ZoneSystem zone, string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return zone.GetGlobalKey(key);
        }

        private void SafeUnapplyAllBoonEffects()
        {
            try { UnapplyAllBoonEffects?.Invoke(); }
            catch (Exception ex) { LogOnce("boon-unapply-all", ex); }
        }

        private void Message(string text)
        {
            try { _game?.ShowMessage(text, MessageType.Center); }
            catch { /* HUD messages are never worth failing a run over. */ }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>Logs a given failure site once per session so a per-frame fault can't flood the log.</summary>
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
