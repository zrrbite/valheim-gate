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

        /// <summary>
        /// Boon offered in slot 1 of a fresh run's FIRST offer. Stamina is what an early Valheim
        /// run is actually short of — everything from running away to chopping the opening chain's
        /// trees is gated on it — so the opening pick is steered there rather than left to the rng.
        /// The other two options are random, and the player is free to take one instead.
        /// </summary>
        private const string FirstBoonPin = "enduring";

        private const float BossPollIntervalSeconds = 1f;
        private const float AutosaveIntervalSeconds = 5f;
        private const int ConsecutiveFailuresBeforeNotice = 5;

        /// <summary>
        /// How long ZNet must report no world at all before an active run is suspended rather than
        /// merely frozen. Loading screens produce brief gaps that must not tear a live run down; a
        /// genuine trip to the main menu never comes back inside this window.
        /// </summary>
        private const float WorldGoneSuspendSeconds = 5f;

        /// <summary>
        /// How long the world must be CONTINUOUSLY present before the outage clock is cleared.
        /// Without this hysteresis a world identity that flickered once a frame would reset the
        /// clock forever and the run would never suspend — the exact failure suspending exists
        /// to prevent.
        /// </summary>
        private const float WorldPresentResetSeconds = 1f;

        private const string FrozenNotice = "Run paused — world not loaded.";
        private const string RespawnNotice = "Run paused — waiting for respawn.";
        private const string WrongWorldNotice = "Run paused — this is not the world the run started in.";
        private const string AbandonWrongWorldNotice = "Load the run's world to abandon it.";
        private const string MultiplayerNotice = "Run Mode v1 supports local/hosted worlds only.";
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
        private readonly BossVigor _bossVigor = new BossVigor();

        /// <summary>
        /// PlayerStatType parsed from a StatDelta challenge's Param, cached per param string.
        /// A null value is a REMEMBERED FAILURE: the name doesn't exist in this build's enum, so
        /// the pool needs fixing, and there is nothing to gain from re-parsing it every second for
        /// the rest of the run.
        /// </summary>
        private readonly Dictionary<string, PlayerStatType?> _statTypes = new Dictionary<string, PlayerStatType?>();

        /// <summary>
        /// Stand-in for NaN in the saved baseline list. JsonUtility writes float.NaN as a bare
        /// <c>NaN</c> token, which is not legal JSON and would make the file unreadable to anything
        /// but Unity — including a human repairing it, which is the whole reason a corrupt run save
        /// is quarantined rather than deleted. Every stat these challenges measure counts upward
        /// from zero, so no real baseline is ever negative and the sentinel can't collide.
        /// </summary>
        private const float NoBaselineSentinel = -1f;

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
        /// Seconds ZNet has reported NO world under an active run. Cleared only after the world
        /// has been continuously present for <see cref="WorldPresentResetSeconds"/>, so it measures
        /// one real outage rather than the gaps between flickers — see
        /// <see cref="WorldGoneSuspendSeconds"/>.
        /// </summary>
        private float _worldGoneSeconds;

        /// <summary>Seconds the world has been continuously present; drives the hysteresis on <see cref="_worldGoneSeconds"/>.</summary>
        private float _worldPresentSeconds;

        /// <summary>
        /// Set on every playable tick, cleared by the one save taken when the world goes away.
        /// While set, that save is retried on each outage tick — a single attempt could silently
        /// no-op (no player profile on that exact frame) and lose everything played since the last
        /// autosave.
        /// </summary>
        private bool _outageSaveOwed;

        /// <summary>
        /// Character the current run belongs to, captured when the run starts or resumes. Used for
        /// the suspend log line: <see cref="CharacterName"/> at suspend time may already be a
        /// DIFFERENT character (that is precisely the case suspending exists to handle).
        /// </summary>
        private string _runCharacter;

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
        /// World + character seen on the previous inactive tick. A change in either re-arms the
        /// one-shot resume: <see cref="_resumeAttempted"/> used to latch for the whole process, so
        /// once a character switch had consumed it, going back to the run's own character and world
        /// never looked at the disk again.
        /// </summary>
        private string _idleWorld;
        private string _idleCharacter;

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

        /// <summary>
        /// True when a saved run for THIS character has been read from disk but cannot be resumed
        /// right now — almost always because it belongs to another world. The lobby offers to
        /// discard it (see <see cref="DiscardPendingRun"/>), which is the only way out when that
        /// world is gone for good.
        /// </summary>
        internal bool HasPendingResume => _pendingResume != null;

        /// <summary>Human-readable world name the parked run belongs to, or null when nothing is parked.</summary>
        internal string PendingResumeWorldName =>
            _pendingResume == null ? null : ReadableWorldName(_pendingResume.worldId);

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

                // v1 is single-machine only: the run owns the world's global keys outright, and on
                // a remote server those are the host's to set. Refuse rather than half-work.
                if (IsRemoteClient())
                {
                    Announce(MultiplayerNotice);
                    HudNotice = MultiplayerNotice;
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
                ResetOutageTracking();
                HudNotice = null;

                _worldId = WorldIdentifier();
                _runCharacter = CharacterName();
                _finalBossKey = finalBossKey;

                _rngSeed = Environment.TickCount;
                _rng = new Random(_rngSeed);

                BuildEngines(BuildChallengePool(), freshRun: true);

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
                // Free melee/tool stamina is baseline empowerment: the early game's stamina tax
                // is tedium, not difficulty. Re-run on the poll tick for newly crafted gear.
                _boonEffects.ApplyPugilist();
                _worldModifiers.ApplyHeat(0f, _cfg);
                _restorePending = true;
                _restoreWorldId = _worldId;

                _trackedPlayer = player;

                _active = true;
                _resumeAttempted = true;
                _pendingResume = null;

                // Deal the opening slots BEFORE the first save, rather than waiting for the first
                // Tick. The save is what a crash or a hard quit in the opening seconds resumes
                // from, and an empty active list there resumes as an ordinary run — RestoreActive
                // retires the opening chain whether or not it restored anything, so those three
                // scripted challenges would be gone for good. Dealing them here puts them in the
                // very first write. A zero dt only runs the top-up; nothing is timed yet.
                _challenges.Tick(0f);
                SyncStatDeltaBaselines();

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

                RearmResumeOnIdentityChange();
                TryResume();
                return;
            }

            // WORLD IDENTITY, and nothing else, decides whether a run is suspended. A null player
            // or a null ZoneSystem while the identity still matches is a state the run comes back
            // from inside its own world — and death is exactly that: Character.OnDeath hands the
            // player to Game.RequestRespawn on a ten-second timer, so Player.m_localPlayer is null
            // for longer than the suspend threshold on EVERY death while the world never moves.
            // Suspending there would tear the run down and rebuild it from disk mid-death (a
            // spurious "resumed" toast, the pending boon offer dropped, the no-armor timer reset,
            // a restore-debt round trip) and would leave DetectRespawnAndReapplyPassives with
            // nothing to detect. Those states freeze, indefinitely, and thaw when the player
            // returns.
            //
            // WorldIdentifier() is ZNet-driven (instance + a non-empty world name), so it is the
            // one signal that means "this game process still has this world open"; ZoneSystem and
            // the player both lag it during a load and both vanish for reasons that aren't a world
            // change. Suspension is therefore keyed to the identity alone.
            string world = WorldIdentifier();

            if (world != null && _worldId != null && world != _worldId)
            {
                // A different world is fully up: no grace period, and deliberately no save —
                // SaveState keys the file by the CURRENT profile, which after a character switch
                // is not the run's, and would stamp this run onto the wrong character.
                SuspendRun($"'{ReadableWorldName(world)}' loaded instead");
                return;
            }

            // Floored so a zero dt can't stall either accumulator.
            float step = Mathf.Max(dt, 0.001f);
            if (world == null)
            {
                _worldPresentSeconds = 0f;
                _worldGoneSeconds += step;
            }
            else
            {
                // Hysteresis: a single world-present frame does NOT clear the outage clock. A
                // flickering ZNet would otherwise reset it every other tick and hold the run
                // frozen at the menu forever, which is the fault suspending exists to prevent.
                _worldPresentSeconds += step;
                if (_worldPresentSeconds >= WorldPresentResetSeconds) _worldGoneSeconds = 0f;
            }

            bool playable = world != null && ZoneSystem.instance != null && Player.m_localPlayer != null;

            if (!playable)
            {
                // One flush of the seconds played since the last autosave, retried on every
                // outage tick until it lands: on the first tick of a logout the player profile is
                // often already gone, and a single silent no-op there is how a whole resumed
                // session used to vanish. Guarded on the run's OWN character, because during a
                // world switch the next profile can be up before ZNet reports the next world —
                // saving then would stamp this run onto somebody else's file.
                if (_outageSaveOwed && CharacterName() == _runCharacter && SaveState())
                {
                    _outageSaveOwed = false;
                }

                if (world == null && _worldGoneSeconds >= WorldGoneSuspendSeconds)
                {
                    SuspendRun("world unloaded");
                    return;
                }

                SetFrozen(true, world == null ? FrozenNotice : RespawnNotice);
                return;
            }

            _outageSaveOwed = true;
            SetFrozen(false, null);
            DetectRespawnAndReapplyPassives();

            _elapsed += dt;
            _challenges?.Tick(dt);

            // Immediately after the Tick that may have dealt one, not on the next poll: a
            // stat-delta slot baselined a second late would silently credit the player with
            // whatever they did in that second (and, on a slot dealt mid-sprint, could hand over
            // a chunk of "Run 800m" for free).
            SyncStatDeltaBaselines();

            _boons?.Tick(dt);
            _boonEffects.Tick(dt);
            TickBossVigor(dt);

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
                if (_active) _boonEffects.ApplyPugilist();
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
            else if (HudNotice == FrozenNotice || HudNotice == RespawnNotice ||
                     HudNotice == WrongWorldNotice || HudNotice == AbandonWrongWorldNotice)
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

        /// <summary>Re-runs Apply for every held passive boon (fleet/sharp/pack/mule/hearty/enduring) — used after a respawn and NOT after a resume (way's charge is persisted separately; re-running Apply("way") there would grant a free charge). Iterates the held set by its IsPassive flag, so a new passive joins simply by being one.</summary>
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
                // Drawn from BOTH a simple challenge's own Kind/Param and a composite's Subs:
                // a composite's top-level Kind/Param are unused filler (see
                // ChallengeDefinition.Subs), so a "hold 25 wood" SUB would never be polled if this
                // only looked at the top level.
                var wanted = _challenges.Active
                    .SelectMany(CollectItemParams)
                    .Distinct();

                foreach (var itemName in wanted)
                {
                    _challenges.ReportMeasure(ChallengeKind.CollectItem, itemName, inventory.CountItems(itemName));
                }

                if (_challenges.Active.Any(HasCollectFood))
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

            PollStatDeltas();
        }

        /// <summary>
        /// Item names an active slot wants counted this poll: its own Param if it's a simple
        /// CollectItem challenge, plus every CollectItem sub's Param if it's a composite. A
        /// composite's top-level Kind/Param are unused filler (see ChallengeDefinition.Subs).
        /// </summary>
        private static IEnumerable<string> CollectItemParams(ActiveChallenge a)
        {
            if (a.Def.Kind == ChallengeKind.CollectItem && !string.IsNullOrEmpty(a.Def.Param))
                yield return a.Def.Param;

            if (a.Def.Subs == null) yield break;
            foreach (var sub in a.Def.Subs)
                if (sub.Kind == ChallengeKind.CollectItem && !string.IsNullOrEmpty(sub.Param))
                    yield return sub.Param;
        }

        /// <summary>True when this slot needs the CollectFood total polled — its own Kind, or (for a composite) any sub's.</summary>
        private static bool HasCollectFood(ActiveChallenge a) =>
            a.Def.Kind == ChallengeKind.CollectFood ||
            (a.Def.Subs != null && a.Def.Subs.Any(sub => sub.Kind == ChallengeKind.CollectFood));

        // --- StatDelta challenges ---

        /// <summary>
        /// Reports each active stat-delta challenge's progress: how far the underlying LIFETIME
        /// stat has moved since the slot was dealt. Slots still waiting on a baseline (the profile
        /// wasn't reachable when they were dealt) are skipped rather than reported as zero —
        /// <see cref="SyncStatDeltaBaselines"/> retries them every frame.
        /// </summary>
        private void PollStatDeltas()
        {
            var active = _challenges.Active;

            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                if (a.Def.Kind != ChallengeKind.StatDelta || float.IsNaN(a.Baseline)) continue;

                float? current = ReadPlayerStat(a.Def.Param);
                if (current == null) continue;

                // Reported to THIS slot, not by param: each stat-delta slot has its own baseline,
                // so the same stat can be worth different progress in different slots (a resumed
                // half-done one beside a freshly dealt one). A param-scoped report would give both
                // whichever delta was computed last.
                //
                // Max-semantics stops a value going backwards; the floor here is for the
                // pathological case of a lifetime stat RESETTING below its own baseline, which
                // would otherwise report a negative and read as "no progress" forever.
                _challenges.ReportSlotMeasure(i, Mathf.Max(0f, current.Value - a.Baseline));
            }
        }

        /// <summary>
        /// Gives every stat-delta slot that lacks one its deal-time zero point. Runs each frame
        /// while playable, so a slot dealt while the player profile was briefly unreachable picks
        /// its baseline up on the next frame that works rather than being stuck at zero progress.
        ///
        /// Only ever fills a NaN: an existing baseline is the run's record of where the player
        /// started, and re-taking it later (on resume, above all) would move the goalposts up to
        /// wherever they've already got to and wipe the progress out. That is exactly why the
        /// baseline is persisted rather than recomputed.
        /// </summary>
        private void SyncStatDeltaBaselines()
        {
            if (_challenges == null) return;

            foreach (var a in _challenges.Active)
            {
                if (a.Def.Kind != ChallengeKind.StatDelta || !float.IsNaN(a.Baseline)) continue;

                float? current = ReadPlayerStat(a.Def.Param);
                if (current == null) continue;

                a.Baseline = current.Value;
            }
        }

        /// <summary>
        /// Current value of a lifetime player stat named by its PlayerStatType member, or null when
        /// the name is unknown, the profile isn't reachable, or the stat has no entry yet.
        ///
        /// Reads PlayerProfile.m_playerStats.m_stats (a Dictionary&lt;PlayerStatType, float&gt;,
        /// confirmed against the IL) rather than the indexer, which throws on a missing key. In
        /// practice PlayerStats' constructor pre-seeds every member with 0, but a build that adds
        /// an enum member without widening that loop would turn a missing entry into a run-killing
        /// exception every poll.
        /// </summary>
        private float? ReadPlayerStat(string param)
        {
            var type = ResolveStatType(param);
            if (type == null) return null;

            try
            {
                var stats = Game.instance?.GetPlayerProfile()?.m_playerStats?.m_stats;
                if (stats == null) return null;

                return stats.TryGetValue(type.Value, out float value) ? value : 0f;
            }
            catch (Exception ex)
            {
                LogOnce("read-player-stat", ex);
                return null;
            }
        }

        /// <summary>Parses (once per param string) a PlayerStatType member name; null when this build has no such member.</summary>
        private PlayerStatType? ResolveStatType(string param)
        {
            if (string.IsNullOrEmpty(param)) return null;

            if (_statTypes.TryGetValue(param, out var cached)) return cached;

            PlayerStatType? resolved =
                Enum.TryParse<PlayerStatType>(param, out var parsed) ? parsed : (PlayerStatType?)null;

            if (resolved == null)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Unknown PlayerStatType '{param}' — " +
                                 "that stat-delta challenge can never progress.");
            }

            _statTypes[param] = resolved;
            return resolved;
        }

        // --- Boss vigor ---

        /// <summary>
        /// Scales nearby bosses to the power banked so far. The multiplier is read fresh on every
        /// scan but only ever applied to bosses seen for the FIRST time (BossVigor keeps that
        /// bookkeeping), so a boss's health is fixed by the heat and boon count at the moment it
        /// came into view and cannot be moved afterwards.
        /// </summary>
        private void TickBossVigor(float dt)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            int boons = _boons?.Held?.Count ?? 0;
            float multiplier = 1f + _cfg.RunBossHpPerBoon * boons + _cfg.RunBossHpPerHeat * _heat.Heat;

            _bossVigor.Tick(dt, player.transform.position, multiplier);
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
            _runCharacter = null;
            _frozen = false;
            ResetOutageTracking();
            _trackedPlayer = null;

            // Restores, then clears. Boss health is the one Run Mode effect written straight into
            // the world's own data, so it has to be given back the way world modifiers are — a
            // run that ended must leave no permanently tougher boss behind.
            _bossVigor.RestoreAll();

            HudNotice = null;
        }

        /// <summary>Clears the world-outage clocks and the pending outage flush; no run, nothing owed.</summary>
        private void ResetOutageTracking()
        {
            _worldGoneSeconds = 0f;
            _worldPresentSeconds = 0f;
            _outageSaveOwed = false;
        }

        /// <summary>
        /// Unloads the run from memory without ending it: boon effects come off and the engines go,
        /// but the world keeps the run's modifiers and the state file stays on disk, because the run
        /// is not over. It comes back the moment its own character loads its own world again —
        /// RunStorage is per-character and <see cref="TryResumePending"/> matches the world, so
        /// resume is already the exact inverse of this.
        ///
        /// This replaces freezing-forever, which was the root of three separate faults from one
        /// cause — the run outliving its world inside a single game process. A frozen run kept its
        /// HUD strip and boon state alive at the main menu and on the NEXT character's world, its
        /// timer never counted, and it could not be abandoned there either (the abandon guard
        /// correctly refuses to write world A's rates into world B). A suspended run cannot do any
        /// of that: outside its own world it does not exist.
        ///
        /// World modifiers are deliberately NOT restored here. They belong to a run that is still
        /// live, and restoring would need the run's world loaded anyway — which, by definition of
        /// this method, it is not. Any outstanding restore debt (_restorePending/_restoreWorldId)
        /// survives untouched and still flushes if that world comes back.
        /// </summary>
        private void SuspendRun(string reason)
        {
            string worldId = _worldId;
            string character = _runCharacter ?? "its character";

            // The player may already be gone; every boon seam is null-safe about that.
            SafeUnapplyAllBoonEffects();
            EndRun();

            // Back to an ordinary "saved run sitting on disk" situation — the one-shot resume
            // must be allowed to fire again, and any parked state belongs to the run just unloaded.
            _resumeAttempted = false;
            _pendingResume = null;
            _idleWorld = null;
            _idleCharacter = null;

            Debug.Log($"[ICanShowYouTheWorld] Run suspended ({reason}) — resumes when {character} " +
                      $"loads world {worldId ?? "it started in"}.");
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

        /// <summary>
        /// Builds the challenge and boon engines for a run.
        ///
        /// <paramref name="freshRun"/> distinguishes StartRun from a resume. Only a fresh run pins
        /// the opening boon offer: on a resume the next offer is not the run's first, and steering
        /// it would hand a mid-run player a designed opener they may well have already had. The
        /// challenge engine needs no such flag — <see cref="ChallengeEngine.RestoreActive"/> retires
        /// the opening chain by itself, and a resume always calls it.
        /// </summary>
        private void BuildEngines(List<ChallengeDefinition> pool, bool freshRun)
        {
            _challenges = new ChallengeEngine(pool, _rng, _cfg.RunChallengeRefillSeconds);
            _challenges.Completed += OnChallengeCompleted;

            _boons = new BoonEngine(DefaultBoons(), _rng, _cfg.RunBoonOfferTimeoutSeconds);
            if (freshRun) _boons.FirstOfferPin = FirstBoonPin;
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

        /// <summary>
        /// Re-arms the one-shot resume whenever the loaded world or the active character changes
        /// while no run is in memory. Without this, <see cref="_resumeAttempted"/> latches for the
        /// life of the process: one look at the disk on the first world load, and a later
        /// character switch — or the reload of the run's own world after a suspend — would never
        /// look again. Any parked state is dropped at the same moment, since it was read for the
        /// character/world pair that just went away.
        /// </summary>
        private void RearmResumeOnIdentityChange()
        {
            string world = WorldIdentifier();
            string character = CharacterName();

            if (world == _idleWorld && character == _idleCharacter) return;

            _idleWorld = world;
            _idleCharacter = character;
            _resumeAttempted = false;
            _pendingResume = null;
        }

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

            // Same v1 rule as StartRun: a remote client must not drive the host's global keys.
            // The run stays parked (and the file untouched) — the lobby's discard button is the
            // way out if the player never intends to go back to a local copy of that world.
            if (IsRemoteClient())
            {
                if (HudNotice != MultiplayerNotice)
                {
                    HudNotice = MultiplayerNotice;
                    Announce(MultiplayerNotice + " Saved run not resumed on this server.");
                }
                return;
            }

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

        /// <summary>
        /// Throws away a saved run that cannot be reached from here — the lobby's escape hatch for
        /// a run parked on a world the player no longer has (deleted, left on another machine, or
        /// simply never coming back). Deletes the state file and clears the parked state.
        ///
        /// This is a lossy operation by construction: the state file holds the ONLY copy of that
        /// world's pre-run modifier values, so discarding it leaves the world on Run Mode's inflated
        /// rates forever unless the world is loaded again in this session (where the outstanding
        /// restore debt would still flush). The world is named in the log so the cost is on record.
        /// </summary>
        internal void DiscardPendingRun()
        {
            try
            {
                if (_pendingResume == null) return;

                string worldName = ReadableWorldName(_pendingResume.worldId);
                bool debtSurvives = _restorePending && _restoreWorldId == _pendingResume.worldId;

                string name = CharacterName();
                if (name != null) RunStorage.Delete(name);

                _pendingResume = null;

                // Nothing left on disk for this character/world pair; don't re-read it every tick.
                _resumeAttempted = true;
                if (HudNotice == WrongWorldNotice || HudNotice == MultiplayerNotice) HudNotice = null;

                Debug.LogWarning(
                    $"[ICanShowYouTheWorld] Saved run for '{name ?? "?"}' discarded — world '{worldName}' " +
                    (debtSurvives
                        ? "still has Run Mode's rates applied; they will be restored if that world is loaded again this session."
                        : "keeps Run Mode's modified rates: its original values are gone with the state file."));

                Announce($"Saved run on '{worldName}' discarded.");
                Message($"Saved run on '{worldName}' discarded.");
            }
            catch (Exception ex)
            {
                LogOnce("discard-pending", ex);
            }
        }

        private void RestoreFrom(RunSaveState s, string world)
        {
            _loggedFailures.Clear();
            _consecutiveTickFailures = 0;
            _frozen = false;
            ResetOutageTracking();
            HudNotice = null;

            _worldId = string.IsNullOrEmpty(s.worldId) ? world : s.worldId;
            _runCharacter = CharacterName();
            _finalBossKey = ResolveFinalBossKey();

            _rngSeed = s.rngSeed;
            _rng = new Random(_rngSeed);

            BuildEngines(BuildChallengePool(), freshRun: false);

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

            _challenges.RestoreActive(
                Zip(s.activeChallengeIds, s.activeChallengeProgress),
                BuildRestoreBaselines(s),
                BuildRestoreSubProgress(s));

            // Anything the save didn't carry a baseline for (a pre-alpha4 save, or a slot that
            // never managed to take one) gets its zero point NOW rather than staying NaN forever.
            // That does re-baseline against a higher lifetime value, but only where there was
            // nothing better to re-baseline against.
            SyncStatDeltaBaselines();

            _boons.RestoreHeld(Zip(s.heldBoonIds, s.heldBoonCooldowns), BuildRestoreCharges(s));

            // RestoreHeld is silent by design, so reapply effects for whatever survived — but
            // only the snapshot/buff-type passives (fleet/sharp/pack/mule/hearty/enduring): their live player/item
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
            _boonEffects.ApplyPugilist();   // baseline empowerment, same as StartRun
            _worldModifiers.ApplyHeat(_heat.Heat, _cfg);
            _restorePending = true;
            _restoreWorldId = _worldId;

            _active = true;

            // Mirror StartRun and write immediately. Without this the resume path had NO write at
            // all: _saveTimer starts at zero here, so the first (and, for a short session, only)
            // chance to persist anything was five seconds of in-world ticking away — and the
            // old freeze-on-logout path returned before the autosave block, so nothing was
            // flushed when the world went away either. A resumed session shorter than the
            // autosave cadence therefore left the file byte-for-byte untouched.
            SaveState();
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

        /// <summary>
        /// Saved stat-delta baselines, aligned index-for-index with activeChallengeIds and with the
        /// <see cref="NoBaselineSentinel"/> mapped back to NaN. Null for a save written before the
        /// list existed, which ChallengeEngine.RestoreActive reads as "no baselines" — the caller
        /// then takes fresh ones. A list SHORTER than the ids (only reachable from a hand-edited
        /// file) is passed through as-is: RestoreActive leaves the uncovered tail NaN.
        /// </summary>
        private static List<float> BuildRestoreBaselines(RunSaveState s)
        {
            if (s.activeChallengeBaselines == null) return null;

            return s.activeChallengeBaselines
                .Select(v => v <= NoBaselineSentinel ? float.NaN : v)
                .ToList();
        }

        /// <summary>
        /// Saved per-sub progress, aligned index-for-index with activeChallengeIds, one semicolon-
        /// joined string per slot parsed back into a float list (see
        /// <see cref="RunSaveState.activeChallengeSubProgress"/>). Null for a save written before
        /// composites existed, which ChallengeEngine.RestoreActive reads as "restart every
        /// composite sub at zero". An empty or unparsable piece never throws — it becomes 0f,
        /// matching every other malformed-save tolerance in this file.
        /// </summary>
        private static List<List<float>> BuildRestoreSubProgress(RunSaveState s)
        {
            if (s.activeChallengeSubProgress == null) return null;

            var result = new List<List<float>>(s.activeChallengeSubProgress.Count);
            foreach (var raw in s.activeChallengeSubProgress)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    result.Add(new List<float>());
                    continue;
                }

                var parts = raw.Split(';');
                var values = new List<float>(parts.Length);
                foreach (var part in parts)
                {
                    values.Add(float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                        ? v
                        : 0f);
                }
                result.Add(values);
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

        /// <summary>
        /// Writes the run to disk. Returns false when there is no player profile to key the file
        /// by this tick — the outage flush retries on that answer rather than losing the write
        /// (RunStorage.Save handles IO failures itself, and they are logged there).
        /// </summary>
        private bool SaveState()
        {
            string name = CharacterName();
            if (name == null) return false; // No profile this tick — skip rather than throw.

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
                activeChallengeBaselines = active
                    .Select(a => float.IsNaN(a.Baseline) ? NoBaselineSentinel : a.Baseline)
                    .ToList(),
                activeChallengeSubProgress = active
                    .Select(a => a.SubProgress == null
                        ? ""
                        : string.Join(";", a.SubProgress.Select(v => v.ToString(CultureInfo.InvariantCulture))))
                    .ToList(),
                heldBoonIds = held.Select(h => h.Def.Id).ToList(),
                heldBoonCooldowns = held.Select(h => h.CooldownRemaining).ToList(),
                heldBoonCharges = held.Select(h => h.Charges).ToList(),
                rngSeed = _rngSeed,
                worldId = _worldId,
                modifierKeys = _worldModifiers.ExportOriginalKeys(),
                modifierValues = _worldModifiers.ExportOriginalValues()
            });

            return true;
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

        /// <summary>
        /// True when this session is a CLIENT attached to someone else's server. ZNet.IsServer()
        /// returns the static m_isServer flag (checked against assembly_valheim's IL) — true for
        /// singleplayer and for the host of an open world, false only for a remote client, so the
        /// negation is the whole test.
        ///
        /// Fails open (reads as "local") if ZNet can't be asked: refusing to start a run because a
        /// lookup threw would be worse than the thing the check is guarding against.
        /// </summary>
        private bool IsRemoteClient()
        {
            try
            {
                var znet = ZNet.instance;
                return znet != null && !znet.IsServer();
            }
            catch (Exception ex)
            {
                LogOnce("is-remote-client", ex);
                return false;
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
        // Foundation-first (owner, 2026-08-19): composite multi-objective quests are built and
        // tested but PARKED out of the deal pool until the core loop's pacing is right — they
        // return as the story-quest system. Flip this to re-admit them.
        internal const bool IncludeCompositeQuests = false;

        internal static List<ChallengeDefinition> DefaultPool()
        {
            var pool = BuildFullPool();
            if (!IncludeCompositeQuests)
                pool.RemoveAll(d => d.Subs != null && d.Subs.Count > 0);
            return pool;
        }

        private static List<ChallengeDefinition> BuildFullPool() => new List<ChallengeDefinition>
        {
            // --- Opening chain: the first three slots of a fresh run, dealt in this order. ---
            // A deliberate on-ramp rather than three random quests at minute zero: gather the wood,
            // gather the stone, then make the thing they build into. The first two targets are the
            // Stone Axe recipe (5 Wood + 3 Stone), so clearing them leaves the player holding
            // exactly what the third is steering them to spend.
            //
            // The third counts CRAFTS, not the axe itself. There is no way to verify an item's
            // $item_ token from here — only 65 of them are hardcoded in assembly_valheim, the rest
            // live in the asset bundles — and a wrong token fails SILENTLY: CountItems returns 0
            // forever and the slot is dead. That is not a risk worth taking on the first minutes
            // of every fresh run, so the mechanic uses a PlayerStatType checked against the enum
            // and the display text does the steering.
            //
            // Openers never come round again — ChallengeEngine excludes them from every random
            // draw and reroll — so a completed or rerolled-away link is gone for that run.
            new ChallengeDefinition { Id = "o-wood",  Opener = true, Tier = 0, Kind = ChallengeKind.CollectItem, Param = "$item_wood",  Target = 5, HeatReward = 1, Display = "Hold 5 Wood" },
            new ChallengeDefinition { Id = "o-stone", Opener = true, Tier = 0, Kind = ChallengeKind.CollectItem, Param = "$item_stone", Target = 3, HeatReward = 1, Display = "Hold 3 Stone" },
            new ChallengeDefinition { Id = "o-craft", Opener = true, Tier = 0, Kind = ChallengeKind.StatDelta,   Param = "CraftsOrUpgrades", Target = 1, HeatReward = 1, Display = "Craft something — an axe!" },

            new ChallengeDefinition { Id = "k-greydwarf", Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Greydwarf", Target = 6, HeatReward = 2, Display = "Kill 6 Greydwarves" },
            new ChallengeDefinition { Id = "k-skeleton",  Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Skeleton",  Target = 6, HeatReward = 2, Display = "Kill 6 Skeletons" },
            new ChallengeDefinition { Id = "k-troll",     Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Troll",     Target = 1,  HeatReward = 3, Display = "Slay a Troll" },
            new ChallengeDefinition { Id = "k-draugr",    Tier = 2, Kind = ChallengeKind.KillPrefab, Param = "Draugr",    Target = 5,  HeatReward = 3, Display = "Kill 5 Draugr" },

            // More biome kill quests (alpha8, owner feedback: the pool needed more low-target
            // kill contracts, not just the handful above). Prefab names checked against what
            // this codebase already uses successfully: "Boar"/"Wolf" are live in SpawnService's
            // and CheatCommands' own prefab lists, "Fenring" is live in CheatCommands'
            // FenringIceNova_aoe lookup, and Greydwarf/Skeleton/Troll/Draugr are the existing
            // entries just above. `strings`/monodis over assembly_valheim.dll cannot confirm the
            // REST directly — mob and item prefab names are Unity asset data, not compiled C#
            // literals, so they never show up in the managed assembly's string heap at all (that
            // was re-confirmed here: even "Greydwarf"/"Draugr"/"Skeleton"/"Troll" above, already
            // known-good, return zero hits). The rest below are the well-established, stable
            // Valheim prefab names — cross-checked against each other rather than guessed.
            new ChallengeDefinition { Id = "k-neck",      Tier = 0, Kind = ChallengeKind.KillPrefab, Param = "Neck",     Target = 4, HeatReward = 1, Display = "Kill 4 Necks" },
            new ChallengeDefinition { Id = "k-deer",      Tier = 0, Kind = ChallengeKind.KillPrefab, Param = "Deer",     Target = 3, HeatReward = 1, Display = "Hunt 3 Deer" },
            new ChallengeDefinition { Id = "k-greyling",  Tier = 0, Kind = ChallengeKind.KillPrefab, Param = "Greyling", Target = 3, HeatReward = 1, Display = "Kill 3 Greylings" },
            new ChallengeDefinition { Id = "k-ghost",     Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Ghost",    Target = 3, HeatReward = 2, Display = "Kill 3 Ghosts" },
            new ChallengeDefinition { Id = "k-surtling",  Tier = 2, Kind = ChallengeKind.KillPrefab, Param = "Surtling", Target = 3, HeatReward = 2, Display = "Kill 3 Surtlings" },
            new ChallengeDefinition { Id = "k-lox",       Tier = 4, Kind = ChallengeKind.KillPrefab, Param = "Lox",         Target = 2, HeatReward = 3, Display = "Kill 2 Lox" },
            new ChallengeDefinition { Id = "k-deathsquito", Tier = 4, Kind = ChallengeKind.KillPrefab, Param = "Deathsquito", Target = 3, HeatReward = 3, Display = "Kill 3 Deathsquitoes" },

            new ChallengeDefinition { Id = "alt-150",     Tier = 3, Kind = ChallengeKind.ReachAltitude, Param = "", Target = 150, HeatReward = 2, Display = "Climb to 150m altitude" },
            new ChallengeDefinition { Id = "alt-90",      Tier = 1, Kind = ChallengeKind.ReachAltitude, Param = "", Target = 90,  HeatReward = 1, Display = "Climb to 90m altitude" },
            new ChallengeDefinition { Id = "c-wood",      Tier = 0, Kind = ChallengeKind.CollectItem, Param = "$item_wood",  Target = 25, HeatReward = 1, Display = "Hold 25 Wood" },
            new ChallengeDefinition { Id = "c-stone",     Tier = 0, Kind = ChallengeKind.CollectItem, Param = "$item_stone", Target = 25, HeatReward = 1, Display = "Hold 25 Stone" },
            new ChallengeDefinition { Id = "c-food",      Tier = 0, Kind = ChallengeKind.CollectFood, Param = "", Target = 10, HeatReward = 1, Display = "Hold 10 food items" },
            new ChallengeDefinition { Id = "naked-5",     Tier = 0, Kind = ChallengeKind.NoArmorMinutes, Param = "", Target = 3, HeatReward = 3, Display = "Wear no armor for 3 minutes" },

            // Stat-delta quests: small, fast, and measured from the value the stat held when the
            // slot was dealt (see SyncStatDeltaBaselines). Param is a PlayerStatType member NAME —
            // every one below was checked against the enum in assembly_valheim's IL, because a
            // typo here fails silently: Enum.TryParse just declines and the challenge sits at 0.
            //
            // The UNITS matter as much as the names, and three of these were wrong before the IL
            // was read properly. What Player.UpdateStats and the destructibles actually do:
            //
            //  - Tree         one per tree FELLED (incremented beside ZNetView.Destroy).
            //                 TreeChops, which this used to use, is one per axe SWING — "chop 15
            //                 trees" was really "hit something with an axe 15 times", clearable
            //                 on a single trunk.
            //  - MineHits     one per successful mining swing. Deliberately not Mines, which the
            //                 IL shows only fires when a whole MineRock/MineRock5 deposit is
            //                 finished off — 25 finished deposits is an expedition, not a tempo
            //                 quest.
            //  - DistanceRun  actual metres, and only while on the ground. So a metre target is
            //                 honest here.
            //  - DistanceSail a flat +1 per UpdateStats tick while on a ship, regardless of speed
            //                 or distance. The tick is 0.5s, so this counts half-seconds, not
            //                 metres: 360 is three minutes at sea, and the old "Sail 600m" was
            //                 really five minutes mislabelled.
            new ChallengeDefinition { Id = "s-chop",   Tier = 0, Kind = ChallengeKind.StatDelta, Param = "Tree",             Target = 3,   HeatReward = 1, Display = "Fell 3 trees" },
            new ChallengeDefinition { Id = "s-jump",   Tier = 0, Kind = ChallengeKind.StatDelta, Param = "Jumps",            Target = 15,  HeatReward = 1, Display = "Jump 15 times" },
            new ChallengeDefinition { Id = "s-pickup", Tier = 0, Kind = ChallengeKind.StatDelta, Param = "ItemsPickedUp",    Target = 20,  HeatReward = 1, Display = "Pick up 20 items" },
            // More, smaller quests: the pool turning over faster is what makes a run feel
            // like momentum rather than a checklist (owner feedback, alpha7 play-test).
            new ChallengeDefinition { Id = "s-craft2",  Tier = 0, Kind = ChallengeKind.StatDelta, Param = "CraftsOrUpgrades", Target = 3,  HeatReward = 1, Display = "Craft or upgrade 3 things" },
            new ChallengeDefinition { Id = "s-chop2",   Tier = 0, Kind = ChallengeKind.StatDelta, Param = "Tree",             Target = 6,  HeatReward = 1, Display = "Fell 6 trees" },
            new ChallengeDefinition { Id = "s-pickup2", Tier = 0, Kind = ChallengeKind.StatDelta, Param = "ItemsPickedUp",    Target = 40, HeatReward = 1, Display = "Pick up 40 items" },
            new ChallengeDefinition { Id = "s-jump2",   Tier = 0, Kind = ChallengeKind.StatDelta, Param = "Jumps",            Target = 30, HeatReward = 1, Display = "Jump 30 times" },
            new ChallengeDefinition { Id = "s-run2",    Tier = 1, Kind = ChallengeKind.StatDelta, Param = "DistanceRun",      Target = 900, HeatReward = 2, Display = "Run 900m" },
            new ChallengeDefinition { Id = "s-mine2",   Tier = 1, Kind = ChallengeKind.StatDelta, Param = "MineHits",         Target = 70, HeatReward = 2, Display = "Land 70 mining hits" },
            new ChallengeDefinition { Id = "s-kills",   Tier = 1, Kind = ChallengeKind.StatDelta, Param = "EnemyKills",       Target = 8,  HeatReward = 2, Display = "Kill 8 creatures — anything" },
            new ChallengeDefinition { Id = "s-kills2",  Tier = 2, Kind = ChallengeKind.StatDelta, Param = "EnemyKills",       Target = 15, HeatReward = 3, Display = "Kill 15 creatures — anything" },
            new ChallengeDefinition { Id = "s-food",    Tier = 1, Kind = ChallengeKind.StatDelta, Param = "FoodEaten",        Target = 3,  HeatReward = 1, Display = "Eat 3 meals" },
            new ChallengeDefinition { Id = "s-sleep",   Tier = 0, Kind = ChallengeKind.StatDelta, Param = "Sleep",            Target = 1,  HeatReward = 1, Display = "Sleep through a night" },
            new ChallengeDefinition { Id = "s-run",    Tier = 0, Kind = ChallengeKind.StatDelta, Param = "DistanceRun",      Target = 400, HeatReward = 1, Display = "Run 400m" },
            new ChallengeDefinition { Id = "s-mine",   Tier = 1, Kind = ChallengeKind.StatDelta, Param = "MineHits",         Target = 35,  HeatReward = 2, Display = "Land 35 mining hits" },
            new ChallengeDefinition { Id = "s-craft",  Tier = 1, Kind = ChallengeKind.StatDelta, Param = "CraftsOrUpgrades", Target = 5,   HeatReward = 1, Display = "Craft or upgrade 5 times" },
            new ChallengeDefinition { Id = "s-doors",  Tier = 1, Kind = ChallengeKind.StatDelta, Param = "DoorsOpened",      Target = 8,  HeatReward = 1, Display = "Open 8 doors" },
            new ChallengeDefinition { Id = "s-sail",   Tier = 2, Kind = ChallengeKind.StatDelta, Param = "DistanceSail",     Target = 180, HeatReward = 2, Display = "Sail for 90 seconds" },

            // --- Composite (multi-objective) quests — alpha8. ---
            // Each is a small checklist ("kill 1 boar, gather some food, ...") rather than a
            // single target; see ChallengeDefinition.Subs. Every sub below is KillPrefab,
            // CollectItem, or CollectFood — StatDelta subs are off-limits (see the doc comment on
            // Subs: one Baseline per slot, not one per sub).
            //
            // $item_raspberries could not be checked (see the comment on k-neck etc. above — item
            // tokens are asset data, invisible to the compiled assembly) and the existing pool's
            // own $item_ tokens ($item_wood/$item_stone) are likewise unverifiable that way, so
            // rather than gamble on a token that fails SILENTLY (dead sub forever), First Blood
            // uses CollectFood instead, exactly as the brief allows. Grave Robbing's coins sub is
            // dropped for the same reason, leaving it a single-sub composite. The "Brute" variant
            // of Greydwarf is "Greydwarf_Elite", not "GreydwarfBrute" — corrected here against
            // Forest Sweep's brief.
            new ChallengeDefinition
            {
                Id = "cq-first-blood", Tier = 0, Target = 1, HeatReward = 2, Display = "First Blood",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Boar", Target = 1, Label = "Kill 1 Boar" },
                    new SubObjective { Kind = ChallengeKind.CollectFood, Param = "", Target = 5, Label = "Gather 5 food" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-camp-life", Tier = 0, Target = 1, HeatReward = 3, Display = "Camp Life",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.CollectItem, Param = "$item_wood",  Target = 25, Label = "Hold 25 Wood" },
                    new SubObjective { Kind = ChallengeKind.CollectItem, Param = "$item_stone", Target = 10, Label = "Hold 10 Stone" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab,  Param = "Greyling",     Target = 2,  Label = "Kill 2 Greylings" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-forest-sweep", Tier = 1, Target = 1, HeatReward = 3, Display = "Forest Sweep",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Greydwarf",       Target = 4, Label = "Kill 4 Greydwarves" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Greydwarf_Elite",  Target = 1, Label = "Kill 1 Greydwarf Brute" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-grave-robbing", Tier = 1, Target = 1, HeatReward = 2, Display = "Grave Robbing",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Skeleton", Target = 4, Label = "Kill 4 Skeletons" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-swamp-errand", Tier = 2, Target = 1, HeatReward = 4, Display = "Swamp Errand",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Draugr", Target = 3, Label = "Kill 3 Draugr" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Leech",  Target = 2, Label = "Kill 2 Leeches" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-mountain-patrol", Tier = 3, Target = 1, HeatReward = 4, Display = "Mountain Patrol",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Wolf",    Target = 3, Label = "Kill 3 Wolves" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Fenring", Target = 1, Label = "Kill 1 Fenring" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-plains-contract", Tier = 4, Target = 1, HeatReward = 3, Display = "Plains Contract",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Goblin", Target = 5, Label = "Kill 5 Goblins" },
                }
            },
        };

        internal static List<BoonDefinition> DefaultBoons() => new List<BoonDefinition>
        {
            new BoonDefinition { Id = "fleet", Display = "Fleet-footed", IsPassive = true },
            new BoonDefinition { Id = "sharp", Display = "Sharpened",    IsPassive = true },
            new BoonDefinition { Id = "pack",  Display = "Packleader",   IsPassive = true },
            new BoonDefinition { Id = "mule",  Display = "Packmule",     IsPassive = true },
            new BoonDefinition { Id = "hearty", Display = "Hearty",      IsPassive = true },
            new BoonDefinition { Id = "enduring", Display = "Enduring",  IsPassive = true },
            new BoonDefinition { Id = "wind",  Display = "Second Wind",  IsPassive = false, CooldownSeconds = 120f },
            new BoonDefinition { Id = "ember", Display = "Emberskin",    IsPassive = false, CooldownSeconds = 180f },
            new BoonDefinition { Id = "way",   Display = "Waystone",     IsPassive = false },
        };
    }
}
