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
        ///
        /// Follows the stamina merge: this named "enduring" until alpha34 folded the five stamina
        /// boons into Tireless. A pin naming a boon that no longer exists is not an error — the
        /// engine simply ignores it — so this would have gone on quietly un-steering every opening
        /// offer with nothing to show for it. Worth remembering that the pin fails SILENTLY.
        /// </summary>
        private const string FirstBoonPin = "tireless";

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

        /// <summary>Bitmask of Heightmap.Biome values the player has stood in during THIS run.
        /// Feeds the engine's ExternalFilter so biome-gated quests (ghosts, wolves, fulings…)
        /// are only dealt once the player has actually been where they live. Meadows is seeded
        /// at run start.</summary>
        private int _visitedBiomes;

        /// <summary>
        /// Categories of building piece the player has been seen to have built during THIS run —
        /// the vocabulary of <see cref="ChallengeKind.BuildPiece"/> and
        /// <see cref="ChallengeDefinition.RequiresBuilt"/>. Feeds the questline's build steps and
        /// the ExternalFilter gate, and is persisted with the run.
        ///
        /// LATCHED, never removed from. The scan can only see what is near the player right now,
        /// so a set that could shrink would un-earn a finished step the moment the player left
        /// home, and would make a gated task flicker in and out of the draw pool depending on where
        /// they were standing when a slot refilled. Tearing the piece down again does not undo the
        /// fact that they built it.
        /// </summary>
        private readonly HashSet<string> _builtSeen = new HashSet<string>();

        /// <summary>
        /// The run's stash — things put aside that follow the player between bases and acts. See
        /// <see cref="RunStash"/> for why it is run state rather than a chest in the world.
        /// </summary>
        private readonly RunStash _stash = new RunStash();

        /// <summary>Act I's deer: stars, the Herald, and what a kill draws. See <see cref="DeerHerd"/>.</summary>
        private DeerHerd _deer;

        /// <summary>
        /// Creature names the questline reports that are NOT Valheim prefabs, and which the name
        /// validator must therefore not look up.
        ///
        /// Only the Herald so far. It is an ordinary Deer wearing a name, so its step cannot match
        /// on "Deer" — any deer would finish it — and the host reports a synthetic name instead when
        /// that specific creature dies. Looking it up in ZNetScene would correctly report that no
        /// such creature exists, which would be a false alarm every launch.
        /// </summary>
        private static readonly HashSet<string> SyntheticCreatureNames =
            new HashSet<string> { DeerHerd.HeraldKillName };

        /// <summary>The saga's acts, built once per service. Pure content — see <see cref="Acts"/>.</summary>
        private readonly List<ActDefinition> _acts = Acts();

        /// <summary>
        /// Index into <see cref="_acts"/>. Not persisted: <see cref="CurrentActIndex"/> derives it
        /// from the world's defeated bosses, and this only caches that between polls so
        /// <see cref="RefreshAct"/> can tell a transition from a no-op.
        /// </summary>
        private int _actIndex;

        /// <summary>
        /// Reused across polls: <see cref="Piece.GetAllPiecesInRadius"/> fills a caller-owned list,
        /// and this runs once a second for the life of a run.
        /// </summary>
        private readonly List<Piece> _pieceBuffer = new List<Piece>();

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
            _boonEffects = new BoonEffects(
                () => _boons?.Held, UndefeatedBossLocations, DefeatedBossCount, LoanSkill, GrantItem);
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

        /// <summary>
        /// Where the Herald is, while its step is the one in play; null otherwise. The HUD shows it
        /// under the step — a named creature somewhere in a 250m radius with no direction is a
        /// search rather than a hunt.
        /// </summary>
        public string HeraldBearing
        {
            get
            {
                if (!_active || _deer == null || !ActIsMeadows) return null;

                bool wanted = _challenges != null && _challenges.Tracks.Any(t =>
                    t.Current != null &&
                    t.Current.Def.Kind == ChallengeKind.KillPrefab &&
                    t.Current.Def.Param == DeerHerd.HeraldKillName);

                return wanted ? _deer.HeraldBearing(Player.m_localPlayer) : null;
            }
        }

        public int HomewardCharges => _active ? _homewardCharges : 0;
        public float EarnedHealth => _active ? _taskHealthReward : 0f;

        public ActDefinition CurrentAct =>
            _active && _actIndex >= 0 && _actIndex < _acts.Count ? _acts[_actIndex] : null;

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
                _deer = new DeerHerd(_cfg, _rng);

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

                _visitedBiomes = (int)Heightmap.Biome.Meadows;
                try { if (Player.m_localPlayer != null) _visitedBiomes |= (int)Player.m_localPlayer.GetCurrentBiome(); } catch { }

                // Clears state from any earlier run in this session; it does NOT stop an existing
                // house from being credited, and is not trying to.
                //
                // KNOWN AND ACCEPTED: start a run standing in a base you already built and the
                // fire/bed/chest steps complete within a second of being dealt, rewards and all.
                // Distinguishing "built during this run" needs a per-piece ZDO snapshot taken at
                // run start, which still misses anything outside its radius — real machinery for
                // half a fix. Anyone running at an established base has skipped far more than three
                // steps' worth of progression already; the mode is built for a fresh start.
                _builtSeen.Clear();
                _stash.Clear();
                _deer.Reset();
                _unbaselinedSeen.Clear();
                _warnedUnbaselined.Clear();
                _taskHealthReward = 0f;
                _homewardCharges = 0;
                _discovered.Clear();
                _worldModifiers.ApplyBaseline(_cfg);
                // Free melee/tool stamina is baseline empowerment: the early game's stamina tax
                // is tedium, not difficulty. Re-run on the poll tick for newly crafted gear.
                _boonEffects.ApplyPugilist();

                // Fresh run: take new snapshots, so clear any stale ones first. Nothing is
                // loaned here — skill is the questline's to pay out (owner, alpha17: WoodCutting
                // starts at the axe step's 25 and grows from there, rather than opening at the
                // cap), so this run starts with the character's own levels.
                _skillLoans.Clear();
                _worldModifiers.ApplyHeat(0f, _cfg);
                _restorePending = true;
                _restoreWorldId = _worldId;

                _trackedPlayer = player;

                _active = true;
                _resumeAttempted = true;
                _pendingResume = null;

                // Deal the three random slots BEFORE the first save rather than waiting for the
                // first Tick: the save is what a crash or a hard quit in the opening seconds
                // resumes from, and an empty active list there would resume with nothing in play
                // until the refill cooldowns elapsed. A zero dt only runs the top-up; nothing is
                // timed yet. (The questline's step is already seated — SetMainChain deals it.)
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
                RestoreLoanedSkills();  // before EndRun: the snapshots are run state EndRun clears
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
                    RemoveHeat(_cfg.RunRerollHeatCost);
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
        /// Keypad4/5/6/7/8 activate held wind/ember/way/brother/windfall while a run is active.
        /// Gated on there being no boon offer pending, matching the brief; the offer keys (1/2/3)
        /// don't overlap with these anyway, so this is a UX choice, not a conflict-avoidance one.
        /// </summary>
        private void HandleBoonActivationInput()
        {
            if (_boons == null || _boons.CurrentOffer.Count > 0) return;

            if (Input.GetKeyDown(KeyCode.Keypad4)) TryActivateHeldBoon("wind");
            else if (Input.GetKeyDown(KeyCode.Keypad5)) TryActivateHeldBoon("ember");
            else if (Input.GetKeyDown(KeyCode.Keypad6)) TryActivateHeldBoon("way");
            else if (Input.GetKeyDown(KeyCode.Keypad7)) TryActivateHeldBoon("brother");
            else if (Input.GetKeyDown(KeyCode.Keypad8)) TryActivateHeldBoon("windfall");
            // Keypad 9 is not a boon: Homeward is a run mechanic earned from bosses, so it sits
            // beside the boon keys rather than among them.
            else if (Input.GetKeyDown(KeyCode.Keypad9)) TryHomeward();
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

            // The per-completion health is a loan too, and NOT a boon, so the passive loop above
            // does not reach it. A respawn hands back a Player with vanilla fields, so without this
            // every point of health the run had earned would quietly vanish on death.
            ApplyTaskHealthReward();

            // Valheim applies skill LOSS on death (Skills.OnDeath → LowerAllSkills), which this
            // mode accepts as vanilla rather than suppressing — but the LOAN is not the player's
            // skill to lose, so it goes straight back on. The snapshot is untouched: the original
            // is still what gets given back at the end of the run.
            ReapplyLoanedSkills();
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
                GrantBossSpoils();
                RechargeWaystone();

                // The way back. Waystone's charge got you here; this one gets you home.
                _homewardCharges++;
                Message($"Homeward charge earned — Keypad 9 to return to your bed. ({_homewardCharges} held)");

                if (boss.defeatKey == _finalBossKey) finished = true;
            }

            // A boss just fell — the next biome's challenges become drawable from here on.
            // Existing actives are untouched; only future draws and rerolls see the new ceiling.
            if (progressed) RefreshMaxTier();

            // ...and the saga moves to the next act. Safe to do here, AFTER the questline has had
            // its say: _challenges.Tick runs every frame while this poll runs once a second, so the
            // boss step's own completion (and its reward) has already fired many times over by the
            // time the key is read here. Do not move this into the per-frame path without
            // rechecking that — swapping the chain out from under an uncompleted final step would
            // silently eat the act's last reward.
            if (progressed) RefreshAct(announce: true);

            // ...and boons gated on progression become offerable. Resistances are the reason this
            // exists: frost resistance in the Meadows is a wasted pick out of only three options.
            if (progressed) RefreshBoonGate();

            if (finished) FinishRun();
        }

        private void PollMeasures(float pollDt)
        {
            var biomePlayer = Player.m_localPlayer;
            if (biomePlayer != null)
            {
                try { _visitedBiomes |= (int)biomePlayer.GetCurrentBiome(); } catch { }
            }

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
                // The questline's reserved slot is included for the same reason: it measures
                // through the very same reports, so anything it asks for has to be polled too.
                var wanted = MeasuredChallenges()
                    .SelectMany(CollectItemParams)
                    .Distinct();

                foreach (var itemName in wanted)
                {
                    _challenges.ReportMeasure(ChallengeKind.CollectItem, itemName, inventory.CountItems(itemName));
                }

                if (MeasuredChallenges().Any(HasCollectFood))
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
            PollBuiltPieces();
            PollReachedBiomes();
            PollDeerHerd();
            PollDiscoveries();
        }

        /// <summary>
        /// Locations this run has found — the latch behind <see cref="ChallengeKind.DiscoverLocation"/>.
        /// Latched for the same reason the built-piece set is: the poll can only see what is near
        /// the player right now, and a set that could shrink would un-find an altar the moment they
        /// walked away from it.
        /// </summary>
        private readonly HashSet<string> _discovered = new HashSet<string>();

        /// <summary>
        /// Completes discovery steps when the player reaches the place they name.
        ///
        /// Only looks for locations something is actually ASKING for, because
        /// <see cref="ZoneSystem.FindClosestLocation"/> is a search over generated locations and
        /// there is no reason to run five of them a second for objectives nobody holds.
        ///
        /// Re-reports everything already found, on the same reasoning as the build scanner: a step
        /// dealt AFTER the player walked past the altar must still complete, and the engine starts
        /// each chain step at zero.
        /// </summary>
        private void PollDiscoveries()
        {
            if (_challenges == null) return;

            var player = Player.m_localPlayer;
            var zone = ZoneSystem.instance;
            if (player == null || zone == null) return;

            Vector3 position = player.transform.position;
            float radius = Mathf.Max(1f, _cfg.RunDiscoverRadius);

            foreach (var wanted in WantedLocations())
            {
                if (_discovered.Contains(wanted)) continue;

                try
                {
                    if (!zone.FindClosestLocation(wanted, position, out ZoneSystem.LocationInstance loc)) continue;
                    if (Vector3.Distance(position, loc.m_position) > radius) continue;
                }
                catch { continue; }

                _discovered.Add(wanted);

                string display = Bosses.FirstOrDefault(b => b.locName == wanted).display ?? wanted;
                Message($"Found it — {display}'s altar.");
            }

            foreach (var found in _discovered)
                _challenges.ReportMeasure(ChallengeKind.DiscoverLocation, found, 1f);
        }

        /// <summary>Location names any live questline step is currently asking to be found.</summary>
        private IEnumerable<string> WantedLocations() =>
            _challenges.Tracks
                .Select(t => t.Current)
                .Where(c => c != null && c.Def.Kind == ChallengeKind.DiscoverLocation)
                .Select(c => c.Def.Param)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

        /// <summary>True while the run is in Act I — the only act the herd applies to.</summary>
        private bool ActIsMeadows => _actIndex == 0;

        /// <summary>Slow Burn's discount on heat GAINED. Losses are untouched — it slows the rise, not the fall.</summary>
        private const float SlowBurnGainMultiplier = 0.75f;

        /// <summary>
        /// Max health lent for every completion — questline step or random task alike.
        ///
        /// Owner, alpha34 play: "maybe we should reward the player with a tiny bit of armor and
        /// health for every quest/task completed since heat is also increased. The boons are the
        /// real increases, but each completion should grant SOMETHING." Heat is what a completion
        /// costs you, so health is what it should pay: enemies hit harder, you have more to spend.
        ///
        /// Armor is not part of it because armor is not reachable as a small increment — the game
        /// computes it from equipped items and its only damage-modifier steps are far too coarse.
        ///
        /// Act I is 15 questline steps plus whatever tasks get done, so Eikthyr is met around +40:
        /// real, and well short of a second health bar. Like every other power the mode grants, it
        /// is LOANED — see BoonEffects.SetTaskHealthReward and the loan ledger behind it.
        /// </summary>
        private const float HealthPerCompletion = 2f;

        /// <summary>Total health lent by completions so far this run. Persisted; given back at run end.</summary>
        private float _taskHealthReward;

        /// <summary>
        /// Unspent Homeward charges — one per boss felled, each a trip back to your claimed bed.
        ///
        /// Owner, alpha34 play: "when a boss is downed, the player should get 1 Gate to home (since
        /// thats where we built our house and crafting stuff)". Waystone already carries you TO the
        /// next altar on the same charge-per-boss rule; this is the return leg, which was the one
        /// pinch point left after the stash removed the cargo problem.
        ///
        /// Deliberately NOT a boon. A boon competes with 21 others for three offer slots, so most
        /// runs would not have it in the act where they most wanted it — and "the trip home is
        /// solved" has to be true every run or it is not solved at all.
        ///
        /// Charges accumulate, so skipping one boss's gate means two available later.
        /// </summary>
        private int _homewardCharges;

        /// <summary>
        /// Spends a Homeward charge, if there is one and there is somewhere to go.
        ///
        /// "Home" is the player's claimed bed — <c>PlayerProfile.GetCustomSpawnPoint</c>, which is
        /// exactly the "where we built our house and crafting stuff" the request meant, and is set
        /// by the very bed the Act I questline makes you build.
        ///
        /// With no bed claimed the charge is NOT spent and the player is told why. Dumping them at
        /// the world spawn instead would be a worse outcome than refusing: they would lose the
        /// charge and end up somewhere they never chose.
        /// </summary>
        private void TryHomeward()
        {
            if (!_active || _frozen) return;

            if (_homewardCharges <= 0)
            {
                Message("No Homeward charge — fell a boss to earn one.");
                return;
            }

            try
            {
                var profile = Game.instance?.GetPlayerProfile();
                if (profile == null || !profile.HaveCustomSpawnPoint())
                {
                    Message("Homeward has nowhere to go — claim a bed first.");
                    return;
                }

                var teleport = ModBootstrap.GetService<ITeleportService>();
                if (teleport == null) return;

                // Lifted clear of the ground for the same reason Waystone does it: arriving inside
                // the terrain is how a teleport turns into a death.
                teleport.TeleportTo(profile.GetCustomSpawnPoint() + Vector3.up * 2f);

                _homewardCharges--;
                Message($"Homeward. {_homewardCharges} charge{(_homewardCharges == 1 ? "" : "s")} left.");
                SaveState();
            }
            catch (Exception ex)
            {
                LogOnce("homeward", ex);
            }
        }

        /// <summary>
        /// Pays a completion's health, and re-applies the running total.
        ///
        /// Passes the TOTAL rather than the increment, because the loan ledger replaces a lender's
        /// contribution rather than adding to it — which is exactly what makes a growing loan safe
        /// to re-apply as often as we like without compounding.
        /// </summary>
        private void GrantCompletionHealth()
        {
            _taskHealthReward += HealthPerCompletion;
            ApplyTaskHealthReward();
        }

        private void ApplyTaskHealthReward()
        {
            try { _boonEffects.SetTaskHealthReward(_taskHealthReward); }
            catch (Exception ex) { LogOnce("task-health-reward", ex); }
        }

        /// <summary>
        /// Raises heat, after Slow Burn's discount, and tells everything that cares.
        ///
        /// Every heat change goes through here or <see cref="RemoveHeat"/> so the world modifiers
        /// and Forge-fed cannot fall out of step with the number — the same "one path, not several
        /// copies" correction that the stat-delta baselines needed in alpha33.
        /// </summary>
        private void AddHeat(float amount)
        {
            if (amount > 0f && HoldsBoon("slowburn")) amount *= SlowBurnGainMultiplier;

            _heat.Add(amount);
            OnHeatChanged();
        }

        private void RemoveHeat(float amount)
        {
            _heat.Remove(amount);
            OnHeatChanged();
        }

        /// <summary>
        /// Pushes the current heat into the world's difficulty keys and re-scales Forge-fed.
        ///
        /// Forge-fed is the only boon whose strength moves during a run, and this is the one place
        /// it can move from: heat changes on discrete events, never per frame, so re-applying here
        /// is both sufficient and cheap.
        /// </summary>
        private void OnHeatChanged()
        {
            _worldModifiers.ApplyHeat(_heat.Heat, _cfg);

            try { _boonEffects.RefreshForgeFed(_heat.Heat); }
            catch (Exception ex) { LogOnce("forgefed-refresh", ex); }
        }

        private bool HoldsBoon(string boonId) =>
            _boons != null && _boons.Held.Any(h => h.Def.Id == boonId);

        /// <summary>
        /// Stars nearby deer, and keeps the Herald standing while its step is the one in play.
        ///
        /// Act I only. Eikthyr's deer are his; starring every deer for the rest of the saga would
        /// turn a piece of Act I character into a permanent tax on hunting.
        /// </summary>
        private void PollDeerHerd()
        {
            if (_deer == null || !ActIsMeadows) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            _deer.UpgradeNearbyDeer(player.transform.position, DeerScanRadius);

            // The Herald exists only while its own step is current, and is re-spawned whenever it is
            // not standing — which is what makes it survive a logout, a zone unload, or a player who
            // wandered off and left it behind. Its ZDO is non-persistent, so "not standing" is the
            // normal state after any reload.
            // Asked of every track rather than just the first: the Herald step lives on HUNT, which
            // is track 0 today, but tying a content check to a track's position would break the
            // moment the tracks were reordered.
            bool heraldWanted = _challenges != null && _challenges.Tracks.Any(t =>
                t.Current != null &&
                t.Current.Def.Kind == ChallengeKind.KillPrefab &&
                t.Current.Def.Param == DeerHerd.HeraldKillName);

            if (heraldWanted && _deer.TrySpawnHerald(player))
            {
                string bearing = _deer.HeraldBearing(player);
                Message(bearing == null
                    ? $"{DeerHerd.HeraldName} is abroad in the meadows."
                    : $"{DeerHerd.HeraldName} is abroad — {bearing} of here.");
            }
        }

        /// <summary>
        /// How far the herd scan reaches. Wider than the build scan: deer are spread across open
        /// ground and a starred one should be starred before the player is close enough to shoot.
        /// </summary>
        private const float DeerScanRadius = 60f;

        /// <summary>
        /// Reports every biome this run has stood in, so <see cref="ChallengeKind.ReachBiome"/>
        /// steps complete.
        ///
        /// Reads the same <see cref="_visitedBiomes"/> mask the challenge filter uses, which is only
        /// ever OR-ed into — so arrival is permanent by construction, and leaving a biome cannot
        /// un-earn having reached it.
        ///
        /// Reports the whole mask every poll rather than only newly-entered biomes, for the reason
        /// the build scanner does the same: a step dealt AFTER the player was already there must
        /// still complete, and the engine deliberately starts each chain step at zero.
        /// </summary>
        private void PollReachedBiomes()
        {
            if (_challenges == null || _visitedBiomes == 0) return;

            foreach (var biome in ReportableBiomes)
            {
                if ((_visitedBiomes & (int)biome) == 0) continue;
                _challenges.ReportMeasure(ChallengeKind.ReachBiome, biome.ToString(), 1f);
            }
        }

        /// <summary>
        /// The biomes a ReachBiome challenge may name. Enumerated rather than derived from
        /// Enum.GetValues so that None (0) and the composite reads never appear as objectives.
        /// </summary>
        private static readonly Heightmap.Biome[] ReportableBiomes =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp,
            Heightmap.Biome.Mountain, Heightmap.Biome.Plains, Heightmap.Biome.Ocean,
            Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth,
        };

        /// <summary>
        /// What each <see cref="ChallengeKind.BuildPiece"/> category means, as a test against a
        /// placed piece.
        ///
        /// Every one of these is a COMPILED Valheim class, checked against the IL — which is the
        /// entire point. A category could just as easily have been a prefab name ("piece_workbench",
        /// "bed"), but prefab names are Unity asset data, invisible to this build, and a wrong one
        /// fails SILENTLY: the quest simply never completes and the questline stalls with no error
        /// anywhere. A wrong type name does not compile.
        ///
        /// GetComponentInChildren rather than GetComponent because a piece's behaviour is not
        /// guaranteed to sit on the same GameObject as its Piece component, and `true` includes
        /// inactive children — an unlit campfire is still a fire you built.
        /// </summary>
        private static readonly Dictionary<string, Func<Piece, bool>> PieceCategories =
            new Dictionary<string, Func<Piece, bool>>
            {
                ["Fire"] = p => p.GetComponentInChildren<Fireplace>(true) != null,
                ["Bed"] = p => p.GetComponentInChildren<Bed>(true) != null,
                ["Chest"] = p => p.GetComponentInChildren<Container>(true) != null,
                ["Door"] = p => p.GetComponentInChildren<Door>(true) != null,
                // The rack you put over a fire to cook on. CookingStation covers the plain one and
                // the iron cooking station both, which is the intent — the quest is "you can cook
                // now", not "you built one specific piece".
                ["Cooking"] = p => p.GetComponentInChildren<CookingStation>(true) != null,
                // Smelter also matches the charcoal kiln and the blast furnace — all three carry a
                // Smelter component and there is no separate class for them. "Build a smelter" is
                // therefore really "build something that smelts", which is close enough to the
                // intent and better than naming a prefab.
                ["Smelter"] = p => p.GetComponentInChildren<Smelter>(true) != null,
                ["Portal"] = p => p.GetComponentInChildren<Teleport>(true) != null,
                ["Fermenter"] = p => p.GetComponentInChildren<Fermenter>(true) != null,
                ["Windmill"] = p => p.GetComponentInChildren<Windmill>(true) != null,
                ["Ship"] = p => p.GetComponentInChildren<Ship>(true) != null,
            };

        /// <summary>
        /// Latches which categories of piece the player has built (see <see cref="_builtSeen"/>) and
        /// reports them to the challenge engine.
        ///
        /// Scans for every category the run has not yet seen, not merely the one the current quest
        /// step asks for, so that a player who built a chest an hour before the chest step came up
        /// is credited for the chest they already own rather than being told to build a second one.
        /// The scan stops entirely once all four have been seen, which in a finished house is most
        /// of the run.
        ///
        /// The latched set is re-reported EVERY poll, including when nothing new was found. The
        /// engine starts each chain step at zero on purpose (a report cannot be banked against a
        /// step that does not exist yet), so the re-report is what lets an already-satisfied step
        /// complete on the poll after it is dealt.
        ///
        /// IsCreator() — verified in the IL as m_creator == the local profile's player ID — is what
        /// makes this "you built it" rather than "you are standing near one": it excludes every
        /// world-generated ruin, and in a shared world it excludes other players' houses too.
        /// </summary>
        private void PollBuiltPieces()
        {
            if (_challenges == null) return;

            if (_builtSeen.Count < PieceCategories.Count) ScanForBuiltPieces();

            foreach (var category in _builtSeen)
                _challenges.ReportMeasure(ChallengeKind.BuildPiece, category, 1f);
        }

        /// <summary>
        /// One radius scan around the player, adding any newly recognised category to
        /// <see cref="_builtSeen"/>. Split out from <see cref="PollBuiltPieces"/> so the set is not
        /// mutated while it is being enumerated for reporting.
        /// </summary>
        private void ScanForBuiltPieces()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            _pieceBuffer.Clear();
            try
            {
                Piece.GetAllPiecesInRadius(
                    player.transform.position, _cfg.RunBuildScanRadius, _pieceBuffer);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Build-piece scan failed: {e.Message}");
                return;
            }

            foreach (var piece in _pieceBuffer)
            {
                // Everything found — the rest of a base's several hundred pieces are wasted work.
                if (_builtSeen.Count >= PieceCategories.Count) break;

                if (piece == null || !piece.IsCreator()) continue;

                foreach (var entry in PieceCategories)
                {
                    if (_builtSeen.Contains(entry.Key)) continue;
                    if (entry.Value(piece)) _builtSeen.Add(entry.Key);
                }
            }

            _pieceBuffer.Clear();
        }

        /// <summary>
        /// Everything whose progress is measured by polling: the three random slots plus the
        /// questline's reserved one. Deliberately NOT used for the no-armor bookkeeping below,
        /// which is keyed by challenge id and belongs to the random slots alone.
        /// </summary>
        private IEnumerable<ActiveChallenge> MeasuredChallenges()
        {
            foreach (var a in _challenges.Active) yield return a;

            foreach (var track in _challenges.Tracks)
                if (track.Current != null) yield return track.Current;
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

            // Each questline's own reserved slot, on exactly the same terms — a track holds its own
            // Baseline for the same reason a random slot does, and two tracks can be measuring the
            // same stat from different zero points, which is precisely why the report is
            // slot-addressed rather than param-scoped.
            var tracks = _challenges.Tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                var quest = tracks[i].Current;
                if (quest == null || quest.Def.Kind != ChallengeKind.StatDelta) continue;

                if (float.IsNaN(quest.Baseline))
                {
                    WarnUnbaselined(quest.Def.Id, tracks[i].Label);
                    continue;
                }

                float? questCurrent = ReadPlayerStat(quest.Def.Param);
                if (questCurrent == null) continue;

                _challenges.ReportSlotMeasure(
                    ChallengeEngine.TrackSlot(i), Mathf.Max(0f, questCurrent.Value - quest.Baseline));
            }
        }

        /// <summary>Steps seen un-baselined at a previous poll, and steps already warned about.</summary>
        private readonly HashSet<string> _unbaselinedSeen = new HashSet<string>();
        private readonly HashSet<string> _warnedUnbaselined = new HashSet<string>();

        /// <summary>
        /// Complains, once per step, about a StatDelta questline step that has no baseline.
        ///
        /// Such a step can never register progress: the poll above has nothing to measure from and
        /// skips it, silently, forever. That is exactly how alpha32 shipped with "Craft an axe" —
        /// the first step of the run — quietly doing nothing, because the baseline sync had not been
        /// updated when one questline became two.
        ///
        /// Warns only on the SECOND consecutive poll, a second apart. The baseline sync runs every
        /// playable frame but can legitimately no-op for a frame or two if the player profile is
        /// briefly unreachable, and an error blaming the mod for that would be worse than the
        /// silence it replaces. Two polls later is no longer transient.
        /// </summary>
        private void WarnUnbaselined(string stepId, string trackLabel)
        {
            if (string.IsNullOrEmpty(stepId)) return;
            if (_unbaselinedSeen.Add(stepId)) return;           // first sighting: could be transient
            if (!_warnedUnbaselined.Add(stepId)) return;        // already said so once

            Debug.LogError($"[ICanShowYouTheWorld] Questline step '{stepId}' on the {trackLabel} track has no " +
                           "stat baseline — it can never register progress. This is a bug in the mod, not the save.");
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
        /// <summary>
        /// Snapshots the zero point for every StatDelta challenge that does not have one yet.
        ///
        /// Goes through <see cref="MeasuredChallenges"/> — the SAME enumeration the polls use — and
        /// that sharing is the point. This method used to keep its own copy of "the actives plus the
        /// questline", and when one questline became two in alpha32 the copy was not updated: it
        /// synced only track 0, so every StatDelta step on the CRAFT track never got a baseline, and
        /// PollStatDeltas skips un-baselined slots. "Craft an axe" — the first step of the run —
        /// silently never registered, along with nine other steps across the saga.
        ///
        /// One enumeration means the baseline sync and the polls cannot disagree about what exists.
        /// </summary>
        private void SyncStatDeltaBaselines()
        {
            if (_challenges == null) return;

            foreach (var a in MeasuredChallenges()) SyncStatDeltaBaseline(a);
        }

        private void SyncStatDeltaBaseline(ActiveChallenge a)
        {
            if (a == null || a.Def.Kind != ChallengeKind.StatDelta || !float.IsNaN(a.Baseline)) return;

            float? current = ReadPlayerStat(a.Def.Param);
            if (current == null) return;

            a.Baseline = current.Value;
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
            RestoreLoanedSkills();  // before EndRun: the snapshots are run state EndRun clears
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

            // Run state, so it goes with the run. The paths that END a run call RestoreLoanedSkills
            // first; SuspendRun deliberately does not — that run is still live, the character keeps
            // the loaned levels, and the originals ride the save file back in on resume.
            _skillLoans.Clear();

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
        /// Checks the act table's own invariants at run start.
        ///
        /// ActDefinitionTests covers these rules, but only against a stand-in table — the real one
        /// lives here, in game-coupled code the unit harness deliberately excludes. So the rules are
        /// asserted against the REAL table at runtime, where a content edit that breaks one shows up
        /// on the next launch instead of in a run hours later.
        /// </summary>
        private void ValidateActs()
        {
            // Duplicate step ids are the dangerous one, and with tracks the hazard is now
            // two-dimensional. RestoreTrack resolves a saved position by id against ONE track's
            // chain, so an id appearing twice anywhere lets a resume seat the wrong step — and a
            // step is complete the moment Progress >= Target, so that can fire an unearned
            // completion, rewards and all, on the first tick.
            var ids = _acts.SelectMany(a => a.AllSteps).Select(c => c.Id).ToList();
            var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            if (duplicates.Count > 0)
                Debug.LogError("[ICanShowYouTheWorld] Duplicate questline step ids across acts/tracks — a " +
                               $"resume can seat the wrong step: {string.Join(", ", duplicates.ToArray())}");

            foreach (var act in _acts)
            {
                if (!Bosses.Any(b => b.defeatKey == act.BossDefeatKey))
                    Debug.LogError($"[ICanShowYouTheWorld] {act.Label} names a boss key no boss has: '{act.BossDefeatKey}'.");

                // The BOSS lives on the hunt track, and its last step being the boss is what makes
                // "the act is over" observable. A craft track may legitimately be short or even
                // empty; a hunt track that does not end on a kill cannot end the act.
                var hunt = act.Tracks.FirstOrDefault(t => t.Id == HuntTrackId);
                if (hunt == null || hunt.Chain.Count == 0)
                {
                    Debug.LogError($"[ICanShowYouTheWorld] {act.Label} has no hunt track — its boss is unreachable.");
                    continue;
                }

                var last = hunt.Chain.LastOrDefault();
                if (last == null || last.Kind != ChallengeKind.KillPrefab)
                    Debug.LogError($"[ICanShowYouTheWorld] {act.Label}'s hunt track does not end on its boss " +
                                   $"(last step '{last?.Id ?? "none"}').");
            }

            // A build category may appear in ONE act only. _builtSeen latches for the whole run, so
            // a category an earlier act already satisfied is reported as built from the moment a
            // later act's step is dealt — the step completes instantly and hands over its reward for
            // nothing. This is invisible in review and obvious in play, which is the worst
            // combination, so it is checked here.
            var categoryOwners = new Dictionary<string, string>();
            foreach (var act in _acts)
            {
                foreach (var category in act.AllSteps
                             .Where(c => c.Kind == ChallengeKind.BuildPiece && !string.IsNullOrEmpty(c.Param))
                             .Select(c => c.Param)
                             .Distinct())
                {
                    if (categoryOwners.TryGetValue(category, out var owner))
                        Debug.LogError($"[ICanShowYouTheWorld] Build category '{category}' is used by both " +
                                       $"{owner} and {act.Label} — the run-long latch will auto-complete the later one.");
                    else
                        categoryOwners[category] = act.Label;
                }
            }
        }

        /// <summary>
        /// Which act the run is in: the number of bosses the WORLD records as dead, clamped to the
        /// last act.
        ///
        /// Derived rather than stored, deliberately. It cannot drift from the world, a resume
        /// recomputes it instead of trusting a save, and a run started on a world that already
        /// killed Eikthyr correctly begins in Act II rather than replaying the Meadows. It is the
        /// same reading <see cref="RefreshMaxTier"/> already takes, for the same reasons.
        ///
        /// Returns the current index unchanged when the world is not loaded — a momentary null
        /// ZoneSystem must not read as "no bosses dead" and throw the run back to Act I.
        /// </summary>
        /// <summary>
        /// Tells the boon engine how far the world has got, for <see cref="BoonDefinition.MinBosses"/>.
        ///
        /// Derived from the world's own keys rather than stored, exactly as the act index is, so a
        /// resume and a run started on an already-progressed world both gate correctly without any
        /// new save state.
        /// </summary>
        private void RefreshBoonGate()
        {
            if (_boons == null) return;

            var zone = ZoneSystem.instance;
            if (zone == null) return;

            _boons.DefeatedBosses = Bosses.Count(b => SafeGetGlobalKey(zone, b.defeatKey));
        }

        private int CurrentActIndex()
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return _actIndex;

            int defeated = Bosses.Count(b => SafeGetGlobalKey(zone, b.defeatKey));
            return Mathf.Clamp(defeated, 0, _acts.Count - 1);
        }

        /// <summary>
        /// Seats the act the world says the run is in, swapping the questline chain if it changed.
        ///
        /// <paramref name="announce"/> distinguishes a TRANSITION from a restore: crossing into an
        /// act mid-run is an event worth a banner, whereas starting or resuming into one is just
        /// where the run already was, and announcing that on every resume would be noise.
        /// </summary>
        private void RefreshAct(bool announce)
        {
            if (_challenges == null) return;

            int next = CurrentActIndex();
            bool changed = next != _actIndex;
            _actIndex = next;

            // Re-seat even when the index is unchanged if nothing is seated yet: this is also the
            // path that installs the act's tracks at run start.
            if (!changed && _challenges.Tracks.Count > 0) return;

            _challenges.SetTracks(_acts[_actIndex].Tracks);

            if (!announce || !changed) return;

            string banner = _acts[_actIndex].Banner;
            Announce(banner);
            Message(banner);
            Debug.Log($"[ICanShowYouTheWorld] Act transition → {_acts[_actIndex].Label}");
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
            // Two gates on what may be DEALT, both asking the same kind of question: has the run
            // actually reached the thing this task is about? Biomes covers "have you been there";
            // RequiresBuilt covers "do you own one of these" — a door task dealt to a player with
            // no door is a dead slot they have to pay heat to reroll.
            _challenges.ExternalFilter = d =>
                (d.Biomes == 0 || (d.Biomes & _visitedBiomes) != 0) &&
                (string.IsNullOrEmpty(d.RequiresBuilt) || _builtSeen.Contains(d.RequiresBuilt));
            _challenges.Completed += OnChallengeCompleted;

            // The questline is installed for a fresh run and a resume alike; only its POSITION
            // differs, and a resume sets that with RestoreMainQuest right after this. The chain
            // is never pool-filtered (see SetMainChain), so the kill-hook trimming applied to
            // `pool` doesn't reach it — a run without the death hook would have an unfinishable
            // questline, which BuildChallengePool already warns about for the same reason.
            // Seats whichever act the world says this run is in — Act I on a fresh world, a later
            // one on a world whose bosses are already down. No banner: starting or resuming into an
            // act is not a transition.
            RefreshAct(announce: false);

            _boons = new BoonEngine(DefaultBoons(), _rng, _cfg.RunBoonOfferTimeoutSeconds);
            if (freshRun) _boons.FirstOfferPin = FirstBoonPin;
            _boons.Gained += OnBoonGained;
            _boons.Lost += OnBoonLost;
            RefreshBoonGate();

            // Every act's chain, not just the current one: Act V's creature names are worth knowing
            // about during Act I, when there is still time to fix them.
            ValidateAssetNames(pool.Concat(AllActChains()));
        }

        /// <summary>
        /// Checks every asset name the run's definitions depend on against the game's own
        /// registries, and logs the ones that do not resolve.
        ///
        /// This is the answer to the mode's most expensive class of bug. Asset names are Unity data,
        /// invisible to the compiled assembly, and a wrong one does not throw: a bad CREATURE name
        /// means a kill quest whose counter never moves, a bad ITEM name a collect sub that is dead
        /// for the run, and a bad RequiresBuilt category a task that is never dealt at all. Every one
        /// of them looks exactly like ordinary bad luck. Until now the only detector was playing
        /// until something felt stuck, which cost several builds.
        ///
        /// Runs once per run start and reports at most one line per bucket. It is diagnostics only —
        /// nothing is disabled on a miss, because a definition that fails here fails the same way it
        /// always did, and taking the run away from the player over it would be a worse outcome than
        /// one dud task. <see cref="NameManifest"/> does the pure half and is unit-tested; this half
        /// needs the live game.
        /// </summary>
        private void ValidateAssetNames(IEnumerable<ChallengeDefinition> definitions)
        {
            try
            {
                var manifest = NameManifest.Collect(definitions);
                var scene = ZNetScene.instance;
                var odb = ObjectDB.instance;

                // Creatures live in ZNetScene, not ObjectDB. The Character test is what makes this
                // meaningful: ZNetScene holds every networked prefab, so a name that resolves to a
                // rock would otherwise pass a bare existence check and still never register a kill.
                if (scene != null)
                {
                    var missing = manifest.CreaturePrefabs
                        .Where(n => !SyntheticCreatureNames.Contains(n))
                        .Where(n => { var p = scene.GetPrefab(n); return p == null || p.GetComponent<Character>() == null; })
                        .ToList();

                    if (missing.Count > 0)
                        Debug.LogError("[ICanShowYouTheWorld] Unknown CREATURE names — their kill quests can " +
                                       $"never progress: {string.Join(", ", missing.ToArray())}");
                }

                // CollectItem matches Inventory.CountItems, which compares against m_shared.m_name —
                // the "$item_" token, not the prefab name. Both are accepted here so a definition
                // written either way validates; only a name that is neither is a real typo.
                if (odb != null && odb.m_items != null)
                {
                    var known = new HashSet<string>();
                    foreach (var go in odb.m_items)
                    {
                        if (go == null) continue;
                        known.Add(go.name);

                        var drop = go.GetComponent<ItemDrop>();
                        if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                            known.Add(drop.m_itemData.m_shared.m_name);
                    }

                    var missing = manifest.ItemNames.Where(n => !known.Contains(n)).ToList();
                    if (missing.Count > 0)
                        Debug.LogError("[ICanShowYouTheWorld] Unknown ITEM names — their collect quests can " +
                                       $"never progress: {string.Join(", ", missing.ToArray())}");
                }

                // Not a Valheim name at all — a typo in OUR vocabulary, checked against the same
                // table the scanner uses. Quiet failure otherwise: the task is simply never dealt.
                var badCategories = manifest.PieceCategories
                    .Where(c => !PieceCategories.ContainsKey(c))
                    .ToList();

                if (badCategories.Count > 0)
                    Debug.LogError("[ICanShowYouTheWorld] Unknown BUILD categories — these quests can never " +
                                   $"be dealt or completed: {string.Join(", ", badCategories.ToArray())}");

                // Also our own vocabulary. A ReachBiome step opens an act, so a typo here stalls the
                // act at its very first beat — with the player standing in the biome, demonstrably
                // having arrived, and nothing happening.
                var badBiomes = manifest.Biomes
                    .Where(b => !Enum.TryParse<Heightmap.Biome>(b, out _))
                    .ToList();

                if (badBiomes.Count > 0)
                    Debug.LogError("[ICanShowYouTheWorld] Unknown BIOME names — their arrival steps can never " +
                                   $"complete: {string.Join(", ", badBiomes.ToArray())}");

                // Location names cannot be resolved against the game: ZoneSystem's lookup returns
                // false both for "no such location" and for "you are simply far away", so a typo is
                // indistinguishable from a long walk. Checking them against the boss table they come
                // from is the only honest verification available — and it does catch the mistake
                // that actually happens, which is using a boss's CREATURE name where its LOCATION
                // name belongs ("Eikthyr" vs "Eikthyrnir", "gd_king" vs "GDKing").
                var badLocations = manifest.Locations
                    .Where(l => !Bosses.Any(b => b.locName == l))
                    .ToList();

                if (badLocations.Count > 0)
                    Debug.LogError("[ICanShowYouTheWorld] Unknown LOCATION names — their discovery steps can " +
                                   $"never complete: {string.Join(", ", badLocations.ToArray())}");

                // Reward prefabs already log at grant time, but that only fires when someone
                // actually reaches the step — which for the later boss spoils is hours in, if ever.
                // Checking them up front turns "find out when you beat Moder" into "find out now".
                if (scene != null || odb != null)
                {
                    var rewards = QuestRewards.Values
                        .Concat(BossSpoils)
                        .SelectMany(entries => entries)
                        .Select(entry => entry.prefab)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Distinct()
                        .Where(name => ResolveItemPrefab(name) == null)
                        .ToList();

                    if (rewards.Count > 0)
                        Debug.LogError("[ICanShowYouTheWorld] Unknown REWARD prefabs — those rewards will not " +
                                       $"be granted: {string.Join(", ", rewards.ToArray())}");
                }

                if (scene == null || odb == null)
                    Debug.LogWarning("[ICanShowYouTheWorld] Asset-name validation ran before the game's " +
                                     "registries were ready; some names were not checked this run.");

                ValidateActs();
            }
            catch (Exception e)
            {
                // Diagnostics must never be the thing that breaks a run.
                Debug.LogWarning($"[ICanShowYouTheWorld] Asset-name validation failed: {e.Message}");
            }
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
                // The questline and the random tasks pay differently, and that separation IS the
                // design: a task hands you a boon offer, a quest step hands you gear. Offering a
                // boon here as well would make the questline strictly better than everything else
                // and drown the player in choices at the same moment they gain four items.
                // Every completion pays health, whichever kind it was. Heat is what finishing
                // something COSTS you — the world gets harder — so something has to come back, and
                // this is the part that does regardless of which track or table you were on.
                GrantCompletionHealth();

                if (def.MainQuest)
                {
                    AddHeat(MainQuestHeatReward);

                    GrantQuestReward(def);
                    SaveState();   // the chain has advanced; don't wait for the autosave
                    return;
                }

                AddHeat(def.HeatReward);
                _boons?.CreateOffer();

                Message($"Challenge complete: {def.Display}  " +
                        $"(+{def.HeatReward:0.#} heat, +{HealthPerCompletion:0.#} health)");
            }
            catch (Exception ex)
            {
                LogOnce("challenge-complete", ex);
            }
        }

        // --- Questline rewards ---

        /// <summary>
        /// Hands over everything <see cref="QuestRewards"/> lists for a finished questline step
        /// and announces it. A step with no entry (or an empty one) simply pays nothing but heat —
        /// the table is the single source of truth for what a step gives.
        /// </summary>
        private void GrantQuestReward(ChallengeDefinition def)
        {
            GrantQuestSkills(def);

            if (def.Id == null || !QuestRewards.TryGetValue(def.Id, out var items) || items == null) return;

            foreach (var (prefabName, count) in items) GrantItem(prefabName, count);

            Message(string.IsNullOrEmpty(def.RewardText)
                ? $"Quest complete: {def.Display}"
                : $"Quest reward: {def.RewardText}");
        }

        /// <summary>
        /// Skill a questline step pays out, alongside its items. Handing someone a bow while their
        /// Bows skill sits at 1 hands them a bow that misses — the gear is only a reward if the
        /// character can use it, so the axe step lifts the three skills its own rewards depend on.
        ///
        /// These are LOANS like every other skill this mode grants (see <see cref="LoanSkill"/>):
        /// raised for the run, snapshotted first, given back at the end. WoodCutting is in the list
        /// for completeness even though the run already loans it at the cap — LoanSkill only ever
        /// raises, so the lower grant is a no-op rather than a demotion.
        /// </summary>
        private static readonly Dictionary<string, (Skills.SkillType skill, float level)[]> QuestSkillRewards =
            new Dictionary<string, (Skills.SkillType, float)[]>
            {
                ["mq-axe"] = new[]
                {
                    (Skills.SkillType.WoodCutting, QuestSkillGrantLevel),
                    (Skills.SkillType.Axes, QuestSkillGrantLevel),
                    (Skills.SkillType.Bows, QuestSkillGrantLevel),
                },
            };

        /// <summary>Owner's number: enough that the gear works, far from the 100 the cap allows.</summary>
        private const float QuestSkillGrantLevel = 25f;

        /// <summary>
        /// Food for the tier just cleared, handed over every time a boss falls.
        ///
        /// This began as timber and stone (owner, alpha17: "award the player a house after each
        /// boss kill") and became food in alpha25, once the resource rate actually worked: at a
        /// genuine 3x, wood is no longer the thing a run is short of. Food is — the next biome's
        /// health and stamina pools come out of a cookpot, and stopping to farm and cook them is
        /// the same fetch-quest tax the questline exists to skip.
        ///
        /// Indexed by bosses felled, so the meal matches where the run is going next rather than
        /// where it has been. Names are Unity asset data and cannot be checked against the
        /// assembly; a wrong one logs loudly in GrantItem and grants nothing.
        /// </summary>
        private static readonly (string prefab, int count)[][] BossSpoils =
        {
            // Index 0 is unused — a kill always means at least one boss down.
            new[] { ("CookedMeat", 10) },
            new[] { ("CookedMeat", 10), ("Honey", 10) },              // after Eikthyr -> Black Forest
            new[] { ("Sausages", 10), ("CarrotSoup", 5) },            // after the Elder -> Swamp
            new[] { ("TurnipStew", 5), ("SerpentStew", 3) },          // after Bonemass -> Mountain
            new[] { ("WolfMeatSkewer", 10), ("OnionSoup", 5) },       // after Moder -> Plains
            new[] { ("LoxMeatPie", 5), ("BloodPudding", 10) },        // after Yagluth
        };

        private void GrantBossSpoils()
        {
            int defeated = DefeatedBossCount();
            if (defeated <= 0) return;

            var spoils = BossSpoils[Mathf.Min(defeated, BossSpoils.Length - 1)];
            foreach (var (prefab, count) in spoils) GrantItem(prefab, count);

            Message("Spoils: provisions for the road ahead.");
        }

        /// <summary>
        /// Refills Waystone when a boss falls — its only source of charges now that a held boon is
        /// never offered twice. It is the one active whose use is counted rather than cooled down,
        /// and a boss kill is exactly when its next destination changes, since the stone always
        /// points at the nearest altar still standing.
        /// </summary>
        private void RechargeWaystone()
        {
            var held = _boons?.Held?.FirstOrDefault(h => h.Def.Id == "way");
            if (held == null) return;

            held.Charges++;
            Message("The way opens again.");
        }

        private void GrantQuestSkills(ChallengeDefinition def)
        {
            if (def.Id == null || !QuestSkillRewards.TryGetValue(def.Id, out var skills) || skills == null) return;

            foreach (var (skill, level) in skills) LoanSkill(skill, level);
        }

        /// <summary>
        /// Puts <paramref name="count"/> of an item into the player's inventory, falling back to
        /// dropping it at their feet when that can't be done — a full pack must never silently
        /// eat a questline reward, which is the only copy the run will ever hand out.
        ///
        /// Uses the name-based Inventory.AddItem overload the game's own console "spawn" command
        /// uses (verified against assembly_valheim's IL:
        /// <c>ItemDrop.ItemData AddItem(string name, int stack, int quality, int variant,
        /// long crafterID, string crafterName, bool pickedUp = false)</c>). It returns null both
        /// for an unknown prefab name and for "no room", hence the fallback on null.
        ///
        /// Quality and variant are copied off the prefab's own ItemData rather than hardcoded to
        /// 1/0, exactly as the console path does — an item whose base quality isn't 1 would
        /// otherwise be handed over subtly wrong.
        /// </summary>
        private void GrantItem(string prefabName, int count) => GrantItem(prefabName, count, -1, -1);

        /// <summary>
        /// As <see cref="GrantItem(string,int)"/>, but with an explicit quality and variant.
        ///
        /// Pass -1 for either to take the prefab's own default, which is what a quest reward wants.
        /// The stash passes real values, because quality and variant are part of an item's identity
        /// there: withdrawing a level-3 axe must not hand back a level-1 one.
        /// </summary>
        private void GrantItem(string prefabName, int count, int qualityOverride, int variantOverride)
        {
            if (string.IsNullOrEmpty(prefabName) || count <= 0) return;

            try
            {
                var prefab = ResolveItemPrefab(prefabName);
                if (prefab == null)
                {
                    Debug.LogError($"[ICanShowYouTheWorld] Quest reward prefab '{prefabName}' not found — " +
                                   "that reward was not granted.");
                    return;
                }

                var itemDrop = prefab.GetComponent<ItemDrop>();
                int quality = qualityOverride >= 0
                    ? qualityOverride
                    : itemDrop != null && itemDrop.m_itemData != null ? itemDrop.m_itemData.m_quality : 1;
                int variant = variantOverride >= 0
                    ? variantOverride
                    : itemDrop != null && itemDrop.m_itemData != null ? itemDrop.m_itemData.m_variant : 0;

                var player = Player.m_localPlayer;
                var inventory = player == null ? null : player.GetInventory();

                if (inventory != null &&
                    inventory.AddItem(prefabName, count, quality, variant, 0L, string.Empty, true) != null)
                {
                    Debug.Log($"[ICanShowYouTheWorld] Quest reward: {count}x {prefabName} added to inventory.");
                    return;
                }

                if (DropAtPlayerFeet(prefab, count))
                {
                    Debug.Log($"[ICanShowYouTheWorld] Quest reward: {count}x {prefabName} dropped at the " +
                              "player's feet (inventory full or unavailable).");
                    return;
                }

                Debug.LogError($"[ICanShowYouTheWorld] Quest reward {count}x {prefabName} could not be granted " +
                               "by either route.");
            }
            catch (Exception ex)
            {
                LogOnce("grant-item", ex);
            }
        }

        /// <summary>
        /// The prefab for an item name. ObjectDB is the item registry Inventory.AddItem itself
        /// looks in, so it is asked first; ZNetScene is the fallback the rest of this codebase
        /// already uses for prefab lookups (see SpawnService) and is what the world-drop path
        /// needs anyway. Unity's overloaded == is used deliberately — a destroyed object must read
        /// as missing here, not as a live prefab.
        /// </summary>
        private static GameObject ResolveItemPrefab(string prefabName)
        {
            var odb = ObjectDB.instance;
            if (odb != null)
            {
                var fromOdb = odb.GetItemPrefab(prefabName);
                if (fromOdb != null) return fromOdb;
            }

            var scene = ZNetScene.instance;
            if (scene != null)
            {
                var fromScene = scene.GetPrefab(prefabName);
                if (fromScene != null) return fromScene;
            }

            return null;
        }

        /// <summary>
        /// Drops an item stack on the ground beside the player, mirroring the game's own drop
        /// routine (Instantiate → ItemDrop.SetStack → ItemDrop.OnCreateNew, all confirmed against
        /// the IL). SetStack clamps to the item's max stack size and saves the ZDO itself, and
        /// OnCreateNew stamps the world level — skipping either produces an item that looks right
        /// and desyncs the moment it is picked up.
        /// </summary>
        private static bool DropAtPlayerFeet(GameObject prefab, int count)
        {
            var player = Player.m_localPlayer;
            if (player == null || prefab == null) return false;

            var position = player.transform.position + player.transform.forward * 0.6f + Vector3.up * 0.4f;
            var spawned = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            if (spawned == null) return false;

            var drop = spawned.GetComponent<ItemDrop>();
            if (drop != null)
            {
                drop.SetStack(count);
                ItemDrop.OnCreateNew(drop);
            }
            return true;
        }

        // --- Loaned skills ---

        /// <summary>A skill this run has raised: what it was worth before, and what it was raised to.</summary>
        private struct SkillLoan
        {
            public float Original;
            public float Level;
        }

        /// <summary>
        /// Every skill this run has loaned the player, by type. Persisted, because a run that
        /// crosses a reload must still be able to give each loan back.
        ///
        /// A table rather than the single WoodCutting field it grew out of: the questline now pays
        /// in skill as well as in gear (the axe step lifts Axes and Bows out of the level-1 range
        /// where a bow is useless), and every one of those grants has to be given back at run end
        /// on the same terms.
        /// </summary>
        private readonly Dictionary<Skills.SkillType, SkillLoan> _skillLoans =
            new Dictionary<Skills.SkillType, SkillLoan>();

        /// <summary>
        /// Raises a skill for the duration of the run and remembers what it was worth first.
        ///
        /// "Max woodcutting from the start" (owner's design) is the original case: chopping is the
        /// one skill an early Valheim run is forced to grind, and grinding is exactly what Run Mode
        /// is not for. The level is LOANED, not given — the pre-run value is snapshotted here and
        /// written back when the run ends, so a run leaves the character's own progression where it
        /// found it.
        ///
        /// The snapshot is taken at most once per skill, and only ever raises: a second call at a
        /// LOWER level (the questline granting 25 in a skill already loaned 100) leaves both the
        /// snapshot and the level alone. Re-reading the level after a boost would capture the
        /// loaned value as the "original" and make the loan permanent, which is why the respawn and
        /// resume paths call <see cref="ReapplyLoanedSkills"/> instead.
        /// </summary>
        private void LoanSkill(Skills.SkillType type, float level)
        {
            try
            {
                var skill = SkillObject(type);
                if (skill == null) return;

                if (!_skillLoans.TryGetValue(type, out var loan))
                {
                    // Nothing to lend someone who is already better than the grant — and recording
                    // a loan here would be actively harmful, since run end writes the snapshot back
                    // and would confiscate whatever they went on to earn.
                    if (skill.m_level >= level) return;

                    loan = new SkillLoan { Original = skill.m_level, Level = 0f };
                }

                if (level <= loan.Level) return;   // already loaned at least this much

                loan.Level = level;
                _skillLoans[type] = loan;
                if (skill.m_level < level) skill.m_level = level;

                Debug.Log($"[ICanShowYouTheWorld] {type} loaned at {level:0} " +
                          $"(original {loan.Original:0.##}).");
            }
            catch (Exception ex)
            {
                LogOnce("skill-loan", ex);
            }
        }

        /// <summary>
        /// Re-applies every loan without touching its snapshot. Needed after a respawn
        /// (Skills.OnDeath multiplies every level down — vanilla behaviour this mode accepts rather
        /// than suppresses) and after a resume, where the originals come from the save file instead.
        /// </summary>
        private void ReapplyLoanedSkills()
        {
            try
            {
                foreach (var entry in _skillLoans)
                {
                    var skill = SkillObject(entry.Key);
                    if (skill != null && skill.m_level < entry.Value.Level)
                        skill.m_level = entry.Value.Level;
                }
            }
            catch (Exception ex)
            {
                LogOnce("skill-reapply", ex);
            }
        }

        /// <summary>
        /// Takes back what the run LENT, and nothing more. Called from the paths that END a run for
        /// good; deliberately NOT from <see cref="SuspendRun"/>, where the run is still live and its
        /// save still carries the originals.
        ///
        /// Subtracting the loan rather than assigning the snapshot back is the whole point. A skill
        /// loaned BELOW the cap can still be trained during the run — Axes and Bows are granted at
        /// 25 and a run gains skill at 3x — and writing the pre-run level back would confiscate
        /// every level the player actually earned, leaving them worse off than if Run Mode had never
        /// touched the skill. (WoodCutting never showed this: it was loaned at the game's cap of
        /// 100, so there was no room above the loan to earn anything.) Taking the loan's own delta
        /// off the CURRENT level gives back exactly the head start, keeps the climb, and can never
        /// drop the player below where they started.
        /// </summary>
        private void RestoreLoanedSkills()
        {
            try
            {
                foreach (var entry in _skillLoans)
                {
                    var skill = SkillObject(entry.Key);
                    if (skill == null) continue;

                    float lent = entry.Value.Level - entry.Value.Original;
                    float restored = Math.Max(entry.Value.Original, skill.m_level - lent);

                    skill.m_level = restored;
                    Debug.Log($"[ICanShowYouTheWorld] {entry.Key} restored to {restored:0.##} " +
                              $"(was {entry.Value.Original:0.##} before the run, lent {lent:0.##}).");
                }
            }
            catch (Exception ex)
            {
                LogOnce("skill-restore", ex);
            }
            finally
            {
                // Cleared even if the writes threw: a retained loan would be re-applied by the next
                // run's respawn path and hand out a level this run never snapshotted.
                _skillLoans.Clear();
            }
        }

        /// <summary>
        /// The live <see cref="Skills.Skill"/> object for a skill type, or null if it can't be reached.
        ///
        /// Getting at it is fiddlier than it looks, and every step below is forced by the IL:
        /// Skills.GetSkill(SkillType) is PRIVATE, and GetSkillList() only returns skills the player
        /// already has an entry for — a character that has never swung an axe has none. Calling
        /// the public GetSkillLevel first is what creates that entry (it goes through GetSkill),
        /// so the list lookup afterwards always finds it.
        ///
        /// GetSkillLevel's own return value is deliberately discarded: it applies status-effect
        /// modifiers and floors the result, so it is the EFFECTIVE level, not the stored one —
        /// snapshotting it would give back a wrong number at the end of the run.
        ///
        /// Writing m_level directly is also deliberate, in preference to the public
        /// CheatRaiseSkill: that one re-balances every OTHER skill downward when a world has the
        /// skill cap enabled, which would quietly damage the character's real progression.
        /// </summary>
        private static Skills.Skill SkillObject(Skills.SkillType type)
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;

            var skills = player.GetSkills();
            if (skills == null) return null;

            skills.GetSkillLevel(type);   // forces the entry to exist

            foreach (var skill in skills.GetSkillList())
            {
                if (skill != null && skill.m_info != null && skill.m_info.m_skill == type)
                    return skill;
            }
            return null;
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
                    RemoveHeat(_cfg.RunDeathHeatPenalty);

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

                // The herd answers separately, and may hand back a synthetic name — the Herald's,
                // which is matched by identity rather than by prefab so ordinary deer cannot
                // complete its step. Reported IN ADDITION to the ordinary kill above: killing the
                // Herald is also killing a deer, and should count for both.
                // On-kill boons see every non-player, non-tamed death, in every act.
                try { _boonEffects.OnKill(); }
                catch (Exception ex) { LogOnce("boon-on-kill", ex); }

                if (_deer != null && ActIsMeadows)
                {
                    string synthetic = _deer.OnCharacterDied(c);
                    if (synthetic != null)
                    {
                        _challenges?.ReportKill(synthetic);
                        Message($"{DeerHerd.HeraldName} falls.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogOnce("character-died", ex);
            }
        }

        // --- Stash ---

        public IReadOnlyList<StashEntry> StashEntries => _active ? _stash.Entries : null;

        /// <summary>
        /// Moves every unequipped MATERIAL out of the player's inventory and into the stash, and
        /// returns how many items moved.
        ///
        /// Materials only — ItemType.Material is a compiled enum, so no asset names are involved,
        /// and it is the honest reading of "resources". Food, arrows, tools and gear stay put: a
        /// button that emptied your quiver and your dinner into a box you cannot reach in a fight
        /// would be a trap, however consistent.
        ///
        /// Equipped items are skipped outright. Pulling something out from under the equip state
        /// is a class of bug worth not having, and nobody equips a material anyway.
        ///
        /// The item list is SNAPSHOTTED before anything is removed — mutating the inventory while
        /// walking its own list is the same hazard Windfall's doubling has, in reverse.
        /// </summary>
        public int DepositMaterials()
        {
            if (!_active) return 0;

            var player = Player.m_localPlayer;
            var inventory = player == null ? null : player.GetInventory();
            if (inventory == null) return 0;

            var candidates = inventory.GetAllItems()
                .Where(i => i != null && i.m_shared != null && !i.m_equipped && i.m_stack > 0)
                .Where(i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material)
                .Where(i => i.m_dropPrefab != null)
                .ToList();

            int moved = 0;
            foreach (var item in candidates)
            {
                // Deposit FIRST: if the stash refuses (it is full and this is a new kind), the item
                // must stay in the inventory rather than being removed into nothing.
                int taken = _stash.Deposit(item.m_dropPrefab.name, item.m_stack, item.m_quality, item.m_variant);
                if (taken <= 0) continue;

                inventory.RemoveItem(item);
                moved += taken;
            }

            if (moved > 0)
            {
                Message($"Stashed {moved} items.");
                SaveState();
            }
            else if (candidates.Count > 0)
            {
                Message("Stash is full.");
            }

            return moved;
        }

        /// <summary>
        /// Takes everything of one stashed kind back into the player's hands. Overflow drops at
        /// their feet, via the same path a quest reward uses.
        ///
        /// The entry is removed from the stash only after the grant is attempted, so a failure to
        /// resolve the prefab cannot quietly delete the contents.
        /// </summary>
        public void WithdrawStash(int index)
        {
            if (!_active) return;

            var entries = _stash.Entries;
            if (index < 0 || index >= entries.Count) return;

            var entry = entries[index];
            string prefab = entry.Prefab;
            int count = entry.Count;
            int quality = entry.Quality;
            int variant = entry.Variant;

            if (ResolveItemPrefab(prefab) == null)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Stashed prefab '{prefab}' no longer resolves — " +
                               "left in the stash rather than destroyed.");
                Message($"Cannot withdraw {prefab}.");
                return;
            }

            GrantItem(prefab, count, quality, variant);
            _stash.WithdrawAll(index);
            SaveState();
        }

        /// <summary>
        /// Puts every questline back where it was.
        ///
        /// A save written since alpha32 carries a position per track and each is restored by id, as
        /// before. An OLDER save carries one <c>mainQuestId</c> from when there was one questline —
        /// and that id could belong to either track now, since the split cut the old chain in two.
        /// So it is looked up across every track's chain and seats whichever one owns it, leaving
        /// the other at its start.
        ///
        /// Restoring the other track to zero rather than guessing is the conservative direction: a
        /// player repeats a step at worst. Guessing a position would risk seating a step whose
        /// target is already met, which fires an unearned completion — rewards and all — on the
        /// first tick.
        /// </summary>
        private void RestoreQuestTracks(RunSaveState s)
        {
            if (s.trackIds != null && s.trackIds.Count > 0)
            {
                for (int i = 0; i < s.trackIds.Count; i++)
                {
                    _challenges.RestoreTrack(
                        s.trackIds[i],
                        s.trackIndices != null && i < s.trackIndices.Count ? s.trackIndices[i] : 0,
                        s.trackProgress != null && i < s.trackProgress.Count ? s.trackProgress[i] : 0f,
                        s.trackStepIds != null && i < s.trackStepIds.Count ? s.trackStepIds[i] : null);
                }
                return;
            }

            if (string.IsNullOrEmpty(s.mainQuestId)) return;

            var owner = _challenges.Tracks.FirstOrDefault(t => t.Chain.Any(d => d.Id == s.mainQuestId));
            if (owner == null)
            {
                Debug.Log($"[ICanShowYouTheWorld] Saved questline step '{s.mainQuestId}' belongs to no current " +
                          "track; both questlines start at the beginning of this act.");
                return;
            }

            _challenges.RestoreTrack(owner.Id, s.mainQuestIndex, s.mainQuestProgress, s.mainQuestId);
            Debug.Log($"[ICanShowYouTheWorld] Migrated a pre-track save: '{s.mainQuestId}' resumed on the " +
                      $"{owner.Label} track.");
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

                // Same reasoning, and more urgent: RestoreFrom re-applies the saved skill loans to
                // the real character before the lines that can throw. The state file — the only
                // other record of what those skills were worth — has just been deleted, and EndRun
                // merely drops the table, so skipping this would make the loan permanent with
                // nothing left to undo it from.
                RestoreLoanedSkills();
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
            _visitedBiomes = s.visitedBiomes != 0
                ? s.visitedBiomes
                : (int)Heightmap.Biome.Meadows;   // pre-biome-gating save: seed the floor
            try { if (Player.m_localPlayer != null) _visitedBiomes |= (int)Player.m_localPlayer.GetCurrentBiome(); } catch { }

            // Null on a pre-alpha26 save, which reads as "nothing built yet" and re-latches from
            // the next scan. The ExternalFilter closure reads this set live, so restoring it here —
            // before or after BuildEngines — is equally correct.
            _builtSeen.Clear();
            if (s.builtCategories != null)
                foreach (var category in s.builtCategories)
                    if (!string.IsNullOrEmpty(category)) _builtSeen.Add(category);

            _stash.Restore(s.stashPrefabs, s.stashCounts, s.stashQualities, s.stashVariants);

            _discovered.Clear();
            if (s.discoveredLocations != null)
                foreach (var loc in s.discoveredLocations)
                    if (!string.IsNullOrEmpty(loc)) _discovered.Add(loc);

            // Re-lend what completions had already paid, so a resumed run keeps the health it
            // earned. 0 on a pre-alpha35 save, which simply starts the accumulation from there.
            _taskHealthReward = Math.Max(0f, s.taskHealthReward);
            _homewardCharges = Math.Max(0, s.homewardCharges);

            _rng = new Random(_rngSeed);
            _deer = new DeerHerd(_cfg, _rng);

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

            // The questline picks up where it left off. A save written before the chain existed
            // carries 0/0, which reads as "start of the chain, nothing done" — the right answer:
            // such a run simply gains the questline from here, at step one.
            //
            // No baseline is persisted for a StatDelta step (see SyncStatDeltaBaselines below,
            // which takes a fresh one): re-baselining can only ever cost the player the fraction
            // of a single craft, because the restored PROGRESS is kept and the report path takes
            // the max of the two.
            RestoreQuestTracks(s);

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

            // A resume must NOT re-snapshot: the character is already carrying the loans, so
            // reading a level now would record the loaned value as their "original" and make the
            // loan permanent. The saved originals are authoritative. A save from before the loans
            // existed (or one taken before a snapshot could be read) carries none, and only then is
            // a fresh snapshot the right thing.
            _skillLoans.Clear();
            foreach (var loan in RunStorage.ImportSkillLoans(s))
                _skillLoans[loan.Key] = new SkillLoan { Original = loan.Value.original, Level = loan.Value.level };

            // Nothing to re-apply on a save that carries no loans — including a save from
            // before loans existed. A fresh snapshot here would invent a grant the run never made.
            ReapplyLoanedSkills();
            // OnHeatChanged, not ApplyHeat: a resumed run must also re-scale Forge-fed to the heat
            // it is coming back at, or the boon would sit at its floor until the next heat change.
            OnHeatChanged();
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
                // Independent of the actives lists above: the questline lives in a reserved slot
                // of its own, so it saves as a position + a progress value, not as a list entry.
                // Kept written for one build's worth of backwards compatibility: a save made here
                // and read by an alpha31 binary still finds its first track where it expects it.
                mainQuestIndex = _challenges?.MainQuestIndex ?? 0,
                mainQuestProgress = _challenges?.CurrentMainQuest?.Progress ?? 0f,
                mainQuestId = _challenges?.CurrentMainQuest?.Def?.Id,

                trackIds = _challenges?.Tracks.Select(t => t.Id).ToList(),
                trackIndices = _challenges?.Tracks.Select(t => t.Index).ToList(),
                trackProgress = _challenges?.Tracks.Select(t => t.Current?.Progress ?? 0f).ToList(),
                trackStepIds = _challenges?.Tracks.Select(t => t.Current?.Def?.Id).ToList(),
                skillLoanTypes = _skillLoans.Keys.Select(k => (int)k).ToList(),
                skillLoanOriginals = _skillLoans.Values.Select(v => v.Original).ToList(),
                skillLoanLevels = _skillLoans.Values.Select(v => v.Level).ToList(),
                heldBoonIds = held.Select(h => h.Def.Id).ToList(),
                heldBoonCooldowns = held.Select(h => h.CooldownRemaining).ToList(),
                heldBoonCharges = held.Select(h => h.Charges).ToList(),
                rngSeed = _rngSeed,
                visitedBiomes = _visitedBiomes,
                builtCategories = _builtSeen.ToList(),
                discoveredLocations = _discovered.ToList(),
                taskHealthReward = _taskHealthReward,
                homewardCharges = _homewardCharges,
                stashPrefabs = _stash.Entries.Select(e => e.Prefab).ToList(),
                stashCounts = _stash.Entries.Select(e => e.Count).ToList(),
                stashQualities = _stash.Entries.Select(e => e.Quality).ToList(),
                stashVariants = _stash.Entries.Select(e => e.Variant).ToList(),
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
        /// <summary>
        /// Bosses felled in this WORLD (the global keys), not just during this run — the same
        /// basis as the challenge tier ceiling, so a companion's strength tracks where the player
        /// actually is in the game. A missing ZoneSystem reads as zero, the safe direction.
        /// </summary>
        private int DefeatedBossCount()
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return 0;
            return Bosses.Count(b => SafeGetGlobalKey(zone, b.defeatKey));
        }

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
            // The standing task: seated in a slot on the first tick (Opener) and never vacated
            // (Repeatable), so the run always has one thing on offer no matter how the random
            // deals go. Hugin turns up on his own schedule, which makes this a task that rewards
            // paying attention rather than one more thing to farm — and every fifth talk pays a
            // boon, the same as any other completion. RavenTalk is a real PlayerStatType, checked
            // against the enum in the IL.
            new ChallengeDefinition
            {
                Id = "raven-talk", Tier = 0, Opener = true, Repeatable = true,
                Kind = ChallengeKind.StatDelta, Param = "RavenTalk", Target = 5, HeatReward = 1,
                Display = "Heed Hugin 5 times",
            },
            // The three opener-flagged links that used to live here (o-wood/o-stone/o-craft) are
            // GONE: the MAIN QUEST CHAIN (see MainQuestChain) is the on-ramp now, and a scripted
            // opening that also ate all three random slots left no room for anything else in the
            // first minutes. The engine's Opener mechanism itself is untouched and still tested —
            // this pool simply no longer uses it.
            //
            // c-wood/c-stone below stay exactly as they were: ordinary random tasks.

            new ChallengeDefinition { Id = "k-greydwarf", Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Greydwarf", Target = 6, Biomes = 8, HeatReward = 2, Display = "Kill 6 Greydwarves" },
            new ChallengeDefinition { Id = "k-skeleton",  Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Skeleton",  Target = 6, Biomes = 8, HeatReward = 2, Display = "Kill 6 Skeletons" },
            new ChallengeDefinition { Id = "k-troll",     Tier = 1, Kind = ChallengeKind.KillPrefab, Param = "Troll",     Target = 1,  Biomes = 8, HeatReward = 3, Display = "Slay a Troll" },
            new ChallengeDefinition { Id = "k-draugr",    Tier = 2, Kind = ChallengeKind.KillPrefab, Param = "Draugr",    Target = 5,  Biomes = 2, HeatReward = 3, Display = "Kill 5 Draugr" },

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
            new ChallengeDefinition { Id = "k-deer",      Tier = 0, Kind = ChallengeKind.KillPrefab, Param = "Deer",     Target = 3, Biomes = 0, HeatReward = 1, Display = "Hunt 3 Deer" },
            new ChallengeDefinition { Id = "k-greyling",  Tier = 0, Kind = ChallengeKind.KillPrefab, Param = "Greyling", Target = 3, Biomes = 0, HeatReward = 1, Display = "Kill 3 Greylings" },
            new ChallengeDefinition { Id = "k-ghost",     Tier = 2, Kind = ChallengeKind.KillPrefab, Param = "Ghost",    Target = 3, Biomes = 8, HeatReward = 2, Display = "Kill 3 Ghosts" },
            new ChallengeDefinition { Id = "k-surtling",  Tier = 2, Kind = ChallengeKind.KillPrefab, Param = "Surtling", Target = 3, Biomes = 2, HeatReward = 2, Display = "Kill 3 Surtlings" },
            new ChallengeDefinition { Id = "k-wolf",   Tier = 3, Kind = ChallengeKind.KillPrefab, Param = "Wolf",   Target = 3, Biomes = 4,  HeatReward = 3, Display = "Kill 3 Wolves" },
            new ChallengeDefinition { Id = "k-goblin", Tier = 4, Kind = ChallengeKind.KillPrefab, Param = "Goblin", Target = 4, Biomes = 16, HeatReward = 3, Display = "Kill 4 Fulings" },
            new ChallengeDefinition { Id = "k-lox",       Tier = 4, Kind = ChallengeKind.KillPrefab, Param = "Lox",         Target = 2, Biomes = 16, HeatReward = 3, Display = "Kill 2 Lox" },
            new ChallengeDefinition { Id = "k-deathsquito", Tier = 4, Kind = ChallengeKind.KillPrefab, Param = "Deathsquito", Target = 3, Biomes = 16, HeatReward = 3, Display = "Kill 3 Deathsquitoes" },

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
            // Tier 2, not 1: MaxTier is defeatedBosses + 1, so a Tier-1 task is drawable from the
            // first minute of a run. Mining needs a pickaxe and the first pickaxe is Eikthyr's
            // antler, so anything below Tier 2 asks for a tool the player cannot have yet.
            new ChallengeDefinition { Id = "s-mine2",   Tier = 2, Kind = ChallengeKind.StatDelta, Param = "MineHits",         Target = 70, HeatReward = 2, Display = "Land 70 mining hits" },
            new ChallengeDefinition { Id = "s-kills",   Tier = 1, Kind = ChallengeKind.StatDelta, Param = "EnemyKills",       Target = 8,  HeatReward = 2, Display = "Kill 8 creatures — anything" },
            new ChallengeDefinition { Id = "s-kills2",  Tier = 2, Kind = ChallengeKind.StatDelta, Param = "EnemyKills",       Target = 15, HeatReward = 3, Display = "Kill 15 creatures — anything" },
            new ChallengeDefinition { Id = "s-food",    Tier = 1, Kind = ChallengeKind.StatDelta, Param = "FoodEaten",        Target = 3,  HeatReward = 1, Display = "Eat 3 meals" },
            new ChallengeDefinition { Id = "s-sleep",   Tier = 0, Kind = ChallengeKind.StatDelta, Param = "Sleep",            Target = 1,  HeatReward = 1, Display = "Sleep through a night" },
            new ChallengeDefinition { Id = "s-run",    Tier = 0, Kind = ChallengeKind.StatDelta, Param = "DistanceRun",      Target = 400, HeatReward = 1, Display = "Run 400m" },
            new ChallengeDefinition { Id = "s-mine",   Tier = 2, Kind = ChallengeKind.StatDelta, Param = "MineHits",         Target = 35,  HeatReward = 2, Display = "Land 35 mining hits" },
            new ChallengeDefinition { Id = "s-craft",  Tier = 1, Kind = ChallengeKind.StatDelta, Param = "CraftsOrUpgrades", Target = 5,   HeatReward = 1, Display = "Craft or upgrade 5 times" },
            // Gated on owning a door rather than on tier: DoorsOpened cannot move without one, and
            // before alpha26 this was drawable from the first minute of a run, when the player has
            // no hammer, let alone a door.
            new ChallengeDefinition { Id = "s-doors",  Tier = 1, Kind = ChallengeKind.StatDelta, Param = "DoorsOpened",      Target = 8,  HeatReward = 1, RequiresBuilt = "Door", Display = "Open 8 doors" },
            // --- Boats: pool only, and gated twice. ---
            //
            // A boat quest is only ever sensible on a world where water is in the way, and nothing
            // knows that in advance. So these are never chain steps (the chain is linear and would
            // hard-stall) and they are gated on evidence rather than guesswork: Biomes = Ocean means
            // "this run has been on open water", RequiresBuilt = "Ship" means "this run owns a boat".
            // A landlocked run satisfies neither and is never dealt one.
            //
            // The Ocean gate is conservative on purpose. Valheim assigns Ocean to deep water, so
            // paddling off a beach usually still reads as the shore's own biome — the gate really
            // fires for players genuinely out on open water. Never offering is the right way to be
            // wrong here.
            new ChallengeDefinition { Id = "s-boat",   Tier = 1, Kind = ChallengeKind.BuildPiece, Param = "Ship", Target = 1, Biomes = (int)Heightmap.Biome.Ocean, HeatReward = 2, Display = "Build a boat" },
            new ChallengeDefinition { Id = "s-sail",   Tier = 2, Kind = ChallengeKind.StatDelta, Param = "DistanceSail",     Target = 180, Biomes = (int)Heightmap.Biome.Ocean, RequiresBuilt = "Ship", HeatReward = 2, Display = "Sail for 90 seconds" },
            new ChallengeDefinition { Id = "s-sail2",  Tier = 2, Kind = ChallengeKind.StatDelta, Param = "DistanceSail",     Target = 420, Biomes = (int)Heightmap.Biome.Ocean, RequiresBuilt = "Ship", HeatReward = 3, Display = "Sail for 3 minutes" },

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

            // --- alpha27: more simultaneous quests, and cooking. ---
            //
            // Every name below is now checked at run start by ValidateAssetNames, which is what
            // makes the cooked-food tokens usable at all: before it, a wrong $item_ token was a
            // sub that stayed dead all run with nothing to tell you why. If one of these is wrong
            // it says so in the log on the first run, and it is a one-line fix.
            //
            // The build subs are new too — BuildPiece became a legal composite sub in alpha27
            // (absolute quantity, no per-sub baseline, which is the rule composites actually need).
            new ChallengeDefinition
            {
                Id = "cq-hearth", Tier = 0, Target = 1, HeatReward = 3, Display = "Hearth and Home",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.BuildPiece, Param = "Cooking", Target = 1, Label = "Build a cooking station" },
                    new SubObjective { Kind = ChallengeKind.BuildPiece, Param = "Chest",   Target = 1, Label = "Build a chest" },
                    new SubObjective { Kind = ChallengeKind.CollectFood, Param = "",       Target = 8, Label = "Hold 8 food" },
                }
            },
            new ChallengeDefinition
            {
                // The name-free cooking quest: no asset names at all, so it works whatever the
                // tokens turn out to be. Gated on owning a rack, so it is never dealt to someone
                // who cannot start it. Its weakness is honest — CollectFood counts raspberries, so
                // a determined forager can finish it without cooking. That is the price of not
                // naming anything, and cq-larder below is the version that does name things.
                Id = "cq-provisions", Tier = 0, Target = 1, HeatReward = 2, Display = "Provisions",
                RequiresBuilt = "Cooking",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.CollectFood, Param = "", Target = 12, Label = "Hold 12 food" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Boar", Target = 3, Label = "Kill 3 Boar" },
                }
            },
            new ChallengeDefinition
            {
                // The named one: actually requires COOKED meat, which CollectFood cannot express.
                Id = "cq-larder", Tier = 0, Target = 1, HeatReward = 3, Display = "Fill the Larder",
                RequiresBuilt = "Cooking",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.CollectItem, Param = "$item_cookedmeat", Target = 5, Label = "Cook 5 meat" },
                    new SubObjective { Kind = ChallengeKind.CollectItem, Param = "$item_wood",       Target = 20, Label = "Hold 20 wood" },
                }
            },
            new ChallengeDefinition
            {
                // Simultaneous kills across two species — the shape cq-forest-sweep already had,
                // which the pool simply did not have enough of.
                Id = "cq-meadow-cull", Tier = 0, Target = 1, HeatReward = 3, Display = "Meadow Cull",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Boar",     Target = 3, Label = "Kill 3 Boar" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Deer",     Target = 2, Label = "Kill 2 Deer" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Greyling", Target = 4, Label = "Kill 4 Greylings" },
                }
            },
            new ChallengeDefinition
            {
                Id = "cq-night-watch", Tier = 1, Target = 1, HeatReward = 4, Display = "Night Watch",
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Greyling",  Target = 5, Label = "Kill 5 Greylings" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Greydwarf", Target = 3, Label = "Kill 3 Greydwarves" },
                    new SubObjective { Kind = ChallengeKind.KillPrefab, Param = "Neck",      Target = 3, Label = "Kill 3 Necks" },
                }
            },
        };

        // --- The saga: one act per boss ---

        /// <summary>
        /// The five acts, in order, aligned one-to-one with <see cref="Bosses"/>. Which one is
        /// current is derived from the world's defeated-boss count — see
        /// <see cref="CurrentActIndex"/> — so this table is pure content.
        ///
        /// Acts I and II are written out in full. III to V are deliberately SHORT: three or four
        /// steps each, enough that no act is ever the dead end Act I used to be, and no more than
        /// that until someone has played that far and can say what belongs there. Thin is honest;
        /// absent is a bug.
        ///
        /// Every chain ends with its own boss, which is what makes "the chain ran out" and "the act
        /// is over" the same event.
        /// </summary>
        internal static List<ActDefinition> Acts() => new List<ActDefinition>
        {
            new ActDefinition
            {
                Id = "act1", Numeral = "I", Title = "The Meadows",
                BossDefeatKey = "defeated_eikthyr", Tracks = Split(MainQuestChain()),
            },
            new ActDefinition
            {
                Id = "act2", Numeral = "II", Title = "The Black Forest",
                BossDefeatKey = "defeated_gdking", Tracks = Split(BlackForestChain()),
            },
            new ActDefinition
            {
                Id = "act3", Numeral = "III", Title = "The Swamp",
                BossDefeatKey = "defeated_bonemass", Tracks = Split(SwampChain()),
            },
            new ActDefinition
            {
                Id = "act4", Numeral = "IV", Title = "The Mountains",
                BossDefeatKey = "defeated_dragon", Tracks = Split(MountainChain()),
            },
            new ActDefinition
            {
                Id = "act5", Numeral = "V", Title = "The Plains",
                BossDefeatKey = "defeated_goblinking", Tracks = Split(PlainsChain()),
            },
        };

        public const string HuntTrackId = "hunt";
        public const string CraftTrackId = "craft";

        /// <summary>
        /// Cuts an act's steps into the two tracks, along the seam the content already had: KILLS go
        /// to HUNT, everything else to CRAFT.
        ///
        /// Splitting rather than hand-writing two lists is deliberate. The acts are written as one
        /// ordered narrative and read better that way, the seam is a property of each step's Kind
        /// rather than an editorial decision, and a step added later lands on the right track without
        /// anyone having to remember to put it there.
        ///
        /// Relative order is preserved within each track, so each still reads as a progression.
        ///
        /// Note the consequence, visible rather than hidden: the later acts are kill-heavy, so their
        /// CRAFT tracks are short — Act IV's is two steps. That is what splitting existing content
        /// means, and with heat as a player-steered dial a short track simply offers less optional
        /// heat in that act rather than being a defect.
        /// </summary>
        internal static List<QuestTrack> Split(List<ChallengeDefinition> chain)
        {
            var steps = chain ?? new List<ChallengeDefinition>();

            return new List<QuestTrack>
            {
                new QuestTrack
                {
                    Id = HuntTrackId, Label = "HUNT",
                    Chain = steps.Where(d => d.Kind == ChallengeKind.KillPrefab).ToList(),
                },
                new QuestTrack
                {
                    Id = CraftTrackId, Label = "CRAFT",
                    Chain = steps.Where(d => d.Kind != ChallengeKind.KillPrefab).ToList(),
                },
            };
        }

        /// <summary>Every act's steps, for the name validator — Act V's names are worth checking in Act I.</summary>
        internal static IEnumerable<ChallengeDefinition> AllActChains() => Acts().SelectMany(a => a.AllSteps);

        // --- Act I: the Meadows → Eikthyr arc ---

        /// <summary>
        /// The ordered main quest. Unlike the random tasks it is never drawn, never rerolled and
        /// never tier-gated (see <see cref="ChallengeEngine.SetMainChain"/>), so it is the one
        /// thread a run can always follow — and it pays in ITEMS rather than heat and boons, which
        /// is the whole point of the separation: random tasks make you stronger in the abstract,
        /// the questline hands you the gear that opens the next step.
        ///
        /// Targets are deliberately tiny. The design brief is "no grinding": every step should be
        /// a few minutes of ordinary play, and the reward should skip the tedious part of what
        /// comes next (a bow instead of stalking deer barehanded, armor instead of a mining trip).
        ///
        /// Mechanics chosen for the same silent-failure reasons documented on the pool below:
        /// step 1 counts CRAFTS via a PlayerStatType (checked against the enum in the IL) rather
        /// than looking for an axe by its $item_ token, which is asset data this build cannot
        /// verify and which fails silently when wrong. The kill steps use prefab names, matched by
        /// the same Character death hook every kill task already uses; "Eikthyr" is the boss's
        /// prefab (its LOCATION is "Eikthyrnir", which is what the boss table above holds — the two
        /// are not the same string).
        /// </summary>
        internal static List<ChallengeDefinition> MainQuestChain() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition
            {
                Id = "mq-axe", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "CraftsOrUpgrades",
                Target = 1, Display = "Craft an axe", RewardText = "Bow, arrows, wood — and the skill to use them",
            },
            new ChallengeDefinition
            {
                Id = "mq-hammer", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "CraftsOrUpgrades",
                Target = 1, Display = "Craft a hammer", RewardText = "Timber and stone for a roof",
            },
            new ChallengeDefinition
            {
                Id = "mq-bench", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "Builds",
                Target = 1, Display = "Build a workbench", RewardText = "A leather tunic",
                Hint = "Ten wood, and stand close to it while you craft.",
            },
            new ChallengeDefinition
            {
                Id = "mq-boar", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Boar",
                Target = 5, Display = "Hunt 5 Boar", RewardText = "Leather leggings + a quiver of arrows",
            },
            new ChallengeDefinition
            {
                Id = "mq-shelter", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "Builds",
                Target = 6, Display = "Raise a roof (6 pieces)", RewardText = "Timber and stone to finish it",
                Hint = "Roof pieces overhead — walls alone are not shelter.",
            },
            // The homestead steps (alpha26). Each one lands immediately before the step that
            // already, silently, required it: TimeInBase only accrues while Player.IsSafeInHome,
            // which needs real comfort — a roof AND a fire — and Sleep needs a bed. Both of those
            // prerequisites used to be invisible, so a player who had not built a fire watched
            // "Settle in" sit at zero with nothing telling them why.
            //
            // They measure with ChallengeKind.BuildPiece, which asks the host whether the player
            // has built a piece carrying a given COMPILED component (Fireplace, Bed, Container) —
            // never a prefab name, which is asset data this build cannot verify and which fails
            // silently when wrong. See PieceCategories.
            new ChallengeDefinition
            {
                Id = "mq-fire", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Fire",
                Target = 1, Display = "Build a fire", RewardText = "Hide for a bed, flint for arrowheads",
                Hint = "A campfire. Not under a wooden floor, or it burns.",
            },
            new ChallengeDefinition
            {
                // Straight after the fire, because that is what it goes on. Nothing in the chain
                // taught cooking before this, which left the single biggest lever on health and
                // stamina as something the player had to know about from outside the run.
                Id = "mq-cook", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Cooking",
                Target = 1, Display = "Build a cooking station", RewardText = "Meat to cook on it",
                Hint = "A cooking station goes ON a fire, not beside it.",
            },
            new ChallengeDefinition
            {
                // Greyling, not Greydwarf: the greydwarf proper lives in the Black Forest, and
                // every step before Eikthyr should be doable without leaving the Meadows. Greylings
                // are the weaker meadows cousin, hence the higher count. The ID is deliberately
                // unchanged so a run already part-way through this step keeps its progress.
                Id = "mq-grey", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Greyling",
                Target = 6, Display = "Kill 6 Greylings", RewardText = "Helmet + cape + more arrows",
            },
            // Build it, live in it, sleep in it. All three ride stats Valheim keeps itself, so
            // none of them can silently stall the chain the way a check against a named building
            // piece would: Builds counts every piece placed (Player.PlacePiece), TimeInBase
            // accrues half a second at a time and ONLY while Player.IsSafeInHome — which needs
            // real comfort, a roof and a fire, not just walls — and Sleep counts every night
            // slept through (Player.SetSleeping). Sleeping is the honest test of a home: it wants
            // a bed and nothing hostile at the door, and it puts the player at the boss in
            // daylight.
            new ChallengeDefinition
            {
                Id = "mq-bed", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Bed",
                Target = 1, Display = "Build a bed", RewardText = "Timber and resin for the rest of the house",
            },
            new ChallengeDefinition
            {
                Id = "mq-home", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "TimeInBase",
                Target = 120, Display = "Settle in (2 min at home)", RewardText = "A shield by the door, and arrows",
                Hint = "Needs a roof AND a fire. Stand still indoors and it counts up.",
            },
            new ChallengeDefinition
            {
                Id = "mq-rest", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "Sleep",
                Target = 1, Display = "Sleep through the night", RewardText = "A hot meal and arrows",
                Hint = "A bed you have claimed, and nothing hostile nearby.",
            },
            new ChallengeDefinition
            {
                // Last of the homestead steps, and the only one nothing downstream depends on —
                // it sits here because somewhere to put the spoils is what you want BEFORE a hunt,
                // and its reward feeds the hunt directly.
                Id = "mq-chest", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Chest",
                Target = 1, Display = "Build a chest", RewardText = "A full quiver before the hunt",
            },
            new ChallengeDefinition
            {
                Id = "mq-deer", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Deer",
                Target = 3, Display = "Hunt 3 Deer", RewardText = "Deer trophies — Eikthyr's summons",
            },
            new ChallengeDefinition
            {
                // Act I's climax before the boss, and the one deer that is an event rather than a
                // counter. Param is SYNTHETIC — the Herald is an ordinary Deer wearing a name, so
                // matching on "Deer" would let any deer finish this. The host reports this name only
                // when that specific creature dies, matched by ZDOID. See DeerHerd.
                Id = "mq-herald", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = DeerHerd.HeraldKillName,
                Target = 1, Display = "Hunt Eikthyr's Herald", RewardText = "A hunter's bow, and the last trophies",
            },
            new ChallengeDefinition
            {
                Id = "mq-find", MainQuest = true, Kind = ChallengeKind.DiscoverLocation, Param = "Eikthyrnir",
                Target = 1, Display = "Find Eikthyr's altar", RewardText = "Eikthyr's summoning stones await",
                Hint = "Two standing stones in the meadows, ringed with runes. Look for open ground.",
            },
            new ChallengeDefinition
            {
                Id = "mq-eikthyr", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Eikthyr",
                Target = 1, Display = "Defeat Eikthyr", RewardText = "Antler pickaxe",
            },
        };

        // --- Act II: the Black Forest → The Elder ---

        /// <summary>
        /// Written out in full, on the same terms as Act I: small steps, item rewards, and every
        /// measure something already proven. MineHits and CraftsOrUpgrades are PlayerStatTypes;
        /// Smelter is a compiled class, so the smelter step carries no more asset-name risk than the
        /// cooking station did.
        ///
        /// The boss step kills "gd_king". The boss TABLE holds "GDKing" — that is the LOCATION, and
        /// the two are different strings. Exactly the confusion the alpha27 validator now catches on
        /// first launch rather than after an unwinnable run.
        ///
        /// Ancient Seeds are HANDED OVER rather than farmed, for the same reason deer trophies are
        /// in Act I: the Elder's altar wants three, they drop from greydwarf shamans and brutes, and
        /// gating an act's finale on drop luck is the one thing this questline never does.
        /// </summary>
        internal static List<ChallengeDefinition> BlackForestChain() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition
            {
                // Every act opens on arrival. ReachBiome asks for the DESTINATION and says nothing
                // about how you got there — which is the only safe thing for a linear chain to ask,
                // since a boat step would stall on a world where the biome is walkable.
                Id = "bf-arrive", MainQuest = true, Kind = ChallengeKind.ReachBiome, Param = "BlackForest",
                Target = 1, Display = "Reach the Black Forest", RewardText = "A torch, and arrows for the dark",
                Hint = "Dark pines and rock. Head away from the meadows.",
            },
            new ChallengeDefinition
            {
                // Eikthyr's antler pickaxe is what opens this act, so the act opens by using it.
                Id = "bf-copper", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "MineHits",
                Target = 40, Display = "Mine the Black Forest (40 hits)", RewardText = "Copper, tin, and coal to smelt it",
                Hint = "Copper needs Eikthyr's antler pickaxe. Look for mottled rock outcrops.",
            },
            new ChallengeDefinition
            {
                Id = "bf-smelter", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Smelter",
                Target = 1, Display = "Build a smelter", RewardText = "More ore than it can hold",
                Hint = "Stone, and surtling cores from the burial chambers.",
            },
            new ChallengeDefinition
            {
                Id = "bf-bronze", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "CraftsOrUpgrades",
                Target = 3, Display = "Forge three things in bronze", RewardText = "Bronze for armour",
                Hint = "Copper and tin smelted together, then forged at a workbench.",
            },
            new ChallengeDefinition
            {
                // The act where the run stops being about one base. A portal is the other half of
                // the answer to "we have to leave our house behind": the stash carries your things,
                // a portal carries you.
                Id = "bf-portal", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Portal",
                Target = 1, Display = "Build a portal", RewardText = "Fine wood and cores for its twin",
                Hint = "Fine wood, greydwarf eyes and surtling cores. Build two.",
            },
            new ChallengeDefinition
            {
                Id = "bf-greydwarf", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Greydwarf",
                Target = 10, Display = "Kill 10 Greydwarves", RewardText = "A bronze buckler and arrows",
            },
            new ChallengeDefinition
            {
                Id = "bf-brute", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Greydwarf_Elite",
                Target = 3, Display = "Kill 3 Greydwarf Brutes", RewardText = "Root armour against their arrows",
            },
            new ChallengeDefinition
            {
                Id = "bf-troll", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Troll",
                Target = 1, Display = "Kill a Troll", RewardText = "Troll hide, and the seeds the Elder wants",
            },
            new ChallengeDefinition
            {
                Id = "bf-find", MainQuest = true, Kind = ChallengeKind.DiscoverLocation, Param = "GDKing",
                Target = 1, Display = "Find the Elder's altar", RewardText = "The Elder's altar, and what it wants",
                Hint = "A ring of stone in the deep forest, guarded. Follow the oldest trees.",
            },
            new ChallengeDefinition
            {
                Id = "bf-elder", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "gd_king",
                Target = 1, Display = "Defeat The Elder", RewardText = "The swamp key",
            },
        };

        // --- Acts III-V: short chains, deliberately ---

        /// <summary>
        /// Acts III to V, seven steps each, on the same rhythm as Act II: arrive, fight, build,
        /// gather, fight, boss.
        ///
        /// Their creature names are the standard vanilla prefabs and are checked at run start by
        /// the validator, so a wrong one shows up in the log on the first launch of any run rather
        /// than as an unwinnable act hours in.
        ///
        /// Note what each BUILD step is chosen for: the fermenter is not decoration, it is how you
        /// make poison resistance mead, which is the answer to Bonemass. The building step teaches
        /// the fight.
        /// </summary>
        internal static List<ChallengeDefinition> SwampChain() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition
            {
                Id = "sw-arrive", MainQuest = true, Kind = ChallengeKind.ReachBiome, Param = "Swamp",
                Target = 1, Display = "Reach the Swamp", RewardText = "Poison resistance mead to survive it",
                Hint = "Flat, flooded, and grey. Bring the poison mead.",
            },
            new ChallengeDefinition
            {
                Id = "sw-draugr", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Draugr",
                Target = 8, Display = "Kill 8 Draugr", RewardText = "Iron arrows and a shield",
            },
            new ChallengeDefinition
            {
                // The mead this makes IS the Bonemass fight. Building it here is the hint.
                Id = "sw-fermenter", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Fermenter",
                Target = 1, Display = "Build a fermenter", RewardText = "Honey and herbs for the mead",
                Hint = "Honey from a beehive, and thistle from the forest floor.",
            },
            new ChallengeDefinition
            {
                Id = "sw-blob", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Blob",
                Target = 5, Display = "Kill 5 Blobs", RewardText = "Withered bone — Bonemass's summons",
            },
            new ChallengeDefinition
            {
                Id = "sw-iron", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "MineHits",
                Target = 60, Display = "Dig up the crypts (60 mining hits)", RewardText = "Iron, already smelted",
                Hint = "Iron is scrap in the crypts, not ore in the ground. Bring a key.",
            },
            new ChallengeDefinition
            {
                Id = "sw-leech", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Leech",
                Target = 3, Display = "Kill 3 Leeches", RewardText = "An iron mace — blunt beats bone",
            },
            new ChallengeDefinition
            {
                Id = "sw-find", MainQuest = true, Kind = ChallengeKind.DiscoverLocation, Param = "Bonemass",
                Target = 1, Display = "Find Bonemass's altar", RewardText = "Blunt weapons, and a way in",
                Hint = "A skull on a mound, deep in the mire. Watch the water — it hides the path.",
            },
            new ChallengeDefinition
            {
                Id = "sw-bonemass", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Bonemass",
                Target = 1, Display = "Defeat Bonemass", RewardText = "Wishbone",
            },
        };

        /// <summary>
        /// The Mountains have NO build step, and that is deliberate rather than an oversight: no
        /// distinctively mountain-built piece has a compiled class of its own, and every category
        /// that does is already claimed by an earlier act. Inventing a filler step would be worse
        /// than an extra fight, so the act gets an extra fight.
        ///
        /// (Reusing an earlier act's category would not work anyway — the built-piece latch runs for
        /// the whole RUN, so "build a fire" in Act IV would complete the instant it was dealt. That
        /// rule is enforced by ValidateActs.)
        /// </summary>
        internal static List<ChallengeDefinition> MountainChain() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition
            {
                Id = "mt-arrive", MainQuest = true, Kind = ChallengeKind.ReachBiome, Param = "Mountain",
                Target = 1, Display = "Reach the Mountains", RewardText = "Frost resistance mead and warm hide",
                Hint = "Above the snowline. Frost will kill you without mead or wolf armour.",
            },
            new ChallengeDefinition
            {
                Id = "mt-wolf", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Wolf",
                Target = 6, Display = "Kill 6 Wolves", RewardText = "Wolf armour against the cold",
            },
            new ChallengeDefinition
            {
                Id = "mt-drake", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Hatchling",
                Target = 4, Display = "Kill 4 Drakes", RewardText = "Frost arrows and dragon tears",
            },
            new ChallengeDefinition
            {
                Id = "mt-silver", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "MineHits",
                Target = 60, Display = "Mine silver (60 hits)", RewardText = "Silver, already smelted",
                Hint = "Silver hides underground — the wishbone finds it.",
            },
            new ChallengeDefinition
            {
                Id = "mt-golem", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "StoneGolem",
                Target = 2, Display = "Kill 2 Stone Golems", RewardText = "Crystal, and a silver blade",
            },
            new ChallengeDefinition
            {
                Id = "mt-fenring", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Fenring",
                Target = 3, Display = "Kill 3 Fenrings", RewardText = "Dragon eggs — Moder's summons",
            },
            new ChallengeDefinition
            {
                Id = "mt-find", MainQuest = true, Kind = ChallengeKind.DiscoverLocation, Param = "Dragonqueen",
                Target = 1, Display = "Find Moder's altar", RewardText = "Frost mead for the summit",
                Hint = "The highest bone-ringed peak. Cold enough to kill without a mead.",
            },
            new ChallengeDefinition
            {
                Id = "mt-moder", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Dragon",
                Target = 1, Display = "Defeat Moder", RewardText = "Dragon tears",
            },
        };

        internal static List<ChallengeDefinition> PlainsChain() => new List<ChallengeDefinition>
        {
            new ChallengeDefinition
            {
                Id = "pl-arrive", MainQuest = true, Kind = ChallengeKind.ReachBiome, Param = "Plains",
                Target = 1, Display = "Reach the Plains", RewardText = "Padded armour — deathsquitos are quick",
                Hint = "Tall golden grass. Deathsquitos hit hard and come fast.",
            },
            new ChallengeDefinition
            {
                Id = "pl-fuling", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Goblin",
                Target = 10, Display = "Kill 10 Fulings", RewardText = "Black metal and needle arrows",
            },
            new ChallengeDefinition
            {
                Id = "pl-windmill", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Windmill",
                Target = 1, Display = "Build a windmill", RewardText = "Barley and flour for the last feast",
                Hint = "Stone and wood, on flat open ground. It grinds barley into flour.",
            },
            new ChallengeDefinition
            {
                Id = "pl-squito", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Deathsquito",
                Target = 5, Display = "Kill 5 Deathsquitos", RewardText = "A black metal blade",
            },
            new ChallengeDefinition
            {
                Id = "pl-lox", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Lox",
                Target = 3, Display = "Kill 3 Lox", RewardText = "Lox meat pies, and a cape",
            },
            new ChallengeDefinition
            {
                Id = "pl-berserker", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "GoblinBrute",
                Target = 2, Display = "Kill 2 Fuling Berserkers", RewardText = "Yagluth's summons",
            },
            new ChallengeDefinition
            {
                Id = "pl-find", MainQuest = true, Kind = ChallengeKind.DiscoverLocation, Param = "GoblinKing",
                Target = 1, Display = "Find Yagluth's altar", RewardText = "The last of the run's provisions",
                Hint = "A ruin of stone hands in the tall grass. Fulings camp near it.",
            },
            new ChallengeDefinition
            {
                Id = "pl-yagluth", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "GoblinKing",
                Target = 1, Display = "Defeat Yagluth", RewardText = "The saga is complete",
            },
        };

        /// <summary>
        /// What each questline step actually hands over, keyed by step id: (item prefab name,
        /// count). The chain's RewardText is the player-facing spelling of the same thing — they
        /// are written next to each other on purpose, since a mismatch between the promise and the
        /// grant is invisible until someone plays it.
        ///
        /// Item prefab names are Unity asset data and cannot be confirmed against the compiled
        /// assembly (see the note on the kill pool below); these are the well-established vanilla
        /// names, and <see cref="GrantQuestReward"/> logs loudly if one fails to resolve rather
        /// than failing silently.
        /// </summary>
        private static readonly Dictionary<string, (string prefab, int count)[]> QuestRewards =
            new Dictionary<string, (string, int)[]>
            {
                // "Lots of wood when he completes the axe, so he can build shelter" — the wood
                // IS the reward here as much as the bow: skip straight from first craft to a roof.
                ["mq-axe"] = new[] { ("Bow", 1), ("ArrowWood", 40), ("Wood", 25), ("Stone", 10) },
                // The hammer step pays for what it unlocks: a workbench costs 10 wood, and a
                // shelter around it a good deal more. Trimmed in alpha25 — these numbers were set
                // while the x3 resource rate was silently inert (see WorldModifiers.SetRate), so
                // they were compensating for a bug rather than balancing a reward.
                ["mq-hammer"] = new[] { ("Wood", 40), ("Stone", 15) },
                // Armor arrives a piece at a time across the Meadows steps rather than as one
                // handout, so each fight in the starter zone pays for itself (owner, alpha18:
                // "a few more steps in the starter zone, like kill boars, with armor and arrow
                // rewards"). Arrows come with every one of them — a bow the player cannot feed
                // is not a reward.
                ["mq-bench"] = new[] { ("ArmorLeatherChest", 1) },
                ["mq-boar"] = new[] { ("ArmorLeatherLegs", 1), ("ArrowWood", 50) },
                ["mq-home"] = new[] { ("ShieldWood", 1), ("ArrowFlint", 20) },
                ["mq-grey"] = new[] { ("HelmetLeather", 1), ("CapeDeerHide", 1), ("ArrowFlint", 30) },
                ["mq-shelter"] = new[] { ("Wood", 40), ("Stone", 20) },
                // The homestead steps each pay for the next one. The hide matters most: a bed
                // wants deer hide, the bed step lands at #9, and the chain does not otherwise hand
                // over a hide until the deer hunt at #12 — without this the player is sent off to
                // farm deer for a step that is meant to take two minutes.
                ["mq-fire"] = new[] { ("DeerHide", 6), ("Flint", 10) },
                // Raw meat, so the station has something on it the moment it is built rather than
                // sending the player back out to hunt before they can use what they just made.
                ["mq-cook"] = new[] { ("RawMeat", 8) },
                ["mq-bed"] = new[] { ("Wood", 30), ("Resin", 10) },
                ["mq-chest"] = new[] { ("ArrowFlint", 30) },
                ["mq-rest"] = new[] { ("CookedMeat", 10), ("ArrowFlint", 20) },
                // Eikthyr's altar wants two deer trophies, and a trophy is a drop the player can
                // hunt for an hour without seeing. Handing them over is the point of this step:
                // the run gates on the FIGHT, never on drop luck.
                ["mq-deer"] = new[] { ("TrophyDeer", 2), ("DeerHide", 5) },
                ["mq-herald"] = new[] { ("BowFineWood", 1), ("TrophyDeer", 2), ("ArrowFlint", 40) },

                // The discovery steps. Each pays what its boss fight actually WANTS, which is the
                // point of putting a step between finding the altar and fighting what stands on it:
                // arriving is the moment to be handed the thing you would otherwise have gone home
                // for.
                ["mq-find"] = new[] { ("TrophyDeer", 2), ("ArrowFlint", 40) },
                ["bf-find"] = new[] { ("AncientSeed", 3), ("ArrowBronze", 40) },
                ["sw-find"] = new[] { ("WitheredBone", 3), ("MeadPoisonResist", 5) },
                ["mt-find"] = new[] { ("DragonEgg", 3), ("MeadFrostResist", 5) },
                ["pl-find"] = new[] { ("GoblinTotem", 5), ("MeadHealthMedium", 5) },
                ["mq-eikthyr"] = new[] { ("PickaxeAntler", 1) },

                // Act II. Ore rather than ingots where the act is about learning to smelt, ingots
                // where it is about getting on with it — bf-copper pays raw so the smelter step has
                // a reason to exist, bf-smelter pays raw again to feed it, and bf-bronze pays
                // finished bronze so the armour is a decision rather than another smelting trip.
                ["bf-arrive"] = new[] { ("Torch", 1), ("ArrowFlint", 40) },
                // Surtling cores here, NOT at the portal step two links later: a smelter needs
                // them and they come from burial chambers, so without this the step quietly
                // sends the player crypt-hunting. Same rule as the deer trophies and ancient
                // seeds — the run gates on the FIGHT, never on a scavenger hunt.
                ["bf-copper"] = new[] { ("CopperOre", 20), ("TinOre", 10), ("Coal", 20), ("SurtlingCore", 8) },
                ["bf-portal"] = new[] { ("FineWood", 20), ("SurtlingCore", 4), ("GreydwarfEye", 10) },
                ["bf-smelter"] = new[] { ("CopperOre", 30), ("TinOre", 15), ("Coal", 30) },
                ["bf-bronze"] = new[] { ("Bronze", 10), ("ArrowBronze", 40) },
                ["bf-greydwarf"] = new[] { ("ShieldBronzeBuckler", 1), ("ArrowBronze", 40) },
                ["bf-brute"] = new[] { ("ArmorRootChest", 1), ("ArmorRootLegs", 1) },
                // The seeds are the point: the Elder's altar wants three, they drop from shamans
                // and brutes, and an act finale must never gate on drop luck (see mq-deer).
                ["bf-troll"] = new[] { ("TrollHide", 10), ("AncientSeed", 3) },
                ["bf-elder"] = new[] { ("SwampKey", 1) },

                // Acts III-V, thin like their chains. Each pays the next step's tedious part and
                // the pre-boss step pays that boss's summoning items, on the Act I pattern.
                ["sw-arrive"] = new[] { ("MeadPoisonResist", 5) },
                ["sw-draugr"] = new[] { ("ArrowIron", 40), ("ShieldIronTower", 1) },
                ["sw-fermenter"] = new[] { ("Honey", 20), ("Thistle", 20) },
                ["sw-blob"] = new[] { ("WitheredBone", 3), ("MeadPoisonResist", 5) },
                ["sw-iron"] = new[] { ("Iron", 30), ("Coal", 30) },
                ["sw-leech"] = new[] { ("MaceIron", 1) },
                ["sw-bonemass"] = new[] { ("Wishbone", 1) },

                ["mt-arrive"] = new[] { ("MeadFrostResist", 5), ("WolfPelt", 10) },
                ["mt-wolf"] = new[] { ("ArmorWolfChest", 1), ("ArmorWolfLegs", 1) },
                ["mt-drake"] = new[] { ("ArrowFrost", 40), ("DragonTear", 2) },
                ["mt-silver"] = new[] { ("Silver", 30), ("Coal", 30) },
                ["mt-golem"] = new[] { ("Crystal", 10), ("SwordSilver", 1) },
                ["mt-fenring"] = new[] { ("DragonEgg", 3) },
                ["mt-moder"] = new[] { ("DragonTear", 5) },

                ["pl-arrive"] = new[] { ("ArmorPaddedCuirass", 1), ("ArmorPaddedGreaves", 1) },
                ["pl-fuling"] = new[] { ("BlackMetal", 20), ("ArrowNeedle", 40) },
                ["pl-windmill"] = new[] { ("Barley", 30), ("BarleyFlour", 20) },
                ["pl-squito"] = new[] { ("SwordBlackmetal", 1) },
                ["pl-lox"] = new[] { ("LoxMeat", 10), ("CapeLox", 1) },
                ["pl-berserker"] = new[] { ("GoblinTotem", 5) },
            };

        /// <summary>
        /// Heat granted by a questline step. Flat and hardcoded rather than config-driven: the
        /// questline's real payment is the items, and the heat is only there so finishing a step
        /// still nudges the world's difficulty the way finishing a task does.
        /// </summary>
        private const float MainQuestHeatReward = 1f;

        /// <summary>
        /// The boon pool.
        ///
        /// Rebalanced in alpha34 (owner, on playing alpha33: "we need more boon types. There are
        /// like three sta ones, and they seem a bit lack luster since we already regen quite fast").
        /// It was five: Enduring, Vigorous, Cat's Breath, Marathoner and Acrobat — five of seventeen
        /// slots spent on a problem the run's BASELINE already solves, since every run starts with
        /// move stamina x0.5, regen x2.5 and all costs x0.75. They are now one boon, Tireless, worth
        /// picking on its own, and the four freed slots went on categories the pool had none of.
        /// </summary>
        internal static List<BoonDefinition> DefaultBoons() => new List<BoonDefinition>
        {
            new BoonDefinition { Id = "fleet", Display = "Fleet-footed", IsPassive = true,  Description = "Move and run faster." },
            new BoonDefinition { Id = "sharp", Display = "Sharpened",    IsPassive = true,  Description = "Your weapons deal 20% more damage." },
            new BoonDefinition { Id = "brother", Display = "Packbrother", IsPassive = false, CooldownSeconds = 240f, Description = "Summon a wolf to fight for you. Two at a time." },
            new BoonDefinition { Id = "mule",  Display = "Packmule",     IsPassive = true,  Description = "Carry 100 more weight." },
            new BoonDefinition { Id = "hearty", Display = "Hearty",      IsPassive = true,  Description = "+15 max health." },
            new BoonDefinition { Id = "tireless", Display = "Tireless",  IsPassive = true,  Description = "+25 max stamina, faster recovery, cheaper dodges." },
            new BoonDefinition { Id = "woodsman", Display = "Woodsman", IsPassive = true, Description = "Woodcutting skill to 60. Trees fall fast." },
            new BoonDefinition { Id = "hunter",   Display = "Hunter",   IsPassive = true, Description = "Bow skill to 50. Straighter, harder shots." },
            new BoonDefinition { Id = "warrior",  Display = "Warrior",  IsPassive = true, Description = "Axe, sword and club skill to 50." },

            // --- Resistances (alpha34) ---
            //
            // Gated, because they are BIOME-SHAPED: frost resistance in the Meadows is a wasted
            // pick, and an offer only holds three options. The gates are one biome early on
            // purpose — being handed the swamp's answer while finishing the Black Forest is
            // preparation, whereas being handed it in the swamp is a rescue.
            new BoonDefinition { Id = "irongut",   Display = "Irongut",      IsPassive = true, MinBosses = 1, Description = "Resistant to poison." },
            new BoonDefinition { Id = "coldblood", Display = "Coldblooded",  IsPassive = true, MinBosses = 2, Description = "Resistant to frost." },
            new BoonDefinition { Id = "fireblood", Display = "Fire-blooded", IsPassive = true, MinBosses = 2, Description = "Resistant to fire." },

            // --- On-kill (alpha34) ---
            //
            // The first boons that reward AGGRESSION rather than raising a stat. They ride the
            // Character death hook the questline already uses, so they cost nothing structurally.
            new BoonDefinition { Id = "bloodthirst", Display = "Bloodthirst", IsPassive = true, Description = "Every kill heals you." },
            new BoonDefinition { Id = "relentless",  Display = "Relentless",  IsPassive = true, Description = "Every kill restores stamina." },

            // --- Risk (alpha34) ---
            //
            // The first boons that COST something. Every other boon in the pool is pure gain, which
            // makes an offer a question of which number goes up; these make it a decision. Both
            // spell the cost out in the description — a downside the player did not see coming
            // would be a different thing entirely.
            new BoonDefinition { Id = "glasscannon", Display = "Glass Cannon", IsPassive = true, Description = "+40% weapon damage. -30% max health." },
            new BoonDefinition { Id = "reckless",    Display = "Reckless",     IsPassive = true, Description = "+50% weapon damage. You take 25% more." },

            // --- Heat (alpha34) ---
            //
            // Boons that engage the mode's own difficulty dial. Slow Burn buys slack; Forge-fed
            // rewards running hot, which turns "work both quest tracks" from merely harder into a
            // build.
            new BoonDefinition { Id = "slowburn", Display = "Slow Burn", IsPassive = true, Description = "Heat rises 25% slower." },
            new BoonDefinition { Id = "forgefed", Display = "Forge-fed", IsPassive = true, Description = "Your weapons hit harder the hotter the run." },

            new BoonDefinition { Id = "wind",  Display = "Second Wind",  IsPassive = false, CooldownSeconds = 120f, Description = "Heals you and nearby allies for 10s." },
            new BoonDefinition { Id = "ember", Display = "Emberskin",    IsPassive = false, CooldownSeconds = 180f, Description = "Cloak of flames burns nearby foes for 30s." },
            new BoonDefinition { Id = "way",   Display = "Waystone",     IsPassive = false, Description = "Teleport to the next boss altar. One charge." },
            new BoonDefinition { Id = "windfall", Display = "Windfall",  IsPassive = false, Description = "Double every stack you carry. One charge, never refills." },
        };
    }
}
