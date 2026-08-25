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
        private const string MultiplayerNotice = "The saga supports local/hosted worlds only.";
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
        private SpiritChase _spirit;
        private StolenLights _lights;
        private TheGatherer _gatherer;
        private ForestWatch _forest;
        private FenWatch _fen;

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

        /// <summary>The homestead's answer to splits — see <see cref="HearthRecords"/>.</summary>
        private readonly HearthRecords _records = new HearthRecords();

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
        /// A finished sentence pointing at whatever the questline currently wants found, or null
        /// when it wants nothing findable. The HUD prints it verbatim under the strip.
        ///
        /// Two things qualify. The Herald — a named creature somewhere in a 250m radius with no
        /// direction is a search rather than a hunt. And the biome an act opens on: "Reach the
        /// Black Forest" was the last step in the saga that told you WHAT to find and nothing
        /// about WHERE, which on a world where the forest happens to lie behind you is a step
        /// that gets solved by walking in circles.
        ///
        /// The Herald wins when both apply: it moves, it expires with the act, and it is the
        /// only one of the two you can lose.
        /// </summary>
        public string QuestBearing
        {
            get
            {
                if (!_active) return null;

                var player = Player.m_localPlayer;
                if (player == null) return null;

                // Outranks every bearing: a direction is no use while the thing it points at
                // cannot be caught, and "the hunt refuses to count" is the single most
                // bug-looking thing this act can do.
                if (ActIsMeadows && DarkStepWanted && !IsNight)
                    return "Nothing you seek walks in the light. Wait for dark.";

                if (_spirit != null && ActIsMeadows && SpiritWanted)
                {
                    string rumour = _spirit.Bearing(player);
                    if (!string.IsNullOrEmpty(rumour)) return rumour;
                }

                if (_deer != null && ActIsMeadows && HeraldWanted)
                {
                    string herald = _deer.HeraldBearing(player);
                    if (!string.IsNullOrEmpty(herald)) return $"The Herald\u2019s tracks lead {herald}";
                }

                return BiomeBearing(player);
            }
        }

        /// <summary>Whether the world is in night. Static on EnvMan; false when it is not loaded.</summary>
        private static bool IsNight
        {
            get
            {
                try { return EnvMan.IsNight(); }
                catch { return false; }
            }
        }

        /// <summary>
        /// Act I's opening mystery: a light that knows where Eikthyr is.
        ///
        /// Ticked only while its step is the one in play, so nothing drifts about the meadows
        /// before the saga has asked for it or after it has been found.
        /// </summary>
        private void PollSpirit()
        {
            if (_spirit == null || !ActIsMeadows || _challenges == null) return;

            if (!SpiritWanted) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            try
            {
                bool wasVisible = _spirit.Spawned;

                if (_spirit.Tick(player))
                    Message("The light goes out. You know where to go.");

                // Said the moment it exists, so nobody walks into it unaware. It is placed at the
                // edge of vision at night, which is atmospheric right up until you finish the step
                // without ever noticing what you were walking toward.
                if (!wasVisible && _spirit.Spawned)
                    Announce("A pale light kindles ahead.");

                if (_spirit.Found)
                    _challenges.ReportMeasure(ChallengeKind.PlayerState, SpiritChase.FoundMeasure, 1f);
            }
            catch (Exception ex) { LogOnce("spirit-chase", ex); }
        }

        /// <summary>
        /// True while a step that only the DARK can finish is in play — the pale light, the night
        /// hunt, the Herald.
        ///
        /// A player has no way to know a step is night-gated by reading it, and a hunt that
        /// silently refuses to count is indistinguishable from a broken one. So the strip says so
        /// outright and the whispers say it in the act's own voice.
        /// </summary>
        private bool DarkStepWanted =>
            _challenges != null && _challenges.Tracks.Any(t =>
                t.Current != null && !t.Blocked &&
                ((t.Current.Def.Kind == ChallengeKind.PlayerState &&
                  t.Current.Def.Param == SpiritChase.FoundMeasure) ||
                 (t.Current.Def.Kind == ChallengeKind.PlayerEvent &&
                  t.Current.Def.Param == StolenLights.TakenEvent) ||
                 (t.Current.Def.Kind == ChallengeKind.KillPrefab &&
                  (t.Current.Def.Param == DeerHerd.NightDeerKillName ||
                   t.Current.Def.Param == DeerHerd.HeraldKillName))));

        /// <summary>
        /// Steps whose reward has been forfeited. Completing one still ADVANCES the track — a
        /// blocked track is the worst thing this design can do, and losing a race should cost the
        /// prize, not the campaign.
        /// </summary>
        private readonly HashSet<string> _forfeited = new HashSet<string>();

        /// <summary>Whether the Gatherer's coming has been announced this appearance of its step.</summary>
        private bool _gathererForetold;

        /// <summary>
        /// Ends the light race badly, if the forest has taken enough.
        ///
        /// The hunt completes anyway and pays NOTHING. What is lost is deer trophies — Eikthyr's
        /// summoning items — so the run is not stuck, it is just doing the rest the hard way. That
        /// is the whole shape of a good failure here: expensive, visible, and survivable (owner:
        /// "just the step then, not the whole run. Maybe you just wont get the reward").
        /// </summary>
        private void PollLightForfeit()
        {
            if (_lights == null || _challenges == null || !ActIsMeadows) return;
            if (_lights.Lost < Mathf.Max(1, _cfg.RunLightForfeitLost)) return;

            var step = _challenges.Tracks
                .Select(t => t.Current)
                .FirstOrDefault(c => c != null &&
                                     c.Def.Kind == ChallengeKind.PlayerEvent &&
                                     c.Def.Param == StolenLights.TakenEvent);

            if (step == null || _forfeited.Contains(step.Def.Id)) return;

            _forfeited.Add(step.Def.Id);

            Announce("The forest took more than you did.");
            Message("The lights are gone, and the trophies with them. Hunt your own.");

            // Advance by reporting the remainder. The completion path is the ordinary one, so
            // nothing downstream needs to know this was a loss — except the reward grant, which
            // checks the forfeit set.
            int remaining = Mathf.CeilToInt(step.Def.Target - step.Progress);
            for (int i = 0; i < remaining; i++)
                _challenges.ReportEvent(ChallengeKind.PlayerEvent, StolenLights.TakenEvent);
        }

        private bool _raceMusicPlayed;

        /// <summary>
        /// Fires the race's music cue through the game's own trigger-music path.
        ///
        /// The name is ASSET DATA (the music table is configured in Unity, invisible to this
        /// assembly), so run-start validation logs whether it resolves — see the probe beside the
        /// fish and spirit checks. TriggerMusic is fire-and-forget: a wrong name plays nothing and
        /// throws nothing, which without the probe would be indistinguishable from working.
        /// </summary>
        private void PlayRaceMusic()
        {
            string cue = _cfg.RunLightMusic;
            if (string.IsNullOrEmpty(cue)) return;

            try { MusicMan.instance?.TriggerMusic(cue); }
            catch (Exception ex) { LogOnce("race-music", ex); }
        }

        /// <summary>
        /// Sends the Gatherer in once its step is in play.
        ///
        /// Unlike the Herald there is no bearing and no search: it spawns beside the player, which
        /// also means it can never be placed somewhere unloaded — the bug that made the Herald
        /// unfindable for two versions.
        /// </summary>
        private void PollGatherer()
        {
            if (_gatherer == null || !ActIsMeadows || _challenges == null) return;

            bool wanted = _challenges.Tracks.Any(t =>
                t.Current != null && !t.Blocked &&
                t.Current.Def.Kind == ChallengeKind.KillPrefab &&
                t.Current.Def.Param == TheGatherer.KillName);

            if (!wanted) { _gathererForetold = false; return; }

            // Foretold ONCE, the moment its step opens — even by day, when it will not come yet.
            // The Herald's fall and the arrival can be hours apart, and a named enemy nobody has
            // heard of arriving unannounced reads as a random spawn rather than a reckoning
            // (owner: "there was no intro message for the harvester").
            if (!_gathererForetold)
            {
                _gathererForetold = true;
                Announce($"{TheGatherer.Name} knows what you have taken.");
                Message("It gathers only in the dark. Be ready when night falls.");
            }

            var player = Player.m_localPlayer;
            if (player == null) return;

            // The Gatherer keeps the act's rule too: it has been following the hunt, and the hunt
            // happens in the dark. Arriving at noon would also squander the arrival — a heavy
            // shape coming through the trees is a different event at night.
            if (!IsNight) return;

            try
            {
                if (_gatherer.TryArrive(player, _lights?.Lost ?? 0))
                {
                    Announce("Something heavy is coming through the trees.");
                    Message(_lights != null && _lights.Lost > 0
                        ? $"{TheGatherer.Name} has followed your hunt all this time — fat on {_lights.Lost} lights you let go."
                        : $"{TheGatherer.Name} has followed your hunt all this time, and taken nothing.");
                }
            }
            catch (Exception ex) { LogOnce("gatherer", ex); }
        }

        /// <summary>
        /// The race at the carcass. Reaching a light takes it back; letting it fade gives it to
        /// the forest, which is the whole story of the act said in one line at the moment it
        /// stings.
        /// </summary>
        private void PollLights()
        {
            if (_lights == null || !ActIsMeadows || _challenges == null) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            try
            {
                // The race gets its own music. Triggered on the 0 -> some edge, so one cue per
                // engagement rather than one per deer; trigger music ends on its own, which is
                // why there is no stop call.
                if (_lights.Burning > 0 && !_raceMusicPlayed)
                {
                    _raceMusicPlayed = true;
                    PlayRaceMusic();
                }
                else if (_lights.Burning == 0)
                {
                    _raceMusicPlayed = false;
                }

                int lost;
                int taken = _lights.Tick(player, out lost);

                for (int i = 0; i < taken; i++)
                    _challenges.ReportEvent(ChallengeKind.PlayerEvent, StolenLights.TakenEvent);

                if (taken > 0) Message("You take the light back.");
                if (lost > 0) Message("The forest takes it.");
            }
            catch (Exception ex) { LogOnce("stolen-lights", ex); }
        }

        private float _whisperAt;

        /// <summary>
        /// Lines for the wait, and lines for the hunt.
        ///
        /// NONE of them may contain a direction or a place. "Something moves between the trees"
        /// sent a player looking through the trees, because that is what it says — atmosphere that
        /// sounds like a hint is worse than no atmosphere at all. These carry mood and a rule
        /// (wait for dark); the bearing and the bar carry position, and they are the only things
        /// that do.
        /// </summary>
        private static readonly string[] DaylightWhispers =
        {
            "The meadows are bright and empty. Whatever you want is asleep.",
            "Nothing answers in daylight.",
            "The antlered one keeps his own hours.",
            "You will find nothing until the sun is down.",
        };

        private static readonly string[] NightWhispers =
        {
            "The dark is awake.",
            "You are not the only thing hunting tonight.",
            "The forest is counting.",
            "Something is owed, and it is being collected.",
            "They are quicker than you in the dark.",
        };

        /// <summary>
        /// Builds every per-act system: the herd, the chase, the lights, the Gatherer, and the two
        /// biome watchers.
        ///
        /// ONE method, called from both StartRun and the resume path. It was two lists, and the
        /// resume one had only the herd — so a run continued after quitting had no pale light, no
        /// deer lights, no Gatherer and no atmosphere in any act. All of them are null-guarded at
        /// every call site, so nothing threw; the act simply went quiet, which is the worst way for
        /// this to fail because it looks like a design choice.
        ///
        /// Anything added here must be reachable from a resume, or it only exists for players who
        /// never close the game.
        /// </summary>
        private void BuildActSystems()
        {
            _deer = new DeerHerd(_cfg, _rng);
            _spirit = new SpiritChase(_cfg, _rng);
            _lights = new StolenLights(_cfg);
            _gatherer = new TheGatherer(_cfg, _rng);
            _forest = new ForestWatch(_cfg, _rng);
            _fen = new FenWatch(_cfg, _rng);
        }

        /// <summary>
        /// Occasional atmosphere while a dark step is in play, day or night.
        ///
        /// Rare on purpose — roughly once a minute and a half. A line every few seconds stops
        /// being unsettling and becomes chatter, and the strip is already carrying the rule.
        /// </summary>
        private void PollWhispers()
        {
            if (!ActIsMeadows || !DarkStepWanted) return;
            if (Time.time < _whisperAt) return;

            _whisperAt = Time.time + Mathf.Max(30f, _cfg.RunWhisperSeconds);

            // When a light is actually being chased, the whisper carries the distance band rather
            // than a random mood. Atmosphere that looks like information and is not is worse than
            // silence — it was read as a proximity hint, because that is what it sounds like.
            if (IsNight && _spirit != null && SpiritWanted)
            {
                var player = Player.m_localPlayer;
                string rumour = player == null ? null : _spirit.Bearing(player);
                if (!string.IsNullOrEmpty(rumour)) { Message(rumour); return; }
            }

            var lines = IsNight ? NightWhispers : DaylightWhispers;
            Message(lines[_rng.Next(lines.Length)]);
        }

        /// <summary>True while a deer-hunt step is the one in play, day or night.</summary>
        private bool DeerHuntWanted =>
            _challenges != null && _challenges.Tracks.Any(t =>
                t.Current != null && !t.Blocked &&
                // The LIGHT RACE is the deer hunt — it is a PlayerEvent, not a kill, and this
                // gate not knowing that is why the act's centrepiece went dead: no light on a
                // deer's fall and no pack, because both are keyed here. The alpha61 edit that was
                // meant to add this branch silently missed (a replace with no assert), so the
                // gate said KillPrefab-only from the day the step stopped being one.
                ((t.Current.Def.Kind == ChallengeKind.PlayerEvent &&
                  t.Current.Def.Param == StolenLights.TakenEvent) ||
                 (t.Current.Def.Kind == ChallengeKind.KillPrefab &&
                  (t.Current.Def.Param == DeerHerd.DeerPrefab ||
                   t.Current.Def.Param == DeerHerd.NightDeerKillName ||
                   t.Current.Def.Param == DeerHerd.HeraldKillName))));

        /// <summary>
        /// True while the pale-light step is the one in play.
        ///
        /// Every spirit-facing surface — the rumour, the closeness bar, the whispers — keys on
        /// THIS, never on SpiritChase.Found. The two can disagree: a dev-skip completes the STEP
        /// without the spirit ever being reached, and every surface keyed on Found then kept
        /// pointing at a chase that no longer ticks. On the Herald step that shadowed the
        /// Herald's own bearing entirely, showed "It is here. Very close." with a full bar, and
        /// there was nothing there.
        /// </summary>
        private bool SpiritWanted =>
            _challenges != null && _challenges.Tracks.Any(t =>
                t.Current != null && !t.Blocked &&
                t.Current.Def.Kind == ChallengeKind.PlayerState &&
                t.Current.Def.Param == SpiritChase.FoundMeasure);

        private bool HeraldWanted =>
            _challenges != null && _challenges.Tracks.Any(t =>
                t.Current != null &&
                t.Current.Def.Kind == ChallengeKind.KillPrefab &&
                t.Current.Def.Param == DeerHerd.HeraldKillName);

        /// <summary>Cache for the biome search; see <see cref="NearestBiome"/>.</summary>
        private Heightmap.Biome _bearingBiome = (Heightmap.Biome)(-1);
        private Vector3 _bearingFrom;
        private float _bearingAt = float.NegativeInfinity;
        private Vector3? _bearingResult;
        private const float BearingRefreshSeconds = 5f;
        private const float BearingMoveThreshold = 40f;

        /// <summary>Direction and distance to the biome an open ReachBiome step names.</summary>
        private string BiomeBearing(Player player)
        {
            if (_challenges == null) return null;

            var step = _challenges.Tracks
                .Select(t => t.Current)
                .FirstOrDefault(c => c != null && c.Def.Kind == ChallengeKind.ReachBiome);
            if (step == null) return null;

            Heightmap.Biome target;
            if (!Enum.TryParse(step.Def.Param, out target)) return null;

            Vector3 here = player.transform.position;

            try
            {
                // Standing in it already: the step is about to tick over on its own, and
                // "The Black Forest lies north, 60m" while surrounded by black forest reads as a bug.
                if (WorldGenerator.instance != null && WorldGenerator.instance.GetBiome(here) == target)
                    return null;
            }
            catch
            {
                return null;
            }

            var found = NearestBiome(here, target);
            if (found == null) return null;

            Vector3 delta = found.Value - here;
            delta.y = 0f;

            float distance = delta.magnitude;
            if (distance < 1f) return null;

            return $"{BiomeCompass.FriendlyName(target)} lies {BiomeCompass.Compass(delta)}, " +
                   $"{Mathf.Round(distance / 10f) * 10f:0}m";
        }

        /// <summary>
        /// The cached nearest-biome search.
        ///
        /// The search costs a couple of thousand noise lookups and this is read from OnGUI, so it
        /// must not run per frame. What it caches is a PLACE, not a direction — the place stays
        /// true while you walk toward it, and the bearing recomputes from it every frame for
        /// free. That is the same fix the Herald needed: a remembered target is stable, whereas
        /// re-deciding where to point every few seconds is what makes a bearing jump.
        /// </summary>
        private Vector3? NearestBiome(Vector3 from, Heightmap.Biome target)
        {
            bool stale = target != _bearingBiome
                || Time.time - _bearingAt > BearingRefreshSeconds
                || (from - _bearingFrom).sqrMagnitude > BearingMoveThreshold * BearingMoveThreshold;

            if (stale)
            {
                _bearingBiome = target;
                _bearingAt = Time.time;
                _bearingFrom = from;

                try { _bearingResult = BiomeCompass.Nearest(from, target); }
                catch { _bearingResult = null; }
            }

            return _bearingResult;
        }

        /// <summary>The light race's scoreboard, for the HUD. Null outside a run.</summary>
        public int LightsTaken => _active && _lights != null ? _lights.Taken : 0;
        public int LightsLost => _active && _lights != null ? _lights.Lost : 0;

        /// <summary>
        /// How close the pale light is, 0 (far) to 1 (on top of it), or -1 when nothing is being
        /// chased. Drawn as a bar, because the rumour text is deliberately vague and vague prose
        /// cannot tell warm from cold.
        /// </summary>
        public float SpiritCloseness
        {
            get
            {
                if (!_active || _spirit == null || !ActIsMeadows || !SpiritWanted) return -1f;

                var player = Player.m_localPlayer;
                if (player == null) return -1f;

                float d = _spirit.DistanceFrom(player);
                return d < 0f ? -1f : 1f - Mathf.Clamp01(d / SpiritChase.Reach);
            }
        }

        /// <summary>Fraction of burn time left on the most urgent light, or -1 when none burns.</summary>
        public float LightUrgency
        {
            get
            {
                if (!_active || _lights == null || _lights.Burning == 0) return -1f;

                float span = Mathf.Max(1f, _cfg.RunLightFadeSeconds);
                return Mathf.Clamp01(_lights.Soonest / span);
            }
        }

        public int LightsBurning => _active && _lights != null ? _lights.Burning : 0;

        /// <summary>True when testing shortcuts are live. The HUD says so; see HandleDevInput.</summary>
        public bool DevMode => _cfg != null && _cfg.RunDevMode;

        public HearthRecords Records => _records;

        public int HomewardCharges => _active ? _homewardCharges : 0;
        public float EarnedHealth => _active ? _taskHealthReward : 0f;

        /// <summary>The transition card: the window draws it large for a few seconds. See RefreshAct.</summary>
        public string ActCardTitle { get; private set; }
        public string ActCardEpigraph { get; private set; }
        public float ActCardShownAt { get; private set; } = float.NegativeInfinity;

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
                    Announce("The saga needs a spawned character.");
                    return;
                }

                var zone = ZoneSystem.instance;
                if (zone == null)
                {
                    Announce("The saga could not reach the world (ZoneSystem missing).");
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
                    Message("The final boss is already dead on this world — begin the saga on a fresh one.");
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
                BuildActSystems();

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
                _spirit?.Reset();
                _lights?.Reset();
                _gatherer?.Reset();
                _forest?.Reset();
                _fen?.Reset();
                _unbaselinedSeen.Clear();
                _warnedUnbaselined.Clear();
                _taskHealthReward = 0f;
                _homewardCharges = 0;
                _discovered.Clear();
                _pinnedActIndex = -1;
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
                Message("The saga begins. Good luck.");
            }
            catch (Exception ex)
            {
                LogOnce("start-run", ex);
                Announce("The saga failed to start — see the log.");
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
            HandleDevInput();

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
        /// <summary>
        /// Everything a dev kit needs and nothing a run should ever hand out. Names taken from the
        /// reward tables, so every one has already been vetted by the run-start validator — a dev
        /// tool that silently grants nothing wastes more time than it saves.
        /// </summary>
        private static readonly (string prefab, int count)[] DevKit =
        {
            ("Wood", 200), ("Stone", 200), ("Flint", 100), ("Resin", 100),
            ("DeerHide", 100), ("LeatherScraps", 100), ("TrophyDeer", 5),
            ("CopperOre", 100), ("TinOre", 100), ("Coal", 100), ("SurtlingCore", 30),
            ("BronzeNails", 100), ("IronNails", 100),
            ("CookedMeat", 30), ("ArrowFlint", 200), ("FishingRod", 1), ("FishingBait", 200),
            ("CarrotSeeds", 30), ("Carrot", 30), ("Honey", 50), ("Raspberry", 50), ("Mushroom", 50),
        };

        /// <summary>
        /// The fighter's half of the dev kit: bronze arms, and the best food the spoils tables
        /// know — top-tier food is where Valheim HP actually comes from, so "more hp" means
        /// eating better, not a bigger chestpiece.
        ///
        /// The bronze names are the one place the kit steps outside the vetted reward tables.
        /// That is acceptable ONLY because GrantItem fails loudly on an unknown name — a dev tool
        /// that silently grants nothing wastes more time than it saves, but one that says so in
        /// the log is a one-word fix.
        /// </summary>
        private static readonly (string prefab, int count)[] DevArmory =
        {
            ("SwordBronze", 1), ("MaceBronze", 1), ("ShieldBronzeBuckler", 1),
            ("ArmorBronzeChest", 1), ("ArmorBronzeLegs", 1), ("HelmetBronze", 1),
            ("CapeDeerHide", 1), ("BowFineWood", 1), ("ArrowBronze", 200),
            ("SerpentStew", 10), ("BloodPudding", 10), ("Sausages", 10),
            ("MeadHealthMedium", 10),
        };

        /// <summary>
        /// Testing shortcuts, behind <see cref="IConfiguration.RunDevMode"/>.
        ///
        /// Keys nothing else uses — the numeric keypad's digits are all spoken for by boon offers,
        /// boon activations and Homeward, so these live on the operators.
        ///
        ///   Keypad +   complete the step in play on every track
        ///   Keypad -   push the clock forward two hours, for the night-gated hunt
        ///   Keypad *   a chest's worth of materials
        ///   Keypad .   drop a deer's light at your feet
        ///   Keypad /     god mode + armory (toggle)
        ///   Keypad Enter gate to the claimed bed, free
        ///
        /// The last one matters more than it looks: the light race is the hardest thing in the act
        /// to reach — kill a deer, at night, while a step is active — and testing the bar, the
        /// timer and the pickup should not require all three.
        /// </summary>
        /// <summary>
        /// The dev speed boost: originals captured once, restored on demand.
        ///
        /// A loan like everything else this mode lends, just a hand-rolled one — the fields
        /// re-initialise from the prefab on relog, so even a leaked loan self-heals, but restoring
        /// properly is what keeps a dev session honest with itself. NOT the legacy SetSpeed, which
        /// writes absolute values over walk, run and jump with no way back.
        /// </summary>
        private bool _devSpeedOn;
        private float _devRunSpeed, _devWalkSpeed;

        private const float DevSpeedFactor = 1.75f;

        private void DevApplySpeed(Player player)
        {
            if (_devSpeedOn || player == null) return;

            _devRunSpeed = player.m_runSpeed;
            _devWalkSpeed = player.m_walkSpeed;
            player.m_runSpeed *= DevSpeedFactor;
            player.m_walkSpeed *= DevSpeedFactor;
            _devSpeedOn = true;
        }

        private void DevRestoreSpeed()
        {
            if (!_devSpeedOn) return;

            var player = Player.m_localPlayer;
            if (player != null)
            {
                player.m_runSpeed = _devRunSpeed;
                player.m_walkSpeed = _devWalkSpeed;
            }

            _devSpeedOn = false;
        }

        private void HandleDevInput()
        {
            if (_cfg == null || !_cfg.RunDevMode || !_active || _frozen) return;

            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                _challenges?.DevCompleteCurrent();
                Message("DEV: current steps completed.");
            }
            else if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                DevAdvanceClock();
            }
            else if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                // Into the STASH, not the inventory. The kit's raw materials alone are several
                // hundred weight — granted to the pockets it left the tester over-encumbered on
                // the spot, which is the opposite of a convenience key. The stash is also simply
                // where a pile of materials belongs; take out what the moment needs.
                foreach (var entry in DevKit) _stash.Deposit(entry.prefab, entry.count, 1, 0);
                SaveState();
                Message($"DEV: {DevKit.Length} materials in the stash.");
            }
            else if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                // God mode, plus a fighter's kit. GM commands are gated off during a run
                // (InputManager.Gate), which is correct for play and wrong for testing — so the
                // dev key calls the service directly rather than un-gating the whole cheat
                // window. Toggle, so it can be turned OFF to test dying too.
                try
                {
                    var combat = ModBootstrap.GetService<ICombatService>();
                    if (combat != null)
                    {
                        combat.SetGodMode(!combat.GodMode);
                        if (combat.GodMode)
                        {
                            foreach (var entry in DevArmory) GrantItem(entry.prefab, entry.count);

                            // Faster legs belong to the god, not the quartermaster — one key for
                            // "make me untouchable", one for "fill my pockets", and the toggle
                            // gives the speed loan its natural way back.
                            DevApplySpeed(Player.m_localPlayer);
                            Message("DEV: god mode ON \u2014 armory granted, +75% speed.");
                        }
                        else
                        {
                            DevRestoreSpeed();
                            Message("DEV: god mode OFF, speed restored.");
                        }
                    }
                }
                catch (Exception ex) { LogOnce("dev-god", ex); }
            }
            else if (Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Homeward without the charge or the cooldown. Testing the homestead half of the
                // act means bouncing between the house and the hunt constantly, and the real
                // Homeward's economy is not the thing under test.
                try
                {
                    var profile = Game.instance?.GetPlayerProfile();
                    if (profile == null || !profile.HaveCustomSpawnPoint())
                    {
                        Message("DEV: no claimed bed to gate to.");
                    }
                    else
                    {
                        var teleport = ModBootstrap.GetService<ITeleportService>();
                        // Lifted clear of the ground, same as the real Homeward: arriving inside
                        // the terrain is how a teleport becomes a death.
                        teleport?.TeleportTo(profile.GetCustomSpawnPoint() + Vector3.up * 2f);
                        Message("DEV: home.");
                    }
                }
                catch (Exception ex) { LogOnce("dev-home", ex); }
            }
            else if (Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                var player = Player.m_localPlayer;
                if (_lights != null && player != null)
                {
                    Vector3 at = player.transform.position + player.transform.forward * 6f;
                    _lights.Release(at);

                    // WITH the pack, exactly as a real deer kill fires it. The key exists to test
                    // the race, and the race is light-versus-greydwarves — dropping the light
                    // alone tested a footrace against nobody.
                    if (_deer != null)
                    {
                        _deer.ContestEnabled = true;
                        _deer.Contest(at);
                    }

                    Message("DEV: a light rises, and the forest answers.");
                }
            }
        }

        /// <summary>
        /// Pushes the world clock forward two game hours.
        ///
        /// A fixed step per press rather than "skip to night": the caller can see what it did and
        /// press again, whereas a loop that hunts for nightfall depends on how EnvMan derives its
        /// time from the network clock, which is not worth being clever about in a test aid.
        /// </summary>
        private void DevAdvanceClock()
        {
            try
            {
                // Valheim's day is 1800 seconds, so an in-game hour is 75 of them.
                const double TwoHours = 150.0;

                var net = ZNet.instance;
                if (net == null) return;

                net.SetNetTime(net.GetTimeSeconds() + TwoHours);
                Message(IsNight ? "DEV: +2h — it is night." : "DEV: +2h — still light.");
            }
            catch (Exception ex)
            {
                LogOnce("dev-clock", ex);
            }
        }

        private void HandleBoonActivationInput()
        {
            if (_boons == null || _boons.CurrentOffer.Count > 0) return;

            if (Input.GetKeyDown(KeyCode.Keypad4)) TryActivateHeldBoon("wind");
            else if (Input.GetKeyDown(KeyCode.Keypad5)) TryActivateHeldBoon("ember");
            else if (Input.GetKeyDown(KeyCode.Keypad6)) TryActivateHeldBoon("way");
            else if (Input.GetKeyDown(KeyCode.Keypad7)) TryActivateHeldBoon("brother");
            else if (Input.GetKeyDown(KeyCode.Keypad8)) TryActivateHeldBoon("windfall");
            else if (Input.GetKeyDown(KeyCode.Keypad0)) TryActivateHeldBoon("bonecaller");
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
                // Only the item names actually being asked for; they are counted in a single pass.
                // Drawn from BOTH a simple challenge's own Kind/Param and a composite's Subs:
                // a composite's top-level Kind/Param are unused filler (see
                // ChallengeDefinition.Subs), so a "hold 25 wood" SUB would never be polled if this
                // only looked at the top level.
                // The questline's reserved slot is included for the same reason: it measures
                // through the very same reports, so anything it asks for has to be polled too.
                var wanted = MeasuredChallenges()
                    .SelectMany(CollectItemParams)
                    .Distinct()
                    .ToList();

                var held = CountHeld(inventory, wanted);
                foreach (var itemName in wanted)
                {
                    int count;
                    _challenges.ReportMeasure(ChallengeKind.CollectItem, itemName,
                        held.TryGetValue(itemName, out count) ? count : 0);
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
            PollPlayerState();
            PollMeals();
            PollSpirit();
            PollLights();
            PollLightForfeit();
            PollGatherer();
            PollWhispers();
            PollForest();
            RefreshActPin();
        }

        /// <summary>
        /// Feeds the homestead records from what the poll can already see.
        ///
        /// Everything here is a MAXIMUM the run reached, latched by HearthRecords, so losing the
        /// fish to a death or tearing the house down does not take the record with it.
        /// </summary>
        private void PollHearthRecords(Player player)
        {
            try
            {
                _records.Report(HearthRecords.Comfort, player.GetComfortLevel());
                _records.Report(HearthRecords.LargestPen, CountTamedNearby(player));

                // The stats are lifetime totals for the character, not this run's — but these are
                // records rather than scores, and "the most trophies this homestead ever had" is
                // the truer reading of the row anyway.
                _records.Report(HearthRecords.Trophies, ReadPlayerStat("ItemStandUses") ?? 0f);
                _records.Report(HearthRecords.NightsSlept, ReadPlayerStat("Sleep") ?? 0f);
                _records.Report(HearthRecords.Foraged, ReadPlayerStat("ItemsPickedUp") ?? 0f);

                var caught = FishHeld(player);
                if (caught.Total > 0) _records.Report(HearthRecords.BestHaul, caught.Total);

                // Marked here rather than at run end, so the star appears the moment the record
                // is beaten — which is when it means something.
                _records.MarkPersonalBests(PermanentRecord.GetRecordBests(player));

            }
            catch
            {
                // Flavour must never be the thing that breaks a poll.
            }
        }

        /// <summary>
        /// Reports the level of any skill a step asks about, as PlayerState "Skill:Name".
        ///
        /// Generic rather than fishing-specific because the game keeps a level for all of them and
        /// there is nothing fishing-shaped about the question — "Woodcutting 20" or "Bows 15" would
        /// work the same way the day someone wants them.
        ///
        /// Only the skills actually named by a step are read, so an unused skill costs nothing.
        /// </summary>
        private void ReportSkillLevels(Player player)
        {
            if (_challenges == null) return;

            foreach (var param in _skillParams)
            {
                Skills.SkillType type;
                if (!_skillTypes.TryGetValue(param, out type)) continue;

                try
                {
                    _challenges.ReportMeasure(ChallengeKind.PlayerState,
                        SkillParamPrefix + param, player.GetSkillLevel(type));
                }
                catch
                {
                    // A skill the build does not have simply goes unreported.
                }
            }
        }

        private const string SkillParamPrefix = "Skill:";

        /// <summary>Skill names any step asks for, resolved once. See <see cref="ReportSkillLevels"/>.</summary>
        private readonly List<string> _skillParams = new List<string>();
        private readonly Dictionary<string, Skills.SkillType> _skillTypes = new Dictionary<string, Skills.SkillType>();

        /// <summary>
        /// Works out which skills the content asks about, once per run.
        ///
        /// A name this build has no skill for is reported LOUDLY rather than silently skipped —
        /// that is the failure mode this mode keeps rediscovering, most recently with a step that
        /// was valid, measurable and impossible.
        /// </summary>
        private void ResolveSkillParams()
        {
            _skillParams.Clear();
            _skillTypes.Clear();

            foreach (var def in AllChallengeDefinitions())
            {
                if (def.Kind != ChallengeKind.PlayerState || string.IsNullOrEmpty(def.Param)) continue;
                if (!def.Param.StartsWith(SkillParamPrefix)) continue;

                string name = def.Param.Substring(SkillParamPrefix.Length);
                if (_skillTypes.ContainsKey(name)) continue;

                try
                {
                    _skillTypes[name] = (Skills.SkillType)Enum.Parse(typeof(Skills.SkillType), name, true);
                    _skillParams.Add(name);
                }
                catch
                {
                    Debug.LogError($"[ICanShowYouTheWorld] '{def.Display}' names skill '{name}', which this " +
                                   "build of Valheim does not have — the step can never complete.");
                }
            }
        }

        /// <summary>
        /// Things whose prefab name contains "Fish" but which are not a fish you caught.
        ///
        /// The tackle (rod, bait), and — less obviously — the PREPARED DISHES. A run-start dump of
        /// the real registry settled what is actually in there:
        ///
        ///     FishAndBread(1), FishAndBreadUncooked(1), FishAnglerRaw(0.5),
        ///     FishCooked(0.5), FishRaw(0.5), FishWraps(1)
        ///
        /// Three of those six are cooking recipes. Counting fish wraps as a catch would have let
        /// the larder finish a fishing step, and it inflated the species count enough that the
        /// impossible "3 species" step passed validation.
        /// </summary>
        private static bool IsFishingGear(string prefabName) =>
            prefabName.IndexOf("Rod", StringComparison.OrdinalIgnoreCase) >= 0 ||
            prefabName.IndexOf("Bait", StringComparison.OrdinalIgnoreCase) >= 0 ||
            prefabName.IndexOf("Trophy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            prefabName.IndexOf("AndBread", StringComparison.OrdinalIgnoreCase) >= 0 ||
            prefabName.IndexOf("Wraps", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>What the player is carrying, fish-wise. One scan, four questions.</summary>
        private struct FishCatch
        {
            public int Total;
            public int Species;
            public int Cooked;
            public float Heaviest;
            public string HeaviestName;
        }

        /// <summary>
        /// Reads the inventory for everything the fishing steps ask about.
        ///
        /// There is no fish stat and no trade stat, so matching the PREFAB name is the only honest
        /// measure. It covers every species and both raw and cooked without naming asset data the
        /// assembly cannot verify — the oldest landmine in this mode. The food test keeps the rod
        /// and the bait out: neither is edible.
        ///
        /// Species are counted by distinct prefab, because that is what a species IS here.
        /// </summary>
        private static FishCatch FishHeld(Player player)
        {
            var catch_ = new FishCatch { HeaviestName = string.Empty };

            try
            {
                var items = player.GetInventory()?.GetAllItems();
                if (items == null) return catch_;

                var species = new HashSet<string>();

                foreach (var item in items)
                {
                    if (item?.m_shared == null) continue;

                    var prefab = item.m_dropPrefab;
                    if (prefab == null || prefab.name.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // NOT filtered on m_food, which is what broke this: raw fish cannot be eaten in
                    // Valheim, so every fish actually caught scored zero and only cooked ones ever
                    // counted (owner: "catching a fish didn't register"). The food test was there to
                    // exclude the rod and the bait; excluding them by name is what it should always
                    // have done, since neither is a fish.
                    if (IsFishingGear(prefab.name)) continue;

                    catch_.Total += item.m_stack;
                    species.Add(prefab.name);

                    if (prefab.name.IndexOf("Cooked", StringComparison.OrdinalIgnoreCase) >= 0)
                        catch_.Cooked += item.m_stack;

                    if (item.m_shared.m_weight > catch_.Heaviest)
                    {
                        catch_.Heaviest = item.m_shared.m_weight;
                        try { catch_.HeaviestName = Localization.instance.Localize(item.m_shared.m_name); }
                        catch { catch_.HeaviestName = prefab.name; }
                    }
                }

                catch_.Species = species.Count;
            }
            catch
            {
                // A poll that cannot read the inventory reports nothing, not a wrong number.
            }

            return catch_;
        }

        /// <summary>
        /// Tamed creatures penned near the player.
        ///
        /// Counted within a radius rather than world-wide because the step is "a pen of three" —
        /// three boar scattered across the map is not a homestead. The radius is generous enough
        /// for a real enclosure and mean enough to exclude one wandering off.
        /// </summary>
        private static float CountTamedNearby(Player player)
        {
            try
            {
                var all = Character.GetAllCharacters();
                if (all == null) return 0f;

                Vector3 here = player.transform.position;
                int count = 0;

                foreach (var c in all)
                {
                    if (c == null || !c.IsTamed()) continue;
                    if (Vector3.Distance(c.transform.position, here) > TamedPenRadius) continue;
                    count++;
                }

                return count;
            }
            catch
            {
                return 0f;
            }
        }

        private const float TamedPenRadius = 40f;

        /// <summary>Remaining burn time per active food, as of the last poll; see <see cref="PollMeals"/>.</summary>
        private readonly Dictionary<string, float> _foodTimes = new Dictionary<string, float>();
        private bool _foodSeeded;

        /// <summary>
        /// Detects meals actually eaten, by watching the food slots rather than the game's counter.
        ///
        /// Valheim's own FoodEaten stat is incremented on exactly one branch of Player.EatFood —
        /// the one where the meal was REFUSED because all three slots were full and nothing was
        /// depleted enough to replace. Both successful branches return without touching it. So
        /// "Eat 3 meals" measured meals not eaten, and eating normally moved nothing.
        ///
        /// A food's burn time only ever counts DOWN, so a slot whose remaining time went up is a
        /// meal just eaten — and that catches all three cases: a new food, a food replacing a
        /// depleted one, and topping the same food up again. Matched by name rather than slot
        /// index, since an expiring food shifts every index after it.
        /// </summary>
        private void PollMeals()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            List<Player.Food> foods;
            try { foods = player.GetFoods(); }
            catch { return; }
            if (foods == null) return;

            int meals = 0;

            foreach (var food in foods)
            {
                if (food == null || string.IsNullOrEmpty(food.m_name)) continue;

                float previous;
                bool known = _foodTimes.TryGetValue(food.m_name, out previous);

                // A whole second of slack: burn time falls every frame, so anything rising past
                // that is an actual meal rather than float noise.
                if (!known || food.m_time > previous + 1f) meals++;

                _foodTimes[food.m_name] = food.m_time;
            }

            // Drop foods that have expired, so eating them again later reads as new.
            var gone = _foodTimes.Keys.Where(name => !foods.Any(f => f != null && f.m_name == name)).ToList();
            foreach (var name in gone) _foodTimes.Remove(name);

            // The first poll of a run establishes what is already burning; a player who loaded in
            // with a full food bar has not just eaten three meals.
            if (!_foodSeeded) { _foodSeeded = true; return; }

            for (int i = 0; i < meals; i++)
                _challenges?.ReportEvent(ChallengeKind.PlayerEvent, "MealEaten");
        }

        /// <summary>
        /// Act II's atmosphere: the Black Forest notices the axe.
        ///
        /// Driven by the lifetime chop count rising, because the mod's only injected hook is
        /// Character.OnDeath and a tree is not a character. See <see cref="ForestWatch"/>.
        /// </summary>
        private void PollForest()
        {
            if (_forest == null || !ActIsBlackForest) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            float? chops = ReadPlayerStat("TreeChops");
            if (chops == null) return;

            try { _forest.OnChopped(player, chops.Value); }
            catch (Exception ex) { LogOnce("forest-watch", ex); }
        }

        /// <summary>
        /// Reports plain facts about the player that the game keeps but never counts.
        ///
        /// Only "have you claimed a bed" so far, which is what makes a bed YOURS — building one and
        /// claiming one are different acts, and the questline was only ever checking the first.
        /// </summary>
        private void PollPlayerState()
        {
            if (_challenges == null) return;

            try
            {
                var profile = Game.instance?.GetPlayerProfile();
                if (profile != null && profile.HaveCustomSpawnPoint())
                    _challenges.ReportMeasure(ChallengeKind.PlayerState, "SpawnPointSet", 1f);

                var player = Player.m_localPlayer;
                if (player == null) return;

                // Comfort is the game's own measure of how much of a HOME a shelter is: a fire, a
                // bed, a chair, a table, each adding one under a roof. Valheim never puts a quest
                // on it, so most players never learn the system exists.
                _challenges.ReportMeasure(ChallengeKind.PlayerState, "Comfort", player.GetComfortLevel());

                // Three foods at once is roughly triple the health of one, and is the single most
                // useful thing a new player can learn before the Black Forest.
                var foods = player.GetFoods();
                if (foods != null)
                    _challenges.ReportMeasure(ChallengeKind.PlayerState, "FoodSlotsFilled", foods.Count);

                _challenges.ReportMeasure(ChallengeKind.PlayerState, "TamedNearby", CountTamedNearby(player));
                var fish = FishHeld(player);
                _challenges.ReportMeasure(ChallengeKind.PlayerState, "FishHeld", fish.Total);
                _challenges.ReportMeasure(ChallengeKind.PlayerState, "CookedFishHeld", fish.Cooked);

                ReportSkillLevels(player);

                PollHearthRecords(player);
            }
            catch
            {
                // Cosmetic to miss a poll; the next one will catch it.
            }
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
        private bool ActIsBlackForest => _actIndex == 1;
        private bool ActIsSwamp => _actIndex == 2;

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
        /// When the free Homeward comes back. Session state rather than run state on purpose:
        /// being sent home by a reload is harmless, and persisting it would mean a save-scum
        /// check for no gain.
        /// </summary>
        private float _homewardReadyAt;

        /// <summary>True when the free gate is off cooldown.</summary>
        public bool HomewardReady => Time.time >= _homewardReadyAt;

        /// <summary>Seconds until the free gate returns, or zero when it is ready.</summary>
        public float HomewardCooldown => Mathf.Max(0f, _homewardReadyAt - Time.time);

        /// <summary>m:ss, for a countdown the player reads at a glance.</summary>
        private static string FormatCountdown(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int whole = Mathf.CeilToInt(seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }

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

            // Charges first, so a boss kill still buys something the cooldown does not.
            if (_homewardCharges <= 0 && !HomewardReady)
            {
                Message($"Homeward returns in {FormatCountdown(_homewardReadyAt - Time.time)}.");
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

                if (_homewardCharges > 0)
                {
                    _homewardCharges--;
                    Message($"Homeward. {_homewardCharges} charge{(_homewardCharges == 1 ? "" : "s")} left.");
                    SaveState();
                }
                else
                {
                    _homewardReadyAt = Time.time + Mathf.Max(0f, _cfg.RunHomewardCooldownMinutes) * 60f;
                    Message($"Homeward. Free again in {FormatCountdown(_homewardReadyAt - Time.time)}.");
                }
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

            // The pen grows during a run, so the shepherd's blessing has to find the new arrivals.
            try { _boonEffects.RefreshShepherd(_boons != null && _boons.Held.Any(h => h.Def.Id == "shepherd")); }
            catch (Exception ex) { LogOnce("shepherd-refresh", ex); }
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

            // Only in the dark. The strip says "nothing you seek walks in the light", and until
            // now the Herald did — it spawned the moment its step came up, noon included, flatly
            // contradicting the act's one rule. The kill was already night-implied (the step
            // before it counts night lights); the SPAWN is what was missed.
            if (heraldWanted && IsNight && _deer.TrySpawnHerald(player))
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
                // Farming (alpha41). Plant is a compiled class, so a growing crop is detectable
                // exactly as a campfire is — no asset names. This is the category that made
                // BuildPiece learn to count: "plant a seed" is thin, "plant ten" is a crop.
                ["Plant"] = p => p.GetComponentInChildren<Plant>(true) != null,
                ["Beehive"] = p => p.GetComponentInChildren<Beehive>(true) != null,
                // Workbench and forge upgrades both carry StationExtension and no class separates
                // them — but a forge is Act II work, so in the Meadows this can only mean the
                // chopping block and the tanning rack.
                ["StationUpgrade"] = p => p.GetComponentInChildren<StationExtension>(true) != null,
                // Act II. Both are compiled classes, so neither names an asset. A cart needs
                // bronze nails, which is what makes it Act II work rather than Act I.
                ["Cart"] = p => p.GetComponentInChildren<Vagon>(true) != null,
                ["SignPost"] = p => p.GetComponentInChildren<Sign>(true) != null,
                // Act III. The cartography table is the swamp's real tool: it is the act where the
                // map stops being scenery and starts being how you get anywhere.
                ["MapTable"] = p => p.GetComponentInChildren<MapTable>(true) != null,
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
        /// <summary>How many pieces of each category were seen near the player on the last scan.</summary>
        private readonly Dictionary<string, int> _builtCounts = new Dictionary<string, int>();

        private void PollBuiltPieces()
        {
            if (_challenges == null) return;

            // Keep scanning while anything wants a COUNT, not just while categories remain unseen.
            // The old early-out stopped once every category had been seen once, which was right
            // when every objective was "have you built one" and wrong the moment "plant 10 seeds"
            // existed.
            bool wantsCount = MeasuredChallenges().Any(a =>
                a.Def.Kind == ChallengeKind.BuildPiece && a.Def.Target > 1f);

            if (_builtSeen.Count < PieceCategories.Count || wantsCount) ScanForBuiltPieces();

            foreach (var category in PieceCategories.Keys)
            {
                _builtCounts.TryGetValue(category, out int live);

                // The larger of what is standing here NOW and "you have built one at some point".
                // The live count is what makes a crop field measurable; the latch is what stops a
                // finished "build a fire" un-finishing when the player walks away from it. The
                // engine's max-semantics keeps whichever was higher, so a harvested field does not
                // take back the step it completed.
                float value = Math.Max(live, _builtSeen.Contains(category) ? 1 : 0);
                if (value > 0f) _challenges.ReportMeasure(ChallengeKind.BuildPiece, category, value);
            }
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

            _builtCounts.Clear();

            foreach (var piece in _pieceBuffer)
            {
                if (piece == null || !piece.IsCreator()) continue;

                // Every category every time, rather than stopping at the first unseen one: the scan
                // now produces COUNTS, and a count is only correct if nothing was skipped.
                foreach (var entry in PieceCategories)
                {
                    if (!entry.Value(piece)) continue;

                    _builtCounts.TryGetValue(entry.Key, out int n);
                    _builtCounts[entry.Key] = n + 1;
                    _builtSeen.Add(entry.Key);
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
        /// <summary>
        /// How many of each named item the player is holding, in ONE pass over the inventory.
        ///
        /// Deliberately not <c>Inventory.CountItems</c>. That method takes a
        /// <c>matchWorldLevel</c> parameter defaulting to TRUE, and silently skips any stack whose
        /// <c>m_worldLevel</c> is below the world's current one — so "Hold 25 Stone" could not be
        /// completed with loose surface stone, which carries the level its zone was generated at
        /// rather than today's, while wood from a freshly felled tree counted normally. A
        /// challenge asking for stone means stone; where it has been lying is not our business.
        ///
        /// Counting here rather than passing the flag also removes the trap instead of stepping
        /// over it, and turns N full scans into one.
        /// </summary>
        private static Dictionary<string, int> CountHeld(Inventory inventory, List<string> names)
        {
            var counts = new Dictionary<string, int>();
            foreach (var name in names) counts[name] = 0;

            var items = inventory?.GetAllItems();
            if (items == null) return counts;

            foreach (var item in items)
            {
                if (item?.m_shared == null) continue;

                // Same comparison the game makes: the localisation token on shared data.
                int running;
                if (counts.TryGetValue(item.m_shared.m_name, out running))
                    counts[item.m_shared.m_name] = running + item.m_stack;
            }

            return counts;
        }

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

            // Homestead bests are written whether the run was won or abandoned: the fish was
            // caught either way, and a record you only keep by winning is one most runs never get
            // to set.
            try
            {
                foreach (var record in _records.Achieved)
                    PermanentRecord.RecordHearth(Player.m_localPlayer, record.Id, record.Value, record.Detail);
            }
            catch (Exception ex) { LogOnce("record-hearth", ex); }

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

            // The dev speed boost is a loan, and the run ending is the last chance to repay it.
            DevRestoreSpeed();

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
        /// <summary>
        /// Every challenge definition this build ships: the random pool and all five acts' tracks.
        ///
        /// Validation needs both. A threshold that is impossible is just as broken in a pool
        /// bounty as in a questline step, and the pool is where the fishing bounties live.
        /// </summary>
        private IEnumerable<ChallengeDefinition> AllChallengeDefinitions() =>
            BuildFullPool().Concat(_acts.SelectMany(a => a.AllSteps));

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

                // The discovery step must sit immediately before the boss, ON THE SAME TRACK.
                //
                // The "ends on a kill" check above passed happily while the discovery step was
                // filed under CRAFT by the Kind-based routing — the hunt track still ended on the
                // boss, it had simply lost the step that was supposed to precede it, and Eikthyr's
                // name turned up at the end of the crafting questline instead. An invariant that
                // only looks at the last element cannot see a missing one.
                // The light race must sit on the HUNT track. It changed Kind in alpha61
                // (KillPrefab -> PlayerEvent) and silently fell to CRAFT, because Kind is only a
                // PROXY for track — the same trap that once filed "Kill Eikthyr" under crafting.
                // The hunt then jumped from the spirit straight to the Herald, and the act's
                // centrepiece was queued behind the bench upgrades.
                foreach (var race in act.AllSteps.Where(c =>
                             c.Kind == ChallengeKind.PlayerEvent && c.Param == StolenLights.TakenEvent))
                {
                    var huntTrack = act.Tracks.FirstOrDefault(t => t.Id == HuntTrackId);
                    if (huntTrack == null || !huntTrack.Chain.Any(c => c.Id == race.Id))
                        Debug.LogError($"[ICanShowYouTheWorld] {act.Label}: the light race '{race.Id}' is not " +
                                       "on the hunt track — the hunt will skip its centrepiece.");
                }

                // A gate naming a track this act does not have is VACUOUS by design, so that a
                // dropped track cannot deadlock a boss — which means a typo in the name disables
                // the gate silently. Since every real gate names a track in its own act, that is
                // checkable.
                foreach (var gated in act.AllSteps.Where(c => !string.IsNullOrEmpty(c.RequiresTrackComplete)))
                {
                    if (!act.Tracks.Any(t => t.Id == gated.RequiresTrackComplete))
                        Debug.LogError($"[ICanShowYouTheWorld] {act.Label} step '{gated.Id}' waits on track " +
                                       $"'{gated.RequiresTrackComplete}', which this act does not have — the gate does nothing.");
                }

                var discovery = act.AllSteps.FirstOrDefault(c => c.Kind == ChallengeKind.DiscoverLocation);
                if (discovery == null)
                {
                    Debug.LogError($"[ICanShowYouTheWorld] {act.Label} has no discovery step — its finale is unearned.");
                }
                else
                {
                    int discoveryIndex = hunt.Chain.IndexOf(discovery);
                    if (discoveryIndex < 0)
                        Debug.LogError($"[ICanShowYouTheWorld] {act.Label}'s discovery step '{discovery.Id}' is not on " +
                                       "the hunt track — check its Track override.");
                    else if (discoveryIndex != hunt.Chain.Count - 2)
                        Debug.LogError($"[ICanShowYouTheWorld] {act.Label}'s discovery step '{discovery.Id}' is not " +
                                       "immediately before the boss.");
                }
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

        /// <summary>True when a questline track is currently asking the player to find a location.</summary>
        private bool DiscoveryStepIsCurrent() =>
            _challenges != null && _challenges.Tracks.Any(t =>
                t.Current != null && t.Current.Def.Kind == ChallengeKind.DiscoverLocation);

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

            var own = _acts[_actIndex].Tracks;
            var live = _challenges.Tracks.ToList();
            var seated = ActDefinition.SeatingFor(_acts, _actIndex, live);

            // SetTracks seats every chain at its beginning, so a carried track's position has to
            // be put back afterwards or the hearth would restart from step one.
            var carried = seated
                .Where(x => !own.Any(o => o.Id == x.Id))
                .Select(x => live.FirstOrDefault(l => l.Id == x.Id))
                .Where(x => x != null)
                .Select(x => new { x.Id, x.Index, Progress = x.Current?.Progress ?? 0f, StepId = x.Current?.Def?.Id })
                .ToList();

            _challenges.SetTracks(seated);

            foreach (var c in carried)
                _challenges.RestoreTrack(c.Id, c.Index, c.Progress, c.StepId);

            if (!announce || !changed) return;

            var act = _acts[_actIndex];
            Announce(act.Banner);
            Message(act.Banner);

            // The CARD is the real announcement. The center-screen text and the chat line were
            // both missable in the chaos right after a boss kill — which is exactly when an act
            // changes — and a transition the player does not notice is a transition that did not
            // happen for them (owner: "I missed act 2 starting").
            ActCardTitle = act.Banner;
            ActCardEpigraph = act.Epigraph;
            ActCardShownAt = Time.time;

            Debug.Log($"[ICanShowYouTheWorld] Act transition → {act.Label}");
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

                // CollectItem compares against m_shared.m_name, as the game itself does —
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
                    // Fishing is the one family of measures keyed to a PREFAB NAME rather than a
                    // compiled type or a stat, and two of its steps carry NUMBERS this assembly
                    // cannot check: how many species exist, and how heavy the heaviest gets.
                    // Weights and species lists are asset data.
                    //
                    // So the world is asked at run start and the answer is LOGGED. A threshold set
                    // from a guess is a step that sits at zero forever with nothing to explain it;
                    // a threshold set from this line is a one-word edit.
                    if (odb != null && odb.m_items != null)
                    {
                        var fishes = odb.m_items
                            .Where(go => go != null &&
                                         go.name.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(go => new { go.name, drop = go.GetComponent<ItemDrop>() })
                            .Where(x => x.drop != null && x.drop.m_itemData?.m_shared != null &&
                                        !IsFishingGear(x.name))
                            .Select(x => new { x.name, weight = x.drop.m_itemData.m_shared.m_weight })
                            .ToList();

                        if (fishes.Count == 0)
                        {
                            Debug.LogError("[ICanShowYouTheWorld] No 'Fish' item prefab found — every " +
                                           "fishing step is impossible.");
                        }
                        else
                        {
                            Debug.Log("[ICanShowYouTheWorld] Fish available: " + string.Join(", ",
                                fishes.Select(f => $"{f.name}({f.weight:0.##})").ToArray()));

                            float heaviest = fishes.Max(f => f.weight);
                            int species = fishes.Select(f => f.name).Distinct().Count();

                            foreach (var step in AllChallengeDefinitions()
                                         .Where(c => c.Kind == ChallengeKind.PlayerState))
                            {
                                if (step.Param == "HeaviestFish" && step.Target > heaviest)
                                    Debug.LogError($"[ICanShowYouTheWorld] '{step.Display}' wants {step.Target}, " +
                                                   $"but the heaviest fish in this world weighs {heaviest:0.##}.");

                                if (step.Param == "FishSpecies" && step.Target > species)
                                    Debug.LogError($"[ICanShowYouTheWorld] '{step.Display}' wants {step.Target} " +
                                                   $"species, but only {species} edible fish prefabs exist.");
                            }
                        }
                    }

                    // Which body the pale lights will wear. The chain prefers a Mistlands wisp
                    // ("Wisp"), falls back to Ghost, then a starred deer — and which one THIS
                    // build resolves is asset data, so it is asked and logged rather than assumed.
                    // One line, and the answer decides whether "could it be a wisp model?" is
                    // already true or needs a different prefab name.
                    foreach (var name in new[] { "Wisp", "Ghost" })
                        Debug.Log($"[ICanShowYouTheWorld] Spirit prefab '{name}': " +
                                  (scene != null && scene.GetPrefab(name) != null ? "available" : "NOT in this build"));

                    // The race's music cue, same discipline: the music table is asset data, and
                    // TriggerMusic on a wrong name plays nothing and throws nothing.
                    if (!string.IsNullOrEmpty(_cfg.RunLightMusic))
                    {
                        bool found = false;
                        try
                        {
                            var mm = MusicMan.instance;
                            var find = mm == null ? null : typeof(MusicMan).GetMethod("FindMusic",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            found = find != null && find.Invoke(mm, new object[] { _cfg.RunLightMusic }) != null;
                        }
                        catch { /* reflection is best-effort; the log line below still narrows it */ }

                        Debug.Log($"[ICanShowYouTheWorld] Race music '{_cfg.RunLightMusic}': " +
                                  (found ? "available" : "NOT found (or unprobeable) — the race may be silent"));
                    }

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
                ResolveSkillParams();
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

        /// <summary>The act whose altar is currently pinned, or -1 for none. Not persisted — a pin lives in the world save.</summary>
        private int _pinnedActIndex = -1;

        /// <summary>
        /// Pins THIS act's boss altar on the map, and only this act's.
        ///
        /// Every altar used to be pinned at StartRun, which handed the player the whole saga in the
        /// first minute — and once the discovery steps arrived that made them travel steps to a
        /// place already known rather than anything found (owner, alpha36: "will the altar be
        /// visible on the map though?"). Now each appears as its act begins: the goal you are
        /// working towards is always legible, the four after it are not.
        ///
        /// Driven from the poll rather than the act transition so that one path covers every way an
        /// act can become current — a fresh run, a resume, a boss falling — and so a player who is
        /// not loaded yet at seating time still gets the pin a second later.
        /// </summary>
        private void RefreshActPin()
        {
            if (_pinnedActIndex == _actIndex) return;
            if (_actIndex < 0 || _actIndex >= _acts.Count) return;

            // The pin arrives when the questline ASKS you to find the altar, not when the act
            // begins (owner, alpha38: "let the marker only appear once you kill the herald").
            // In Act I that is after the Herald falls; in every act it is the moment the discovery
            // step becomes current. The map is therefore never ahead of the questline.
            //
            // The vanilla Vegvisir near spawn still works — reading it is the player choosing to
            // skip the mystery, which is their business. Removing it would be a permanent change to
            // a world this mode otherwise leaves exactly as it found it.
            if (!DiscoveryStepIsCurrent()) return;

            var game = Game.instance;
            var player = Player.m_localPlayer;
            if (game == null || player == null) return;

            string key = _acts[_actIndex].BossDefeatKey;
            var boss = Bosses.FirstOrDefault(b => b.defeatKey == key);
            if (boss.locName == null) return;

            try
            {
                // showMap:false — the banner already says the act changed; throwing the map open on
                // top of that is one interruption too many.
                game.DiscoverClosestLocation(
                    boss.locName, player.transform.position, boss.display,
                    (int)Minimap.PinType.Boss, false);

                _pinnedActIndex = _actIndex;
            }
            catch (Exception ex)
            {
                // Left unpinned rather than marked done, so the next poll tries again.
                LogOnce("discover-" + boss.locName, ex);
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

            if (def.Id == null) return;

            // A race you lost pays nothing. The step still completed and the track still moved;
            // only the prize is gone.
            if (_forfeited.Contains(def.Id)) return;

            string boonId;
            if (QuestBoons.TryGetValue(def.Id, out boonId) && _boons != null)
            {
                try
                {
                    if (_boons.Grant(boonId))
                    {
                        var granted = _boons.Held.FirstOrDefault(h => h.Def.Id == boonId);
                        if (granted != null) Message($"Well rested. {granted.Def.Display} earned.");
                    }
                }
                catch (Exception ex) { LogOnce("grant-quest-boon", ex); }
            }

            if (!QuestRewards.TryGetValue(def.Id, out var items) || items == null) return;

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
            new[] { ("LoxPie", 5), ("BloodPudding", 10) },        // after Yagluth
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

                string prefabName = PrefabNameOf(c);
                _challenges?.ReportKill(prefabName);

                // Eikthyr's herd is being farmed, and a deer gives up its light where it falls.
                // Only while the hunt is on, for the same reason the pack is: this is the story of
                // the hunt, not a thing that follows the player around the act.
                if (_lights != null && ActIsMeadows && prefabName == DeerHerd.DeerPrefab && IsNight && DeerHuntWanted)
                {
                    try
                    {
                        _lights.Release(c.transform.position);
                        Message("Its light rises. Take it before they do.");
                    }
                    catch (Exception ex) { LogOnce("release-light", ex); }
                }

                // Eikthyr spawns darkness, so his hunt happens in it. Reported IN ADDITION to the
                // plain name, so the random pool's daytime "Hunt 3 Deer" still counts while the
                // questline's asks for the dark.
                if (prefabName == DeerHerd.DeerPrefab && IsNight)
                    _challenges?.ReportKill(DeerHerd.NightDeerKillName);

                // The herd answers separately, and may hand back a synthetic name — the Herald's,
                // which is matched by identity rather than by prefab so ordinary deer cannot
                // complete its step. Reported IN ADDITION to the ordinary kill above: killing the
                // Herald is also killing a deer, and should count for both.
                // On-kill boons see every non-player, non-tamed death, in every act.
                try { _boonEffects.OnKill(); }
                catch (Exception ex) { LogOnce("boon-on-kill", ex); }

                if (_fen != null && ActIsSwamp)
                {
                    try
                    {
                        if (_fen.OnCharacterDied(c)) Message("The swamp does not let go of its dead.");
                    }
                    catch (Exception ex) { LogOnce("fen-watch", ex); }
                }

                if (_gatherer != null && ActIsMeadows)
                {
                    string felled = _gatherer.OnCharacterDied(c);
                    if (felled != null)
                    {
                        _challenges?.ReportKill(felled);
                        Message($"{TheGatherer.Name} falls, and the lights it held go free.");

                        // And they actually DO. The line used to be the whole event — the hoard
                        // "went free" in a message and nothing appeared in the world (owner: "he
                        // dropped a lot of stuff, but no spirits"). One light per light the
                        // forest took, within reason, and on a two-minute fade rather than the
                        // race's thirty seconds: these are freed, not contested, and the player
                        // just fought for them.
                        if (_lights != null)
                        {
                            int freed = Mathf.Clamp(_lights.Lost, 2, 6);
                            for (int i = 0; i < freed; i++)
                            {
                                float angle = i * Mathf.PI * 2f / freed;
                                _lights.Release(
                                    c.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.5f,
                                    120f);
                            }
                        }
                    }
                }

                if (_deer != null && ActIsMeadows)
                {
                    // Only while the hunt is the step in play. A pack every time you touch a deer
                    // is atmosphere during the hunt and a tax on every other minute of the act —
                    // and it is 100% now, so the gate is what keeps it from being punishing
                    // (owner: "we should only spawn grey* IF the deer hunt quest is active").
                    _deer.ContestEnabled = DeerHuntWanted;

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

            // Same hunt, same ground. Re-rolling on resume would move the Herald after the player
            // had already walked most of the way to where the bearing had been pointing.
            _deer.HeraldTarget = s.heraldTargetSet
                ? new Vector3(s.heraldTargetX, s.heraldTargetY, s.heraldTargetZ)
                : (Vector3?)null;

            // Re-lend what completions had already paid, so a resumed run keeps the health it
            // earned. 0 on a pre-alpha35 save, which simply starts the accumulation from there.
            _taskHealthReward = Math.Max(0f, s.taskHealthReward);
            _homewardCharges = Math.Max(0, s.homewardCharges);

            _rng = new Random(_rngSeed);
            BuildActSystems();

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
            _lights?.Restore(s.lightsTaken, s.lightsLost);
            _records.Restore(s.recordIds, s.recordValues, s.recordDetails);

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
                lightsTaken = _lights?.Taken ?? 0,
                lightsLost = _lights?.Lost ?? 0,
                recordIds = _records.All.Select(r => r.Id).ToList(),
                recordValues = _records.All.Select(r => r.Value).ToList(),
                recordDetails = _records.All.Select(r => r.Detail).ToList(),
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
                heraldTargetSet = _deer?.HeraldTarget != null,
                heraldTargetX = _deer?.HeraldTarget?.x ?? 0f,
                heraldTargetY = _deer?.HeraldTarget?.y ?? 0f,
                heraldTargetZ = _deer?.HeraldTarget?.z ?? 0f,
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
            // Fishing bounties live in the POOL rather than the questline: they are the kind of
            // thing you take on when you fancy it, and the pool is where optional heat is bought.
            new ChallengeDefinition { Id = "c-fishhaul", Tier = 1, Kind = ChallengeKind.PlayerState, Param = "FishHeld",       Target = 8, HeatReward = 2, Display = "A day at the water (8 fish)" },
            new ChallengeDefinition { Id = "c-fishcook", Tier = 1, Kind = ChallengeKind.PlayerState, Param = "CookedFishHeld", Target = 3, HeatReward = 2, Display = "Fish supper (3 cooked)" },
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
            new ChallengeDefinition { Id = "s-food",    Tier = 1, Kind = ChallengeKind.PlayerEvent, Param = "MealEaten",       Target = 3,  HeatReward = 1, Display = "Eat 3 meals" },
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
                // Titles name the STORY, never the biome and never the boss. "The Meadows" told
                // a player where they were, which they could already see, and the act table is the
                // one place the saga gets to say what an act is ABOUT before it happens.
                //
                // They also run as one arc, and the arc is LIGHT: I asks who is taking it, II
                // answers where it goes, and the finale bookends the theft the saga opens with.
                //
                // The saga is SEVEN acts — the five mainland bosses plus the Queen and Fader.
                // Only five are built. V was called "The Last Harvest" while five was the whole
                // story and read as a finale; it is a middle, so it takes the Plains' own image
                // instead. See the act plans spec.
                Id = "act1", Numeral = "I", Title = "The Stolen Light",
                Epigraph = "Something is taking the light from the meadows. Take it back.",
                BossDefeatKey = "defeated_eikthyr", Tracks = Split(MainQuestChain()),
            },
            new ActDefinition
            {
                Id = "act2", Numeral = "II", Title = "Where the Light Goes",
                Epigraph = "The forest has been fed for years. Meet what did the feeding.",
                BossDefeatKey = "defeated_gdking", Tracks = Split(BlackForestChain()),
            },
            new ActDefinition
            {
                Id = "act3", Numeral = "III", Title = "Nothing Stays Buried",
                Epigraph = "What the marsh takes, it keeps.",
                BossDefeatKey = "defeated_bonemass", Tracks = Split(SwampChain()),
            },
            new ActDefinition
            {
                Id = "act4", Numeral = "IV", Title = "The White Silence",
                Epigraph = "Above the treeline, even light freezes.",
                BossDefeatKey = "defeated_dragon", Tracks = Split(MountainChain()),
            },
            new ActDefinition
            {
                Id = "act5", Numeral = "V", Title = "The Golden Ruin",
                Epigraph = "They harvested a god's herd before you. See how it ended.",
                BossDefeatKey = "defeated_goblinking", Tracks = Split(PlainsChain()),
            },
        };

        public const string HuntTrackId = "hunt";
        public const string CraftTrackId = "craft";
        public const string HearthTrackId = "hearth";
        public const string ForgeTrackId = "forge";
        public const string MarshTrackId = "marsh";

        /// <summary>
        /// Every track a saga can have, in DISPLAY order, with the label each shows.
        ///
        /// A table rather than a hardcoded pair of buckets, because each act after the first is
        /// planned to have a third track of its own — forge, marsh, peak, steading. Adding one is
        /// now a row here plus a Track override on its steps.
        ///
        /// Order matters twice: hunt stays first so track 0 remains MainQuestSlot, and the rest
        /// keep a stable position so a row does not move under the player between acts.
        /// </summary>
        private static readonly (string id, string label)[] TrackTable =
        {
            (HuntTrackId,   "HUNT"),
            (CraftTrackId,  "CRAFT"),
            (HearthTrackId, "HEARTH"),
            (ForgeTrackId,  "FORGE"),
            (MarshTrackId,  "MARSH"),
        };

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

            // An explicit Track wins; otherwise Kind decides. See ChallengeDefinition.Track for why
            // the override exists — Kind is only a PROXY for track, and the discovery steps are the
            // case where the proxy is wrong.
            string TrackOf(ChallengeDefinition d) =>
                !string.IsNullOrEmpty(d.Track)
                    ? d.Track
                    : d.Kind == ChallengeKind.KillPrefab ? HuntTrackId : CraftTrackId;

            var tracks = TrackTable
                .Select(t => new QuestTrack
                {
                    Id = t.id, Label = t.label,
                    Chain = steps.Where(d => TrackOf(d) == t.id).ToList(),
                })
                .ToList();

            // A step naming a track the table does not have would vanish silently, which is the
            // failure mode this mode keeps paying for.
            var orphans = steps.Where(d => !TrackTable.Any(t => t.id == TrackOf(d))).ToList();
            if (orphans.Count > 0)
                Debug.LogError("[ICanShowYouTheWorld] Steps name a track that does not exist: " +
                               string.Join(", ", orphans.Select(d => $"{d.Id}->{TrackOf(d)}").ToArray()));

            return tracks.Where(t => t.Chain.Count > 0).ToList();
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
                Target = 4, Display = "Hunt 4 Boar", RewardText = "Leather leggings + a quiver of arrows",
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
                // FIRST on the hearth track. It arrived sixth and by then the foraging was long
                // done — ItemsPickedUp is measured from when the step APPEARS, so a late one
                // asks for fifty more berries at the point berries stopped being interesting
                // (owner: "it arrives a little late, so there's not much incentive").
                //
                // Here it feeds the step directly after it: three different foods at once is
                // berries, mushrooms and something cooked.
                Id = "mq-forage", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.StatDelta,
                Param = "ItemsPickedUp", Target = 30, Display = "Forage the meadows (30 finds)",
                RewardText = "Seeds and a full larder",
                Hint = "Berries, mushrooms, flint by the water, anything on the ground.",
            },
            new ChallengeDefinition
            {
                // Placed right after the cooking station, because that is the moment the player
                // CAN do it and the moment it is worth learning: three foods at once is roughly
                // triple the health of one, and most people eat a single thing and then wonder
                // why the Black Forest kills them.
                Id = "mq-meal", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState, Param = "FoodSlotsFilled",
                Target = 3, Display = "Sit down to a proper meal", RewardText = "A stocked larder",
                Hint = "Three different foods at once — cooked meat, berries, mushrooms.",
            },
            new ChallengeDefinition
            {
                // Greyling, not Greydwarf: the greydwarf proper lives in the Black Forest, and
                // every step before Eikthyr should be doable without leaving the Meadows. Greylings
                // are the weaker meadows cousin, hence the higher count. The ID is deliberately
                // unchanged so a run already part-way through this step keeps its progress.
                Id = "mq-grey", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Greyling",
                Target = 4, Display = "Kill 4 Greylings", RewardText = "Helmet + cape + more arrows",
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
                // Claiming, not just building — that is what makes a bed yours, and what "sleep
                // through the night" and Homeward both actually depend on. The step used to check
                // only that one had been placed (owner: "a quest to claim the bed").
                Id = "mq-bed", MainQuest = true, Kind = ChallengeKind.PlayerState, Param = "SpawnPointSet",
                Target = 1, Display = "Build a bed and claim it", RewardText = "Timber and resin for the rest of the house",
                Hint = "Place it under a roof, then interact to claim it as your spawn.",
            },
            new ChallengeDefinition
            {
                Id = "mq-home", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.StatDelta, Param = "TimeInBase",
                Target = 120, Display = "Settle in (2 min at home)", RewardText = "A shield by the door, and arrows",
                Hint = "Needs a roof AND a fire. Stand still indoors and it counts up.",
            },
            new ChallengeDefinition
            {
                Id = "mq-rest", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.StatDelta, Param = "Sleep",
                Target = 1, Display = "Sleep through the night", RewardText = "A hot meal, a fishing rod \u2014 and Tireless, for sleeping well",
                Hint = "A bed you have claimed, and nothing hostile nearby.",
            },
            new ChallengeDefinition
            {
                // The game's own measure of how much of a HOME a shelter is. Fire and bed are
                // already built by now, so this asks for the furniture: a chair, a table, a
                // banner, each adding one under a roof. Valheim never quests on comfort, so the
                // system usually goes unnoticed until someone reads a wiki.
                Id = "mq-comfort", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState, Param = "Comfort",
                Target = 5, Display = "Make it comfortable (comfort 5)",
                RewardText = "Hide and resin to furnish it",
                Hint = "A chair and a table, under the roof, near the fire. Rested lasts longer for each.",
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
                // LAST on the crafting track, not beside the bench it upgrades. Sitting fourth it
                // arrived before the flint and deer hide it wants, so it read as a wall rather
                // than a step (owner: "it's too early"). The bench upgrades are what you build
                // once the house is finished and you are improving it, which is exactly here.
                //
                // The two the Meadows can actually build: a chopping block and a tanning rack.
                // Wood, flint and hide — no bronze, which is the check the trophy step failed
                // (owner: "we have two upgrades for the workbench, we should have those in HEARTH").
                Id = "mq-upgrade", MainQuest = true, Kind = ChallengeKind.BuildPiece,
                Param = "StationUpgrade", Target = 2, Display = "Upgrade the workbench (2)",
                RewardText = "Flint, hide and resin",
                Hint = "A chopping block and a tanning rack, both inside the bench's circle.",
            },
            new ChallengeDefinition
            {
                // The rod comes from the previous step's reward, because Haldor — the only
                // vanilla source — spawns in the Black Forest, and Act I is not supposed to need
                // that trip yet. See the Act I design spec.
                Id = "mq-fish", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState,
                Param = "FishHeld", Target = 1, Display = "Catch your first fish",
                RewardText = "A cauldron's worth of bait",
                Hint = "Equip the rod, hold to cast, hold right-click to reel — then WALK OVER and pick it up.",
            },
            new ChallengeDefinition
            {
                // Spaced away from "Cast a line" on purpose. The hearth is a linear chain, so
                // stacking every fishing step together would turn the homestead act into a
                // fishing act for ten minutes.
                Id = "mq-fish-varied", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState,
                // NOT species. This game ships exactly two catchable fish items — FishRaw and
                // FishAnglerRaw, the latter a Mistlands fish — so "3 species" could never be done
                // in the Meadows. Only the cooking recipes made it look possible. A haul is the
                // honest version of the same idea: keep at it rather than land one and leave.
                Param = "FishHeld", Target = 5, Display = "A good haul (5 fish)",
                RewardText = "Bait, and salt for the catch",
                Hint = "Deeper water bites more often. Keep the bait topped up.",
            },
            new ChallengeDefinition
            {
                // Husbandry belongs to the homestead, not to the forest. It lived in Act II with
                // the planting until the obvious was pointed out: boar are a MEADOWS animal, so
                // asking for one in the Black Forest either sends you back or completes itself on
                // arrival — neither of which is a quest.
                Id = "mq-tame", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.StatDelta, Param = "CreatureTamed",
                Target = 1, Display = "Tame a boar", RewardText = "Food enough to keep it, and a friend for it",
                Hint = "Pen it, drop food, and stay out of sight until it calms.",
            },
            new ChallengeDefinition
            {
                // Skill rather than count: fishing levels quickly at first, so this asks you to
                // actually spend an evening at the water rather than land three and leave. Measured
                // through the generic Skill: reading, so "Woodcutting 20" would work the same way.
                Id = "mq-fish-skill", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState,
                Param = "Skill:Fishing", Target = 10, Display = "Fishing skill 10",
                RewardText = "Bait, and a fisherman's supper",
                Hint = "Every fish landed raises it. Deeper water pays better.",
            },
            new ChallengeDefinition
            {
                // Self-calibrating, so it can never be unreachable: on a first saga any fish
                // completes it, and afterwards it asks for a genuinely better one. The quest and
                // the HOMESTEAD record are the same object — finishing this puts the star on the
                // panel in the same instant.
                Id = "mq-fish-best", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState,
                // Weight was the plan, and every fish in this game weighs 0.5 — so "heavier than
                // your best" would have completed on the first cast, forever. A full larder is a
                // homestead idea anyway, and it needs the cooking station the hearth already built.
                Param = "CookedFishHeld", Target = 5, Display = "A fisherman's larder (5 cooked)",
                RewardText = "A feast from the water",
                Hint = "Cook them on the station. Cooked fish keeps and feeds better.",
            },
            new ChallengeDefinition
            {
                // Last on the track by design: boar breed on their own schedule, and this is the
                // one homestead step that can take real time. The hunt track runs in parallel, so
                // a slow pen delays nothing on the way to Eikthyr.
                Id = "mq-pen", MainQuest = true, Track = HearthTrackId, Kind = ChallengeKind.PlayerState, Param = "TamedNearby",
                Target = 3, Display = "A pen of three", RewardText = "Feed enough for a herd",
                Hint = "Two tamed boar in a pen, fed and left alone, will raise a third.",
            },
            new ChallengeDefinition
            {
                // The act's opening mystery, before any deer. Something pale drifts at the edge of
                // the meadows and it knows where Eikthyr is — reaching it, not killing it, is the
                // point. The strip carries a rumour rather than a bearing: "far to the north-east"
                // and nothing more, because a number turns a chase into a walk.
                Id = "mq-spirit", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.PlayerState,
                Param = SpiritChase.FoundMeasure, Target = 1, Display = "Follow the pale light",
                RewardText = "A torch that will not go out, and arrows",
                Hint = "One light the forest never found. It keeps to the dark and does not wait.",
            },
            new ChallengeDefinition
            {
                // The act's centre. Killing the deer is the easy half: every one gives up a light
                // where it falls, the forest sends its children for it, and the light burns for
                // half a minute. Take it back or they do.
                //
                // Measured on lights TAKEN rather than deer killed, so the step is the race rather
                // than the kill — and lights only rise after dark, which keeps the whole hunt
                // nocturnal without a second rule to explain.
                Id = "mq-deer", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.PlayerEvent, Param = StolenLights.TakenEvent,
                Target = 5, Display = "Take back their light (5)",
                RewardText = "Deer trophies — Eikthyr's summons",
                Hint = "His trophies are the price of an audience \u2014 hunt after dark, and when a deer falls, RUN TO THE LIGHT before it fades. What you do with their lights is between you and the forest. Let it take eight and the trophies are lost.",
            },
            new ChallengeDefinition
            {
                // Act I's climax before the boss, and the one deer that is an event rather than a
                // counter. Param is SYNTHETIC — the Herald is an ordinary Deer wearing a name, so
                // matching on "Deer" would let any deer finish this. The host reports this name only
                // when that specific creature dies, matched by ZDOID. See DeerHerd.
                Id = "mq-herald", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = DeerHerd.HeraldKillName,
                Target = 1, Display = "Hunt Eikthyr's Herald", RewardText = "A hunter's bow, and the last trophies",
                Hint = "The herd's guardian, and the last trophies you need. Its fall will be heard.",
            },
            new ChallengeDefinition
            {
                // The answer to "why are there always greydwarves". Everything they carried off
                // went somewhere, and when the Herald falls it stops waiting and comes for you.
                //
                // No bearing and no search: it spawns beside the player, which is the opposite of
                // the Herald on purpose — one act, two named creatures, one you must find and one
                // that finds you. It also cannot be placed somewhere unloaded, which is the bug
                // that made the Herald unfindable for two versions.
                Id = "mq-gatherer", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.KillPrefab,
                Param = TheGatherer.KillName, Target = 1, Display = "Kill the Gatherer",
                RewardText = "Every light it was holding, and the way to the altar",
                Hint = "It is fat on what the forest took. Kill it and every light it holds goes free. It will find you.",
            },
            new ChallengeDefinition
            {
                Id = "mq-find", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.DiscoverLocation,
                Param = "Eikthyrnir",
                Target = 1, Display = "Find Eikthyr's altar", RewardText = "Eikthyr's summoning stones await",
                Hint = "Two standing stones, ringed with runes. Hang the trophies his own herd paid for, and call him down.",
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
                // Each act opens by collecting on the last one. The trophy sat in the inventory
                // unused for the whole saga because nothing ever asked for it, and a boss power
                // you never turned on is a reward you never received (owner: "there should be a
                // quest to deliver Eikthyr\u2019s trophy and activate its power").
                //
                // Measured on the stat the game keeps for setting that power, so it is the ACT of
                // claiming that counts, not carrying the trophy around.
                Id = "bf-power", MainQuest = true, Track = ForgeTrackId, Kind = ChallengeKind.StatDelta, Param = "SetGuardianPower",
                Target = 1, Display = "Claim Eikthyr\u2019s power",
                RewardText = "Provisions for the road", Hint = "His trophy on the sacrificial stones. Stag-like stride: press the power key and run.",
            },
            new ChallengeDefinition
            {
                // Every act opens on arrival. ReachBiome asks for the DESTINATION and says nothing
                // about how you got there — which is the only safe thing for a linear chain to ask,
                // since a boat step would stall on a world where the biome is walkable.
                Id = "bf-arrive", MainQuest = true, Kind = ChallengeKind.ReachBiome, Param = "BlackForest",
                Target = 1, Display = "Reach the Black Forest", RewardText = "A torch, and arrows for the dark",
                Hint = "The Gatherer was only a hand. Everything it collected went this way, for years. Dark pines and rock \u2014 head away from the meadows.",
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
                // Act II, not Act I: an item stand needs bronze nails, so in the Meadows this step
                // was IMPOSSIBLE — and because a track is a linear chain it stalled every hearth
                // step behind it. Fishing, foraging, taming and the pen were all unreachable for a
                // whole playthrough (owner: "I could not hang anything on my wall because the thing
                // you hang it on cant be crafted yet").
                //
                // On CRAFT rather than a new Act II hearth: an act with its own "hearth" would
                // collide with Act I's carried one and discard it. See ActDefinition.SeatingFor.
                Id = "bf-trophy", MainQuest = true, Track = ForgeTrackId, Kind = ChallengeKind.StatDelta, Param = "ItemStandUses",
                Target = 1, Display = "Hang a trophy", RewardText = "Wood and resin for the hall",
                Hint = "An item stand needs bronze nails. Mount it on a wall, then place a trophy.",
            },
            new ChallengeDefinition
            {
                // A cart needs bronze nails, so it cannot be built before the smelter — which is
                // why it sits after it rather than with the homestead work it resembles. It is the
                // first thing in the saga that makes ORE a solvable problem rather than a series
                // of trips.
                Id = "bf-cart", MainQuest = true, Track = ForgeTrackId, Kind = ChallengeKind.BuildPiece,
                Param = "Cart", Target = 1, Display = "Raise a cart",
                RewardText = "Bronze nails, and iron to come",
                Hint = "Wood and bronze nails, at the workbench. It hates hills.",
            },
            new ChallengeDefinition
            {
                // Wood only, and pure ceremony — the second homestead gets a name. Cheap on
                // purpose: after the cart and the smelter, something that costs nothing.
                Id = "bf-sign", MainQuest = true, Track = ForgeTrackId, Kind = ChallengeKind.BuildPiece,
                Param = "SignPost", Target = 1, Display = "Name your holding",
                RewardText = "Timber and resin",
                Hint = "A sign at the door. Interact to write on it.",
            },
            new ChallengeDefinition
            {
                // LAST on the track deliberately. Haldor's camp is the one name here this assembly
                // cannot verify, and a track is a linear chain — anything behind an unfindable
                // step is unreachable, as Act I learned. Behind it there is nothing.
                Id = "bf-haldor", MainQuest = true, Track = ForgeTrackId, Kind = ChallengeKind.DiscoverLocation,
                Param = "Vendor_BlackForest", Target = 1, Display = "Find the trader",
                RewardText = "Coin enough to spend",
                Hint = "Haldor keeps a camp in the black forest. He does not move.",
            },
            new ChallengeDefinition
            {
                Id = "bf-smelter", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Smelter",
                Target = 1, Display = "Build a smelter",
                RewardText = "Ore, and surtling cores enough to never crawl a crypt again",
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
            // --- Farming (alpha41) ---
            //
            // Owner, on reaching the Black Forest: "here you start getting seeds. There should be
            // some FARMING quests. Plant seeds, tame boar." Placed where seeds actually start, and
            // after the portal — infrastructure first, then settling in.
            //
            // All three ride mechanisms that already exist: Plant and Beehive are compiled classes
            // (so a crop is detectable exactly as a campfire is), and CreatureTamed is a real
            // PlayerStatType. Nothing here names an asset.
            new ChallengeDefinition
            {
                Id = "bf-plant", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Plant",
                Target = 10, Display = "Plant a crop (10 seeds)", RewardText = "More seeds, and a queen for a hive",
                Hint = "Seeds from the forest floor, and a cultivator to break the ground. Sow at whichever homestead you keep — the first one, or a new one out here.",
            },
            new ChallengeDefinition
            {
                Id = "bf-bees", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = "Beehive",
                Target = 1, Display = "Build a beehive", RewardText = "Honey, and mead to come",
                Hint = "Needs a queen bee — one came with your seeds. Hive it under a roof, outdoors.",
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
                Id = "bf-find", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.DiscoverLocation, Param = "GDKing",
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
                // Each act opens by collecting on the last one. The trophy sat in the inventory
                // unused for the whole saga because nothing ever asked for it, and a boss power
                // you never turned on is a reward you never received (owner: "there should be a
                // quest to deliver Eikthyr\u2019s trophy and activate its power").
                //
                // Measured on the stat the game keeps for setting that power, so it is the ACT of
                // claiming that counts, not carrying the trophy around.
                Id = "sw-power", MainQuest = true, Track = MarshTrackId, Kind = ChallengeKind.StatDelta, Param = "SetGuardianPower",
                Target = 1, Display = "Claim the Elder\u2019s power",
                RewardText = "Provisions for the road", Hint = "Hang his trophy where you hung Eikthyr\u2019s. Faster felling, for the iron ahead.",
            },
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
                Id = "sw-fermenter", MainQuest = true, Track = MarshTrackId, Kind = ChallengeKind.BuildPiece, Param = "Fermenter",
                Target = 1, Display = "Build a fermenter", RewardText = "Honey and herbs for the mead",
                Hint = "Honey from a beehive, and thistle from the forest floor.",
            },
            new ChallengeDefinition
            {
                // The swamp is the first act where the map matters more than the road: crypts and
                // the altar are scattered, and the ground between them is the part that kills you.
                Id = "sw-chart", MainQuest = true, Track = MarshTrackId, Kind = ChallengeKind.BuildPiece,
                Param = "MapTable", Target = 1, Display = "Chart the marshes",
                RewardText = "Bronze and bone for the work",
                Hint = "A cartography table. Bronze, fine wood and bone fragments.",
            },
            new ChallengeDefinition
            {
                // A karve rather than a raft: the swamp's water is a road, and the act's spoils
                // are heavy. Ship is a compiled component, so this covers raft, karve and longship
                // alike — the quest is "you can travel by water now".
                Id = "sw-karve", MainQuest = true, Track = MarshTrackId, Kind = ChallengeKind.BuildPiece,
                Param = "Ship", Target = 1, Display = "Build a boat",
                RewardText = "Iron nails, and a hold worth filling",
                Hint = "At the water's edge, with a workbench nearby.",
            },
            new ChallengeDefinition
            {
                Id = "sw-sail", MainQuest = true, Track = MarshTrackId, Kind = ChallengeKind.StatDelta,
                Param = "DistanceSail", Target = 600, Display = "Sail the fens",
                RewardText = "A full hold of provisions",
                Hint = "Follow the water inland. Most crypts sit on a shore.",
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
                Id = "sw-find", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.DiscoverLocation, Param = "Bonemass",
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
                // Each act opens by collecting on the last one. The trophy sat in the inventory
                // unused for the whole saga because nothing ever asked for it, and a boss power
                // you never turned on is a reward you never received (owner: "there should be a
                // quest to deliver Eikthyr\u2019s trophy and activate its power").
                //
                // Measured on the stat the game keeps for setting that power, so it is the ACT of
                // claiming that counts, not carrying the trophy around.
                Id = "mt-power", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "SetGuardianPower",
                Target = 1, Display = "Claim Bonemass\u2019 power",
                RewardText = "Provisions for the road", Hint = "Same stones. Blunt, slash and pierce resistance \u2014 the mountains will ask for it.",
            },
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
                Id = "mt-find", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.DiscoverLocation, Param = "Dragonqueen",
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
                // Each act opens by collecting on the last one. The trophy sat in the inventory
                // unused for the whole saga because nothing ever asked for it, and a boss power
                // you never turned on is a reward you never received (owner: "there should be a
                // quest to deliver Eikthyr\u2019s trophy and activate its power").
                //
                // Measured on the stat the game keeps for setting that power, so it is the ACT of
                // claiming that counts, not carrying the trophy around.
                Id = "pl-power", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "SetGuardianPower",
                Target = 1, Display = "Claim Moder\u2019s power",
                RewardText = "Provisions for the road", Hint = "Same stones. A following wind, whichever way you sail.",
            },
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
                Id = "pl-find", MainQuest = true, Track = HuntTrackId, Kind = ChallengeKind.DiscoverLocation, Param = "GoblinKing",
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
        /// <summary>
        /// Boons a questline step awards outright, by step id.
        ///
        /// Items are the usual currency, but some steps earn something items cannot say. Sleeping
        /// a full night in a bed you built, under a roof you raised, is the run telling you the
        /// homestead is working — so it pays in the thing rest actually gives (owner: "we rested
        /// well and now have a nice place to sleep, so a Well rested boon should be awarded").
        ///
        /// Deliberately rare. A boon is the run's real power currency, normally bought with heat,
        /// and handing them out freely would make the offer wheel meaningless.
        /// </summary>
        private static readonly Dictionary<string, string> QuestBoons =
            new Dictionary<string, string>
            {
                ["mq-rest"] = "tireless",
            };

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
                // The power-claim step that opens each act pays in food, like the boss kill it
                // follows — see the boss-spoils note. Enough to set out on, not a head start.
                ["bf-power"] = new[] { ("CookedMeat", 5), ("Honey", 5) },
                ["sw-power"] = new[] { ("Sausages", 5), ("CarrotSoup", 3) },
                ["mt-power"] = new[] { ("TurnipStew", 3), ("Sausages", 5) },
                ["pl-power"] = new[] { ("WolfMeatSkewer", 5), ("OnionSoup", 3) },
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
                // The cozy steps pay in the materials the NEXT cozy step wants, so the
                // homestead funds its own decoration rather than the hunt funding it.
                ["mq-meal"] = new[] { ("CookedMeat", 5), ("Raspberry", 20), ("Mushroom", 10) },
                ["mq-upgrade"] = new[] { ("Flint", 20), ("DeerHide", 10), ("Resin", 20) },
                ["mq-comfort"] = new[] { ("DeerHide", 10), ("Resin", 20), ("Wood", 30) },
                ["bf-trophy"] = new[] { ("Wood", 30), ("Resin", 15) },
                ["mq-fish"] = new[] { ("FishingBait", 100), ("Wood", 20) },
                ["mq-fish-varied"] = new[] { ("FishingBait", 100), ("Honey", 20) },
                ["mq-fish-skill"] = new[] { ("FishingBait", 150), ("CookedMeat", 10) },
                ["mq-fish-best"] = new[] { ("FishingBait", 150), ("Honey", 20) },
                ["mq-forage"] = new[] { ("CarrotSeeds", 10), ("Honey", 10) },
                ["mq-pen"] = new[] { ("Carrot", 20), ("Raspberry", 30) },
                // The rod comes from the HOMESTEAD, not the hunt — waking in your own bed is
                // the moment the house starts giving something back, and it reads better than a
                // dead stag handing you fishing tackle.
                //
                // Safe on this track since alpha47: an unfinished hearth now carries into Act II,
                // so a rod earned here can no longer be cut off by the boss falling early. That
                // was the only reason it ever sat on the hunt track.
                ["mq-rest"] = new[] { ("CookedMeat", 10), ("ArrowFlint", 20), ("FishingRod", 1), ("FishingBait", 50) },
                // Eikthyr's altar wants two deer trophies, and a trophy is a drop the player can
                // hunt for an hour without seeing. Handing them over is the point of this step:
                // the run gates on the FIGHT, never on drop luck.
                ["mq-deer"] = new[] { ("TrophyDeer", 2), ("DeerHide", 5) },
                ["mq-spirit"] = new[] { ("Torch", 2), ("ArrowFlint", 40) },
                ["mq-gatherer"] = new[] { ("TrophyDeer", 2), ("ArrowFlint", 60), ("CookedMeat", 10) },
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
                // The plant step pays the QUEEN BEE the hive step needs, so the beehive is
                // buildable when asked — the same lesson the smelter's surtling cores taught.
                ["bf-plant"] = new[] { ("CarrotSeeds", 20), ("QueenBee", 1) },
                ["mq-tame"] = new[] { ("Carrot", 20), ("RawMeat", 10) },
                ["bf-bees"] = new[] { ("Honey", 20) },
                // Thirty cores, which is a smelter, a kiln and eleven portals — a lifetime
                // supply by any honest reckoning (owner: "getting surtling cores is not fun").
                //
                // Deliberately paid AFTER the smelter rather than before: cores are what a smelter
                // COSTS, so the step still makes you crawl one burial chamber. You do the unfun
                // thing once, prove it, and never do it again. Handing them over earlier would
                // skip the part that gives the reward its meaning.
                ["bf-smelter"] = new[] { ("CopperOre", 30), ("TinOre", 15), ("Coal", 30), ("SurtlingCore", 30) },
                ["bf-cart"] = new[] { ("BronzeNails", 40), ("Wood", 40) },
                ["bf-sign"] = new[] { ("Wood", 30), ("Resin", 20) },
                ["bf-haldor"] = new[] { ("Coins", 300) },
                ["bf-bronze"] = new[] { ("Bronze", 10), ("ArrowBronze", 40) },
                ["bf-greydwarf"] = new[] { ("ShieldBronzeBuckler", 1), ("ArrowBronze", 40) },
                ["bf-brute"] = new[] { ("ArmorRootChest", 1), ("ArmorRootLegs", 1) },
                // The seeds are the point: the Elder's altar wants three, they drop from shamans
                // and brutes, and an act finale must never gate on drop luck (see mq-deer).
                ["bf-troll"] = new[] { ("TrollHide", 10), ("AncientSeed", 3) },
                ["bf-elder"] = new[] { ("CryptKey", 1) },

                // Acts III-V, thin like their chains. Each pays the next step's tedious part and
                // the pre-boss step pays that boss's summoning items, on the Act I pattern.
                ["sw-arrive"] = new[] { ("MeadPoisonResist", 5) },
                ["sw-draugr"] = new[] { ("ArrowIron", 40), ("ShieldIronTower", 1) },
                ["sw-fermenter"] = new[] { ("Honey", 20), ("Thistle", 20) },
                ["sw-chart"] = new[] { ("Bronze", 10), ("BoneFragments", 20) },
                ["sw-karve"] = new[] { ("IronNails", 40), ("Wood", 40) },
                ["sw-sail"] = new[] { ("Sausages", 10), ("MeadHealthMedium", 3) },
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
            // Testable from Act I: you tame a boar on the hearth track, so this has something to
            // work on long before a boss falls.
            new BoonDefinition { Id = "shepherd", Display = "Shepherd", IsPassive = true, Description = "Your tamed animals hit harder and hold longer. New ones too." },
            // Act II onward. Skeletons in the Meadows would be a Black Forest answer to a Meadows
            // problem, and the flavour belongs with the burial chambers.
            new BoonDefinition { Id = "bonecaller", Display = "Bonecaller", IsPassive = false, CooldownSeconds = 180f, MinBosses = 1, Description = "Raise two skeletons to fight for you. [0]" },
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
