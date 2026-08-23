using System;
using System.Collections.Generic;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.Services;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// All of Run Mode's on-screen UI: the always-on timer/heat strip, the Heat HUD, the
    /// lobby, and the (display-only) boon offer panel.
    ///
    /// Drawn from <see cref="UIManager.OnGUI"/> from INSIDE the already-scaled GUI.matrix, so
    /// every coordinate here is in the scaled view space handed to <see cref="Draw"/> — never
    /// Screen.width/height.
    ///
    /// Every public entry point is defensive: OnGUI runs several times per frame and a throw
    /// there would take the rest of the mod's UI down with it.
    ///
    /// Visuals live in <see cref="RunTheme"/> — this file only decides layout and what to draw.
    /// </summary>
    public class RunWindow
    {
        private const int HudWindowId = 10;
        private const int LobbyWindowId = 11;
        private const int OfferWindowId = 12;

        private const float HudWidth = 420f;

        /// <summary>
        /// Width available to a row INSIDE the HUD's scroll view: the window less its padding and
        /// the vertical scrollbar. Rows are sized against this rather than eyeballed, because a
        /// horizontal group whose fixed widths overflow does not just clip — GUILayout squeezes
        /// the flexible parts to nothing, and a word-wrapping label squeezed to nothing renders
        /// as EMPTY. That is what made held boons show their "passive" tag with no name beside it.
        /// </summary>
        private const float HudContentWidth = HudWidth - 46f;

        /// <summary>Width of the status column on a held-boon row ("passive", "12s  [4]", "x1").</summary>
        private const float BoonStatusWidth = 104f;

        /// <summary>
        /// Powers every run has from the first second, shown in the HUD's BOONS list above the
        /// earned ones. They are not in the offer pool — offering something the player already
        /// has is exactly the complaint that emptied the pool of duplicates in alpha18.
        /// </summary>
        private static readonly string[] BaselineBoons =
        {
            "Hunter's Eye",
            "Pugilist (free melee & tools)",
        };

        // --- Tracker (the "Hunter's Eye" boon's panel) ---
        private const int TrackerWindowId = 13;
        private const float TrackerWidth = 340f;
        private const float TrackerHeight = 250f;

        // --- Stash ---
        //
        // Its own window, beside the tracker, rather than a section of the HUD (owner, alpha31:
        // "it clutters the main Run window"). The HUD is the thing you read at a glance mid-fight —
        // timer, heat, the current step — and a list of stored materials is neither urgent nor
        // short. Same bottom-left strip as the tracker, which is where the panels you consult
        // rather than watch already live.
        private const int StashWindowId = 14;
        private const float StashWidth = 320f;
        private const float StashHeight = 250f;

        /// <summary>
        /// Species colors, so a row keeps its identity when the list re-sorts. Distance ordering
        /// means rows swap places constantly as things move, and a wall of same-colored text is
        /// unreadable when that happens — the color is what the eye actually tracks.
        ///
        /// Exactly <see cref="TrackerMaxRows"/> entries, and that is load-bearing: it is what lets
        /// <see cref="AssignTrackerColors"/> promise that no two species on screen ever share a
        /// color. Add a row without adding a color and the promise quietly becomes a hope.
        /// </summary>
        private static readonly Color[] TrackerPalette =
        {
            ParseTrackerColor("E8DFC8"), // parchment
            ParseTrackerColor("D98C5F"), // ember
            ParseTrackerColor("7FB2E5"), // ice
            ParseTrackerColor("A9CE6B"), // moss
            ParseTrackerColor("C98BC9"), // heather
            ParseTrackerColor("E5CE6B"), // amber
            ParseTrackerColor("6FC9B6"), // verdigris
            ParseTrackerColor("D96F86"), // rose
            ParseTrackerColor("8FA9D9"), // slate
            ParseTrackerColor("C9A86F"), // bronze
        };

        /// <summary>This frame's species → color assignment; see <see cref="AssignTrackerColors"/>.</summary>
        private readonly Dictionary<string, Color> _trackerColors = new Dictionary<string, Color>();

        /// <summary>Distinct species keys on screen this frame, reused to avoid a per-frame allocation.</summary>
        private readonly List<string> _trackerKeys = new List<string>();

        /// <summary>Palette slots already claimed this frame.</summary>
        private readonly HashSet<int> _trackerSlotsTaken = new HashSet<int>();

        /// <summary>How far the Hunter's Eye reaches. The GM tracking window uses 100m; a run's
        /// version is deliberately shorter, so it answers "what is about to reach me" rather than
        /// mapping the whole valley.</summary>
        private const float TrackerRange = 70f;

        /// <summary>Most creatures listed. Past this the panel stops being readable at a glance.</summary>
        private const int TrackerMaxRows = 10;
        private const float LobbyWidth = 360f;
        private const float LobbyHeight = 280f;
        private const float OfferWidth = 460f;
        private const float OfferHeight = 200f;
        private const float StripWidth = 300f;
        private const float StripHeight = 24f;

        /// <summary>Seconds within which a second [Abandon run] / [Discard saved run] press counts as confirmation.</summary>
        private const float AbandonConfirmSeconds = 2f;

        // --- Feel-polish timings (see the state block below) ---
        private const float OfferFadeSeconds = 0.25f;
        private const float HeatPulseSeconds = 1f;
        private const float CompletionFlashSeconds = 1f;
        private const float CompletionFlashPruneSeconds = 2f;

        /// <summary>Toggled by the End key (see CheatController's command table).</summary>
        public bool Visible;

        /// <summary>
        /// Mirrors UIManager's F1 state, set each frame before <see cref="Draw"/>. During a run
        /// F1 shows the Heat HUD in place of the four GM windows, so either key opens it.
        /// </summary>
        public bool CheatUiVisible;

        private IRunService _service;
        private RunService _concrete;
        private IConfiguration _config;

        private Rect _hudRect;
        private Rect _lobbyRect;
        private Rect _offerRect;
        private Rect _trackerRect;
        private Rect _stashRect;
        private Vector2 _stashScroll;

        /// <summary>Reused across frames — a fresh list every OnGUI would churn the heap.</summary>
        private readonly List<Character> _trackerBuffer = new List<Character>();
        private float _laidOutForWidth = -1f;
        private float _laidOutForHeight = -1f;

        private float _lastAbandonPress = float.NegativeInfinity;
        private float _lastDiscardPress = float.NegativeInfinity;
        private Vector2 _hudScroll;

        // --- Feel-polish state ---
        // All of it is written ONLY at a Layout event (see the Update* methods below), for the
        // same reason ApplyPendingActions is: OnGUI fires several events per visual frame, and a
        // value that changed depending on which one just ran would make two passes of the same
        // frame disagree — fine for pure color/scale (paint-only), fatal if it ever fed a size or
        // control count. Reads happen on every event; they're safe because by the time a
        // Repaint runs, the Layout event for that same frame already settled the value.

        /// <summary>Set when the boon offer transitions from empty to non-empty; drives the fade-in.</summary>
        private float _offerShownAt = float.NegativeInfinity;
        private int _lastOfferCount;

        /// <summary>Heat last observed, and when it last went up — drives the heat pulse.</summary>
        private float _lastSeenHeat = float.NaN;
        private float _heatPulseStart = float.NegativeInfinity;

        /// <summary>
        /// Questline step last observed, and when it last changed — drives the same gold flash on
        /// the QUEST section. Tracked by the step CHANGING rather than by seeing it Done, because
        /// the engine advances the chain in the same Tick that completes it: a "done" main quest
        /// is essentially never visible to a Layout pass, whereas the swap always is. Null means
        /// "nothing seen yet", which must not flash — otherwise every run would open with one.
        /// </summary>
        // Per-track completion flash: the step each track was last seen on, and when it last changed.
        // Keyed by track id rather than held as one value, so advancing one questline does not flash
        // the other — see UpdateQuestFlash.
        private readonly Dictionary<string, string> _lastTrackStepIds = new Dictionary<string, string>();
        private readonly Dictionary<string, float> _trackFlashAt = new Dictionary<string, float>();

        /// <summary>Challenge ids seen completed, and when — drives the brief gold flash on a row.</summary>
        private readonly HashSet<string> _seenCompletedIds = new HashSet<string>();
        private readonly Dictionary<string, float> _completionFlashAt = new Dictionary<string, float>();
        private readonly List<string> _flashPruneBuffer = new List<string>();

        // Deferred lifecycle actions. A button that flips IsRunActive mid-pass would change the
        // window set between GUILayout's Layout and Repaint passes, which IMGUI answers with a
        // stream of "Mismatched LayoutGroup" errors. The buttons only raise these flags; they
        // are consumed at a Layout event (see ApplyPendingActions), so the window set only ever
        // changes between passes.
        private bool _pendingStart;
        private bool _pendingAbandon;
        private bool _pendingDiscard;

        // Stash actions defer for a related but distinct reason: both mutate the entry list this
        // section is in the middle of walking, and doing that mid-pass corrupts the layout stack
        // for every window drawn afterwards. -1 means nothing pending.
        private bool _pendingDeposit;
        private int _pendingWithdraw = -1;

        // Failure sites already logged, keyed by site + message: a new fault still gets a line,
        // a repeating one doesn't flood OnGUI. Capped so a fault with a varying message
        // (positions, timings) can't grow the set without bound.
        private const int MaxLoggedFailures = 32;
        private readonly HashSet<string> _loggedFailures = new HashSet<string>();

        // Styles are built once and reused; a new GUIStyle per OnGUI call would churn every
        // frame. Section/body/small/header/panel styles live in RunTheme — these two are local
        // because they're one-off sizes/purposes (the big timer digits, the notice line).
        private GUIStyle _stripStyle;
        private GUIStyle _noticeStyle;
        private GUIStyle _timerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;

        /// <summary>The Hud whose tip list we have already added to; guards against adding twice.</summary>
        private Hud _tippedHud;

        public void ToggleVisible() => Visible = !Visible;

        /// <summary>
        /// True while a run is in progress. UIManager asks before drawing the GM windows — they
        /// are hidden for the duration of a run. Never throws: a broken lookup reads as "no run".
        /// </summary>
        public bool RunActive
        {
            get
            {
                try
                {
                    var run = Service;
                    return run != null && run.IsRunActive;
                }
                catch (Exception ex)
                {
                    LogOnce("run-active", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Applies a [Start Run] / [Abandon run] click raised on an earlier pass, but only at a
        /// Layout event — the one point in the IMGUI cycle where changing which windows exist is
        /// safe. Called by UIManager BEFORE it reads <see cref="RunActive"/>, so the whole pass
        /// (GM windows included) sees one consistent run state; also called at the top of
        /// <see cref="Draw"/>, which is idempotent, so the guarantee holds for any caller.
        /// </summary>
        public void ApplyPendingActions()
        {
            if (!_pendingStart && !_pendingAbandon && !_pendingDiscard &&
                !_pendingDeposit && _pendingWithdraw < 0) return;
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            bool start = _pendingStart;
            bool abandon = _pendingAbandon;
            bool discard = _pendingDiscard;
            bool deposit = _pendingDeposit;
            int withdraw = _pendingWithdraw;
            _pendingStart = false;
            _pendingAbandon = false;
            _pendingDiscard = false;
            _pendingDeposit = false;
            _pendingWithdraw = -1;

            try
            {
                var run = Service;
                if (run == null) return;

                // Stash actions are handled before the lifecycle ones and do not compete with them:
                // they never change which windows exist, so they cannot be the thing that has to
                // wait, and an abandon queued in the same frame should still find the stash settled.
                if (deposit) run.DepositMaterials();
                if (withdraw >= 0) run.WithdrawStash(withdraw);

                // Abandon wins if both somehow queued: it is the safer of the two to honour.
                if (abandon) run.AbandonRun();
                else if (discard) _concrete?.DiscardPendingRun();
                else if (start) run.StartRun();
            }
            catch (Exception ex)
            {
                LogOnce("pending-action", ex);
            }
        }

        /// <summary>
        /// Cached, null-safe service lookup. ModBootstrap.GetService throws when a service is
        /// missing, which is not survivable inside OnGUI — ServiceContainer.TryGet returns null.
        /// </summary>
        private IRunService Service
        {
            get
            {
                if (_service == null)
                {
                    _service = ServiceContainer.Instance.TryGet<IRunService>();
                    _concrete = _service as RunService;
                }
                return _service;
            }
        }

        private IConfiguration Config =>
            _config ?? (_config = ServiceContainer.Instance.TryGet<IConfiguration>());

        public void Draw(float viewWidth, float viewHeight)
        {
            try
            {
                ApplyPendingActions();

                var run = Service;
                if (run == null) return;

                EnsureStyles();

                // The title card is drawn BEFORE the main-menu guard below, because the moment it
                // exists for — a loading screen — is precisely a moment with no player. It has its
                // own, tighter condition instead: the game's loading screen must actually be up,
                // which is false on the main menu (there is no Hud there at all).
                DrawSagaTitle(run, viewWidth, viewHeight);

                // Nothing Run Mode draws belongs on the main menu — not the strip, not the HUD,
                // not the offer, and not the lobby (whose Start/Discard buttons act on a world
                // that isn't there). Suspending an active run already clears the in-world case;
                // this also covers the parked-run notice and any menu frame before that lands.
                if (Player.m_localPlayer == null) return;

                Layout(viewWidth, viewHeight);

                if (run.IsRunActive)
                {
                    UpdateHeatPulse(run.Heat);
                    UpdateCompletionFlashes(run.Challenges);
                    UpdateQuestFlash(run.Challenges);
                    UpdateOfferFadeState(run.Boons?.CurrentOffer?.Count ?? 0);

                    // The strip is the one piece that survives with the rest of the UI hidden.
                    DrawStrip(run, viewWidth);

                    // The map gets the whole screen to itself: nothing on the HUD is worth
                    // reading over it, and unlike the crafting window there is no interaction the
                    // player wants from Run Mode while looking at it (owner, alpha25).
                    if ((Visible || CheatUiVisible) && !MapOpen())
                    {
                        // The crafting window is different: hiding kept it readable but put the
                        // HUD out of the mouse's reach, and its reroll and abandon buttons are the
                        // parts a player most wants while standing at a bench. So the HUD slides
                        // LEFT of it instead. The shift is one config number (RunHudMenuOffset)
                        // because the right answer depends on resolution and UI scale.
                        float offset = InventoryOpen() ? (_config?.RunHudMenuOffset ?? 470f) : 0f;
                        var hudRect = _hudRect;
                        hudRect.x = Mathf.Max(10f, _hudRect.x - offset);

                        hudRect = GUILayout.Window(HudWindowId, hudRect, DrawHud, GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(HudWidth), GUILayout.Height(_hudRect.height));

                        // Only the un-offset position is remembered, so dragging the window while
                        // a menu is open doesn't permanently shunt the HUD across the screen.
                        if (offset <= 0f) _hudRect = hudRect;

                        // Opposite side of the screen from the HUD: it is a glance-at panel, and
                        // the right edge is already carrying everything else. Baseline, not a boon
                        // (owner, alpha24: "tracking should ALWAYS be enabled").
                        _trackerRect = GUILayout.Window(TrackerWindowId, _trackerRect, DrawTracker,
                            GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(TrackerWidth), GUILayout.Height(TrackerHeight));

                        // Beside the tracker, on the same bottom strip. Always up during a run:
                        // every run has a stash, and a panel you have to summon is one you forget
                        // you have.
                        _stashRect = GUILayout.Window(StashWindowId, _stashRect, DrawStash,
                            GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(StashWidth), GUILayout.Height(StashHeight));
                    }

                    var boons = run.Boons;
                    if (boons != null && boons.CurrentOffer.Count > 0)
                    {
                        // Fade-in: alpha is a pure function of (now - _offerShownAt), a value only
                        // ever written at a Layout event above — so Layout and Repaint of the same
                        // frame compute the identical alpha. GUI.color is restored unconditionally,
                        // Window body included, so a throw inside it can't leave color state leaked
                        // onto whatever draws next.
                        float alpha = Mathf.Clamp01((Time.realtimeSinceStartup - _offerShownAt) / OfferFadeSeconds);
                        GUI.color = new Color(1f, 1f, 1f, alpha);
                        try
                        {
                            _offerRect = GUILayout.Window(OfferWindowId, _offerRect, DrawOffer, GUIContent.none, RunTheme.Panel,
                                GUILayout.Width(OfferWidth), GUILayout.Height(OfferHeight));
                        }
                        finally
                        {
                            GUI.color = Color.white;
                        }
                    }
                }
                else if (Visible)
                {
                    UpdateOfferFadeState(0);
                    _lobbyRect = GUILayout.Window(LobbyWindowId, _lobbyRect, DrawLobby, GUIContent.none, RunTheme.Panel,
                        GUILayout.Width(LobbyWidth), GUILayout.Height(LobbyHeight));
                }
            }
            catch (Exception ex)
            {
                LogOnce("draw", ex);
            }
        }

        // --- Layout ---

        private void Layout(float viewWidth, float viewHeight)
        {
            if (Mathf.Approximately(viewWidth, _laidOutForWidth) &&
                Mathf.Approximately(viewHeight, _laidOutForHeight))
            {
                return;
            }

            _laidOutForWidth = viewWidth;
            _laidOutForHeight = viewHeight;

            // Scales with the window instead of sitting at a fixed 480: the HUD carries a
            // questline step, three tasks, every held boon and a split per boss, and on a tall
            // screen there is no reason to scroll any of it.
            float hudHeight = Mathf.Clamp(viewHeight - 90f, 360f, 720f);
            _hudRect = new Rect(viewWidth - HudWidth - 10f, 40f, HudWidth, hudHeight);
            // Bottom-left, anchored to the bottom edge (owner, alpha24). The top-left belongs to
            // Valheim's own health/stamina/food readout and the hotbar; down here it is out of the
            // way of both. The window is draggable and its dragged position is kept until the game
            // window changes size, so this is a starting point rather than a decree.
            // Clear of Valheim's own health/stamina/food readout, which these used to sit under at
            // some resolutions and UI scales (owner, alpha40: "they block health and food"). The
            // margin is config for the same reason the HUD's menu offset is — the right number
            // depends on the screen, so it cannot be a constant that is right for everyone.
            float panelX = _config?.RunSidePanelX ?? 190f;

            _trackerRect = new Rect(panelX, Mathf.Max(10f, viewHeight - TrackerHeight - 10f),
                TrackerWidth, TrackerHeight);
            // Immediately right of the tracker, sharing its bottom edge. Both are draggable and
            // keep their dragged positions until the game window resizes, so this is a starting
            // point rather than a decree.
            _stashRect = new Rect(panelX + TrackerWidth + 10f, Mathf.Max(10f, viewHeight - StashHeight - 10f),
                StashWidth, StashHeight);
            _lobbyRect = new Rect((viewWidth - LobbyWidth) * 0.5f, (viewHeight - LobbyHeight) * 0.5f,
                LobbyWidth, LobbyHeight);
            _offerRect = new Rect((viewWidth - OfferWidth) * 0.5f, (viewHeight - OfferHeight) * 0.5f,
                OfferWidth, OfferHeight);
        }

        private void EnsureStyles()
        {
            if (_stripStyle != null) return;

            var font = RunTheme.ThemedFont;

            _stripStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15,
                normal = { textColor = Color.white } // tinted per-draw via GUI.contentColor
            };
            _noticeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = RunTheme.AccentGold }
            };
            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 28,
                normal = { textColor = RunTheme.TextParchment }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 46,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = RunTheme.AccentGold }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = RunTheme.TextParchment }
            };

            if (font != null)
            {
                _stripStyle.font = font;
                _noticeStyle.font = font;
                _timerStyle.font = font;
                _titleStyle.font = font;
                _subtitleStyle.font = font;
            }
        }

        // --- Feel-polish state updates (Layout-gated; see the field block above) ---

        private void UpdateHeatPulse(float heat)
        {
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            if (!float.IsNaN(_lastSeenHeat) && heat > _lastSeenHeat + 0.001f)
            {
                _heatPulseStart = Time.realtimeSinceStartup;
            }
            _lastSeenHeat = heat;
        }

        /// <summary>White-hot right after an increase, settling to the steady heat color over
        /// <see cref="HeatPulseSeconds"/>. A pure function of wall-clock time and already-committed
        /// state, so it reads identically on every event type in a frame.</summary>
        private Color HeatDisplayColor()
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - _heatPulseStart) / HeatPulseSeconds);
            return Color.Lerp(Color.white, RunTheme.HeatRed, t);
        }

        private void UpdateCompletionFlashes(ChallengeEngine challenges)
        {
            if (Event.current == null || Event.current.type != EventType.Layout) return;
            if (challenges == null) return;

            var active = challenges.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                string id = a.Def?.Id;
                if (id == null) continue;

                if (a.Done)
                {
                    if (_seenCompletedIds.Add(id)) _completionFlashAt[id] = Time.realtimeSinceStartup;
                }
                else
                {
                    // Not done (e.g. rerolled into a fresh challenge reusing the slot): allow a
                    // later completion of this id to flash again.
                    _seenCompletedIds.Remove(id);
                }
            }

            if (_completionFlashAt.Count == 0) return;

            // Bounded: a run's challenge ids are a small, finite pool, but this still prunes
            // anything past its flash window so the dict can't grow across a long run.
            _flashPruneBuffer.Clear();
            foreach (var kv in _completionFlashAt)
            {
                if (Time.realtimeSinceStartup - kv.Value > CompletionFlashPruneSeconds) _flashPruneBuffer.Add(kv.Key);
            }
            for (int i = 0; i < _flashPruneBuffer.Count; i++) _completionFlashAt.Remove(_flashPruneBuffer[i]);
        }

        private float CompletionFlash01(string id)
        {
            if (id == null || !_completionFlashAt.TryGetValue(id, out var at)) return 0f;
            return 1f - Mathf.Clamp01((Time.realtimeSinceStartup - at) / CompletionFlashSeconds);
        }

        /// <summary>
        /// Flashes a track's row gold when its step changes.
        ///
        /// Kept PER TRACK. A single shared timestamp would flash both rows whenever either advanced,
        /// which reads as "something happened over there too" — precisely the wrong signal when the
        /// whole point of two tracks is telling them apart. The first sighting of a track never
        /// flashes: seeing something for the first time is not it changing.
        /// </summary>
        private void UpdateQuestFlash(ChallengeEngine challenges)
        {
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            var tracks = challenges?.Tracks;
            if (tracks == null) return;

            foreach (var track in tracks)
            {
                if (track.Id == null) continue;

                string stepId = track.Current?.Def?.Id;
                if (_lastTrackStepIds.TryGetValue(track.Id, out var previous) && stepId != previous)
                    _trackFlashAt[track.Id] = Time.realtimeSinceStartup;

                _lastTrackStepIds[track.Id] = stepId;
            }
        }

        /// <summary>How gold a track's row should be right now: 1 just after it advanced, decaying to 0.</summary>
        private float TrackFlash01(string trackId)
        {
            if (trackId == null || !_trackFlashAt.TryGetValue(trackId, out var at)) return 0f;
            return 1f - Mathf.Clamp01((Time.realtimeSinceStartup - at) / CompletionFlashSeconds);
        }

        private void UpdateOfferFadeState(int offerCount)
        {
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            if (offerCount > 0 && _lastOfferCount <= 0) _offerShownAt = Time.realtimeSinceStartup;
            else if (offerCount <= 0) _offerShownAt = float.NegativeInfinity;
            _lastOfferCount = offerCount;
        }

        // --- Saga title card (loading screens) ---

        /// <summary>
        /// "VALHEIM: THE SAGA" over the game's loading screen, with the act underneath.
        ///
        /// Typographic rather than an image: nothing has to be drawn, nothing extra has to ship
        /// beside the DLL, and — unlike a static picture — it can say which act you are loading
        /// into, which is the part that actually means something.
        ///
        /// Shown ONLY while a saga is live or one is waiting to resume. A vanilla world loading on
        /// a vanilla save looks exactly like vanilla, which is the same rule the rest of the mode
        /// keeps: it changes the game while it is running the mode, and not otherwise.
        /// </summary>
        private void DrawSagaTitle(IRunService run, float viewWidth, float viewHeight)
        {
            if (!LoadingScreenUp()) return;
            if (!run.IsRunActive && !(_concrete?.HasPendingResume ?? false)) return;

            EnsureSagaTips();

            // Upper third rather than centred: the game puts its own progress bar and tip low, and
            // this should sit above them rather than argue with them.
            float y = viewHeight * 0.22f;

            GUI.Label(new Rect(0f, y, viewWidth, 60f), "VALHEIM: THE SAGA", _titleStyle);

            var act = run.CurrentAct;
            if (act != null)
                GUI.Label(new Rect(0f, y + 56f, viewWidth, 30f), act.Label, _subtitleStyle);

            GUI.Label(new Rect(0f, y + 88f, viewWidth, 24f), $"v{ModVersion.VERSION}", _subtitleStyle);
        }

        /// <summary>
        /// True while the game's loading screen is actually on screen.
        ///
        /// Reads the Hud's loading CanvasGroup rather than guessing from a null player: the main
        /// menu also has no player, and nothing this mod draws belongs there. On the menu there is
        /// no Hud at all, so this is false — which is exactly the discrimination needed.
        /// </summary>
        private static bool LoadingScreenUp()
        {
            try
            {
                var hud = Hud.instance;
                return hud != null && hud.m_loadingScreen != null && hud.m_loadingScreen.alpha > 0.05f;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds the saga's own lines to the game's loading-tip rotation, once per Hud.
        ///
        /// Guarded by the Hud instance it was done to, not a bool: a Hud is rebuilt per scene load,
        /// and a plain flag would leave the tips missing from every load after the first. Reference
        /// equality rather than Unity's ==, for the usual reason — a destroyed Hud must read as
        /// "different", not as null-and-therefore-skip.
        /// </summary>
        private void EnsureSagaTips()
        {
            try
            {
                var hud = Hud.instance;
                if (hud == null || hud.m_loadingTips == null) return;
                if (ReferenceEquals(hud, _tippedHud)) return;

                _tippedHud = hud;
                hud.m_loadingTips.AddRange(SagaLoadingTips);
            }
            catch
            {
                // Cosmetic. Never worth taking a load screen down for.
            }
        }

        /// <summary>
        /// Tips the saga adds to the game's rotation. Each one is something the mode actually does
        /// that a player could reasonably not know — the same test the questline hints use.
        /// </summary>
        private static readonly string[] SagaLoadingTips =
        {
            "Heat is a choice. Every quest you finish makes the world harder and you stronger.",
            "What you put in the stash follows you. You never have to carry a base to the next act.",
            "The Herald runs. Follow the tracks on the strip, not your instincts.",
            "A boss altar is only marked once the saga asks you to find it.",
            "Every boss felled is a way home. Keypad 9 returns you to your bed.",
            "Two questlines run at once. Doing both is stronger, hotter, and worth more.",
            "Power is loaned. Everything the saga grants goes back when it ends.",
        };

        // --- Strip (always on during a run, F1 or no F1) ---

        private void DrawStrip(IRunService run, float viewWidth)
        {
            var rect = new Rect((viewWidth - StripWidth) * 0.5f, 6f, StripWidth, StripHeight);

            GUI.DrawTexture(rect, RunTheme.Solid(RunTheme.PanelFill));
            RunTheme.Frame(rect, RunTheme.PanelBorder);

            var leftRect = new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
            var rightRect = new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height);

            GUI.contentColor = RunTheme.TextParchment;
            GUI.Label(leftRect, FormatTime(run.ElapsedSeconds), _stripStyle);
            GUI.contentColor = HeatDisplayColor();
            GUI.Label(rightRect, $"Heat {run.Heat:0.#}", _stripStyle);
            GUI.contentColor = Color.white;

            DrawAbilityBar(run, rect);

            // The Herald's direction, under the strip and therefore ALWAYS visible — the run window
            // does not have to be open (owner, alpha39: "it would be nice to have more frequent
            // hints about the Herald's whereabouts"). It was only ever drawn inside the quest panel,
            // so while actually playing there was nothing to follow.
            //
            // A standing line rather than repeated messages: the direction changes as you walk, and
            // something you can glance at beats something that interrupts you every thirty seconds.
            // Stacked, not overlaid: the notice line already lives immediately under the strip, and
            // two labels sharing one rect render on top of each other.
            float lineY = rect.yMax + 2f;

            string bearing = run.QuestBearing;
            if (!string.IsNullOrEmpty(bearing))
            {
                GUI.contentColor = RunTheme.AccentGold;
                GUI.Label(new Rect(rect.x - 100f, lineY, StripWidth + 200f, 20f),
                    bearing, _noticeStyle);
                GUI.contentColor = Color.white;
                lineY += 20f;
            }

            string notice = _concrete?.HudNotice;
            if (!string.IsNullOrEmpty(notice))
            {
                GUI.Label(new Rect(rect.x - 100f, lineY, StripWidth + 200f, 20f), notice, _noticeStyle);
            }
        }

        // --- Heat HUD ---

        private void DrawHud(int id)
        {
            // Each window body is its own try/catch: an exception escaping a GUILayout.Window
            // callback leaves IMGUI's clip/layout stacks unbalanced for every window after it.
            try { DrawHudBody(); }
            catch (Exception ex) { LogOnce("hud", ex); }

            GUI.DragWindow();
        }

        private void DrawHudBody()
        {
            var run = Service;
            if (run == null) return;

            // --- Header: stays out of the scroll view, so the act and the numbers are always on
            //     screen.
            //
            //     The ACT is the headline, not the clock (owner, alpha38: "this is developing into
            //     less of a speed-run-mod and more of a more complete Valheim experience"). It used
            //     to read "RUN" over a large timer, which made the first thing your eye landed on a
            //     stopwatch — and the scoring had already stopped rewarding speed: heat multiplies
            //     the score while time only divides it, so a slow thorough saga outscores a fast
            //     thin one by a wide margin. The presentation now matches the arithmetic.
            //
            //     This is also where the act line lives now; DrawQuestSection no longer repeats it.
            var act = run.CurrentAct;

            // The build, small and beside the act. It used to exist only in the Credits popup at
            // activation — which is precisely the moment you are not wondering, whereas mid-run,
            // after four builds in an afternoon, is (owner: "we should be able to see which version
            // we're playing from inside the game itself").
            GUILayout.BeginHorizontal();
            GUILayout.Label(act == null ? "SAGA" : $"SAGA — ACT {act.Numeral}", RunTheme.Header);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{ModVersion.VERSION}", RunTheme.Small);
            GUILayout.EndHorizontal();

            GUILayout.Label(act == null ? "" : act.Title.ToUpperInvariant(), _timerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(FormatTime(run.ElapsedSeconds), _stripStyle);
            GUILayout.FlexibleSpace();
            GUI.contentColor = HeatDisplayColor();
            GUILayout.Label($"Heat {run.Heat:0.#}", _stripStyle);
            GUI.contentColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Saga score {run.CurrentScore:0.##}", RunTheme.Header);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Heat's counterweight, shown beside it: every completion raises both, and seeing only
            // the cost would make the trade look worse than it is. Hidden until earned rather than
            // sitting at +0, so it reads as something the run gave you.
            if (run.EarnedHealth > 0f || run.HomewardCharges > 0)
            {
                GUILayout.BeginHorizontal();

                if (run.EarnedHealth > 0f)
                {
                    GUI.contentColor = RunTheme.CompleteGreen;
                    GUILayout.Label($"+{run.EarnedHealth:0} health earned", RunTheme.Small);
                    GUI.contentColor = Color.white;
                }

                GUILayout.FlexibleSpace();

                if (run.HomewardCharges > 0)
                {
                    GUI.contentColor = RunTheme.AccentGold;
                    GUILayout.Label($"Homeward x{run.HomewardCharges}  [9]", RunTheme.Small);
                    GUI.contentColor = Color.white;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);

            // --- Main questline: pinned above the scroll, like the timer. It is the one thing on
            //     this HUD that says where the run is GOING, so it must never scroll out of view
            //     behind a long splits list. ---
            DrawQuestSection(run);

            GUILayout.Space(6f);

            // The window height is fixed, so the body — which grows with splits, challenges and
            // held boons — scrolls. Without this it would overflow and clip the Abandon button
            // out of reach, and with the input gate on, that button is the only way out of a run.
            // Explicitly no horizontal scrollbar: every row below is sized to HudContentWidth, so
            // sideways scrolling could only ever mean a row has outgrown the window — and a HUD
            // the player has to drag sideways to read is worse than one that wraps.
            _hudScroll = GUILayout.BeginScrollView(_hudScroll, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
            // finally, not a plain call: a throw inside the body must still close the group,
            // or every window drawn after this one inherits a broken layout stack.
            try { DrawHudSections(run); }
            finally { GUILayout.EndScrollView(); }

            // --- Abandon, behind a two-press confirm so a stray click can't end a run. Outside
            //     the scroll view, so it is always reachable however long the body gets. ---
            bool armed = Time.realtimeSinceStartup - _lastAbandonPress <= AbandonConfirmSeconds;
            GUI.contentColor = armed ? RunTheme.HeatRed : Color.white;
            if (GUILayout.Button(armed ? "Abandon the saga — click again" : "Abandon the saga"))
            {
                if (armed)
                {
                    _lastAbandonPress = float.NegativeInfinity;
                    _pendingAbandon = true; // Applied at the next Layout pass.
                }
                else
                {
                    _lastAbandonPress = Time.realtimeSinceStartup;
                }
            }
            GUI.contentColor = Color.white;
        }

        /// <summary>
        /// The stash: deposit materials, take them back, from anywhere.
        ///
        /// Deliberately not an inventory screen. There is no grid and no drag-and-drop — one button
        /// puts every material in, and one button per kind takes it out. The stash exists so that
        /// moving house between acts is not an afternoon of hauling; making it a second inventory
        /// to manage would reintroduce the chore it removes.
        ///
        /// The header and the deposit button sit OUTSIDE the scroll view, so the one control you
        /// always want stays put however long the list gets — the same reasoning that keeps the
        /// abandon button out of the HUD's scroll view.
        ///
        /// Withdrawals are deferred to the next Layout pass, like the abandon button: mutating the
        /// list this loop is walking, mid-IMGUI, corrupts the layout stack for every window drawn
        /// afterwards.
        /// </summary>
        private void DrawStashBody()
        {
            var run = Service;
            if (run == null) return;

            var entries = run.StashEntries;
            int kinds = entries?.Count ?? 0;

            GUILayout.BeginHorizontal();
            GUILayout.Label("STASH", RunTheme.Header);
            GUILayout.FlexibleSpace();
            if (kinds > 0) GUILayout.Label($"{kinds} kinds", RunTheme.Small);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Deposit materials")) _pendingDeposit = true;

            GUILayout.Space(4f);

            if (kinds == 0)
            {
                GUILayout.Label("  empty — what you stash follows you\n  between bases and acts", RunTheme.Small);
                return;
            }

            _stashScroll = GUILayout.BeginScrollView(_stashScroll, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));

            // finally, not a plain call: a throw inside the body must still close the group, or
            // every window drawn after this one inherits a broken layout stack.
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];

                    GUILayout.BeginHorizontal();
                    // Quality is shown only when it is not the ordinary 1, so the common case stays
                    // quiet and an upgraded tool is obvious.
                    string label = entry.Quality > 1
                        ? $"  {entry.Prefab} +{entry.Quality - 1}"
                        : "  " + entry.Prefab;

                    GUILayout.Label(label, RunTheme.Small, GUILayout.Width(StashWidth - 150f));
                    GUILayout.Label($"{entry.Count}", RunTheme.Small, GUILayout.Width(44f));
                    if (GUILayout.Button("Take", GUILayout.Width(52f))) _pendingWithdraw = i;
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// The questlines: one row per track, each with its step, progress and reward.
        ///
        /// Both are always shown rather than one at a time. The point of two tracks is that the
        /// player chooses which thread to pull — and since every step pays heat, that choice is the
        /// difficulty dial. A thread you have to press a key to see is one you forget you have, and
        /// a dial you cannot see is not a dial.
        ///
        /// Random tasks pay in heat and boons; questlines pay in ITEMS, which is why the reward line
        /// is spelled out rather than left as a surprise. Nothing here is interactive — a questline
        /// step cannot be rerolled.
        /// </summary>
        private void DrawQuestSection(IRunService run)
        {
            // No act line here: since alpha38 the act IS the HUD's headline, drawn above by
            // DrawHudBody. Repeating it would be the third time the same words appear on one panel.
            GUILayout.Label("QUESTS", RunTheme.Header);

            var tracks = run.Challenges?.Tracks;
            if (tracks == null || tracks.Count == 0)
            {
                GUILayout.Label("  no questline", RunTheme.Small);
                return;
            }

            // Each row carries its OWN flash — see UpdateQuestFlash.
            for (int i = 0; i < tracks.Count; i++) DrawQuestTrack(tracks[i], TrackFlash01(tracks[i].Id));
        }

        /// <summary>One track's row. An exhausted track says so rather than vanishing — a row that
        /// disappeared would read as a bug, and "done" is information.</summary>
        private void DrawQuestTrack(QuestTrack track, float flash)
        {
            var quest = track.Current;

            if (quest == null)
            {
                // For the hunt track of any act but the last, this is a blink: the act flips on the
                // next boss poll and new tracks are seated within the second. A CRAFT track can sit
                // here for real, though — finishing it early is allowed, and so is never finishing
                // it before the boss falls.
                GUI.contentColor = Color.Lerp(RunTheme.CompleteGreen, RunTheme.AccentGold, flash);
                GUILayout.Label($"  {track.Label}   done", RunTheme.Small);
                GUI.contentColor = Color.white;
                return;
            }

            GUI.contentColor = Color.Lerp(RunTheme.TextParchment, RunTheme.AccentGold, flash);
            GUILayout.Label($"  {track.Label}   {quest.Def.Display}", RunTheme.Body);
            GUI.contentColor = Color.white;

            GUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            var barRect = GUILayoutUtility.GetRect(140f, 12f, GUILayout.Width(140f), GUILayout.Height(12f));
            float frac = quest.Def.Target > 0f ? quest.Progress / quest.Def.Target : 0f;
            RunTheme.Bar(barRect, frac, quest.Done ? RunTheme.CompleteGreen : RunTheme.AccentGold);
            GUILayout.Label($"{quest.Progress:0}/{quest.Def.Target:0}", RunTheme.Small, GUILayout.Width(46f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // What the step actually NEEDS, for the steps where that is not obvious. Written after
            // two play sessions lost time to exactly this — a smelter wanting surtling cores, a
            // home wanting a fire — so it sits above the reward, which is the thing you read when
            // you already know what to do.
            if (!string.IsNullOrEmpty(quest.Def.Hint))
            {
                GUILayout.Label("  " + quest.Def.Hint, RunTheme.Small);
            }

            // Live direction to whatever this step wants found — the Herald, or the biome an act
            // opens on. Not part of the definition because it depends on where you are standing.
            bool pointable =
                (quest.Def.Kind == ChallengeKind.KillPrefab && quest.Def.Param == DeerHerd.HeraldKillName)
                || quest.Def.Kind == ChallengeKind.ReachBiome;
            if (pointable)
            {
                string bearing = Service?.QuestBearing;
                if (!string.IsNullOrEmpty(bearing))
                {
                    GUI.contentColor = RunTheme.AccentGold;
                    GUILayout.Label("  " + bearing, RunTheme.Small);
                    GUI.contentColor = Color.white;
                }
            }

            if (!string.IsNullOrEmpty(quest.Def.RewardText))
            {
                GUI.contentColor = RunTheme.AccentGold;
                GUILayout.Label("  Reward: " + quest.Def.RewardText, RunTheme.Small);
                GUI.contentColor = Color.white;
            }
        }

        /// <summary>Splits, tasks and held boons — the part of the HUD that scrolls.</summary>
        private void DrawHudSections(IRunService run)
        {
            // --- Splits ---
            GUILayout.Label("SPLITS", RunTheme.Header);
            var splits = run.Splits;
            if (splits == null || splits.Count == 0)
            {
                GUILayout.Label("  no bosses down yet", RunTheme.Small);
            }
            else
            {
                foreach (var split in splits) GUILayout.Label("  " + split, RunTheme.Small);
            }

            GUILayout.Space(6f);

            // --- Tasks (the three random, rerollable slots — the questline is drawn above) ---
            GUILayout.Label("TASKS", RunTheme.Header);
            var challenges = run.Challenges;
            if (challenges == null || challenges.Active.Count == 0)
            {
                GUILayout.Label("  none active", RunTheme.Small);
            }
            else
            {
                // Frozen (wrong/missing world): rerolling here would write ApplyHeat's
                // enemy-damage/level-up modifiers into a world the run doesn't own — hide the
                // button rather than let it fire against the wrong save.
                bool frozen = _concrete?.IsFrozen ?? false;

                // Indexed, not foreach: the reroll button needs the slot index the engine uses.
                for (int i = 0; i < challenges.Active.Count; i++)
                {
                    var a = challenges.Active[i];
                    bool composite = a.Def.Subs != null && a.Def.Subs.Count > 0;

                    // Brief gold flash on completion, decaying back to the steady done/active color.
                    float flash = CompletionFlash01(a.Def.Id);
                    Color rowColor = Color.Lerp(a.Done ? RunTheme.CompleteGreen : RunTheme.TextParchment,
                        RunTheme.AccentGold, flash);

                    GUILayout.BeginHorizontal();
                    GUI.contentColor = rowColor;
                    GUILayout.Label(a.Def.Display, RunTheme.Small, GUILayout.Width(168f));
                    GUI.contentColor = Color.white;

                    // A composite's own Progress/Target are unused filler (see
                    // ChallengeDefinition.Subs) — the fraction that matters is per-sub, drawn below.
                    if (!composite)
                    {
                        var barRect = GUILayoutUtility.GetRect(70f, 12f, GUILayout.Width(70f), GUILayout.Height(12f));
                        float frac = a.Def.Target > 0f ? a.Progress / a.Def.Target : 0f;
                        RunTheme.Bar(barRect, frac, a.Done ? RunTheme.CompleteGreen : RunTheme.AccentGold);
                        GUILayout.Label($"{a.Progress:0}/{a.Def.Target:0}", RunTheme.Small, GUILayout.Width(46f));
                    }

                    GUILayout.FlexibleSpace();

                    // An above-tier leftover (only possible from a save older than the tier
                    // ladder) is unreachable content, so clearing it costs nothing — and must
                    // stay clickable at 0 heat, or the slot is dead for the rest of the run.
                    bool free = challenges.IsAboveTier(i);
                    string label = free ? "free reroll" : "reroll";
                    float width = free ? 80f : 58f;

                    if (!frozen && GUILayout.Button(label, GUILayout.Width(width))) run.RerollChallenge(i);
                    GUILayout.EndHorizontal();

                    if (!composite) continue;

                    for (int s = 0; s < a.Def.Subs.Count; s++)
                    {
                        var sub = a.Def.Subs[s];
                        float p = a.SubProgress != null && s < a.SubProgress.Count ? a.SubProgress[s] : 0f;
                        bool subDone = p >= sub.Target;
                        string text = subDone
                            ? $"  ✓ {sub.Label}"
                            : $"  · {sub.Label} ({p:0}/{sub.Target:0})";

                        GUI.contentColor = subDone ? RunTheme.CompleteGreen : RunTheme.TextMuted;
                        GUILayout.Label(text, RunTheme.Small);
                        GUI.contentColor = Color.white;
                    }
                }

                float cost = Config?.RunRerollHeatCost ?? 0f;
                if (!frozen && cost > 0f) GUILayout.Label($"  reroll costs {cost:0.#} heat", RunTheme.Small);
            }

            GUILayout.Space(6f);

            // --- Boons ---
            GUILayout.Label("BOONS", RunTheme.Header);

            // Baseline powers every run starts with, listed here so the HUD answers "what am I
            // carrying" completely rather than only listing what was picked from an offer.
            foreach (var granted in BaselineBoons)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("  " + granted, RunTheme.Small,
                    GUILayout.Width(HudContentWidth - BoonStatusWidth));
                GUILayout.Label("always on", RunTheme.Small, GUILayout.Width(BoonStatusWidth));
                GUILayout.EndHorizontal();
            }

            var boons = run.Boons;
            if (boons == null || boons.Held.Count == 0)
            {
                GUILayout.Label("  nothing earned yet", RunTheme.Small);
            }
            else
            {
                foreach (var h in boons.Held)
                {
                    bool ready = !h.Def.IsPassive && h.CooldownRemaining <= 0f &&
                        (h.Def.CooldownSeconds > 0f || h.Charges > 0);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  " + h.Def.Display, RunTheme.Small,
                        GUILayout.Width(HudContentWidth - BoonStatusWidth));
                    GUILayout.Label(BoonStatus(h), ready ? RunTheme.Ready : RunTheme.Small,
                        GUILayout.Width(BoonStatusWidth));
                    GUILayout.EndHorizontal();
                }
            }

        }

        /// <summary>Cooldown/charges plus the activation key for the three active boons.</summary>
        /// <summary>
        /// The abilities you can PRESS, always on screen under the timer — one compact row per
        /// active boon with its key and readiness. The HUD's BOONS section still lists everything
        /// including passives; this exists so the growing list never costs you the overview of
        /// what is actually usable right now.
        /// </summary>
        private void DrawAbilityBar(IRunService run, Rect strip)
        {
            var boons = run.Boons;
            if (boons == null) return;

            var actives = new List<HeldBoon>();
            foreach (var h in boons.Held)
            {
                if (!h.Def.IsPassive) actives.Add(h);
            }
            if (actives.Count == 0) return;

            const float slotW = 116f;
            const float slotH = 20f;
            float totalW = actives.Count * slotW;
            float x = strip.x + (strip.width - totalW) * 0.5f;
            float y = strip.yMax + 2f;

            for (int i = 0; i < actives.Count; i++)
            {
                var h = actives[i];
                bool ready = h.CooldownRemaining <= 0f
                    && (h.Def.CooldownSeconds > 0f || h.Charges > 0);

                var slot = new Rect(x + i * slotW, y, slotW - 4f, slotH);

                // Background tinted by readiness: a warm green wash when usable, dark panel fill
                // while cooling down or spent.
                Color bg = ready
                    ? new Color(RunTheme.CompleteGreen.r, RunTheme.CompleteGreen.g, RunTheme.CompleteGreen.b, 0.35f)
                    : new Color(RunTheme.PanelFill.r, RunTheme.PanelFill.g, RunTheme.PanelFill.b, 0.9f);
                GUI.DrawTexture(slot, RunTheme.Solid(bg));

                // Cooldown wipe: darkens the slot proportionally to time remaining, in place of
                // relying on the "12s" text alone.
                if (!ready && h.Def.CooldownSeconds > 0f)
                {
                    float remaining01 = Mathf.Clamp01(h.CooldownRemaining / h.Def.CooldownSeconds);
                    RunTheme.Radialish(slot, remaining01);
                }

                RunTheme.Frame(slot, ready ? RunTheme.AccentGold : RunTheme.PanelBorder);

                string key = ShortActivationKey(h.Def.Id);
                string state = h.CooldownRemaining > 0f
                    ? $"{h.CooldownRemaining:0}s"
                    : h.Def.CooldownSeconds <= 0f ? $"x{h.Charges}" : "";

                var style = ready ? RunTheme.Ready : RunTheme.Small;
                GUI.Label(slot, $"{key} {h.Def.Display} {state}".TrimEnd(), style);
            }
        }

        private static string ShortActivationKey(string boonId)
        {
            switch (boonId)
            {
                case "wind": return "[4]";
                case "ember": return "[5]";
                case "way": return "[6]";
                case "brother": return "[7]";
                case "windfall": return "[8]";
                default: return "";
            }
        }

        /// <summary>
        /// The right-hand status column of the held-boon list — a FIXED 104px
        /// (<see cref="BoonStatusWidth"/>), which is what this has to fit inside. It used to read
        /// "ready  [Keypad 8]", which does not, and spilled over the column.
        ///
        /// So it says what KIND of boon this is, matching the "always on" a passive prints in the
        /// same column, and nothing more. The key and the live state are not lost: the activation
        /// strip above the list already shows "[8] Windfall x1", which is the place to read them.
        /// </summary>
        private static string BoonStatus(HeldBoon h) => h.Def.IsPassive ? "always on" : "Activated";

        // --- Lobby ---

        private void DrawLobby(int id)
        {
            try { DrawLobbyBody(); }
            catch (Exception ex) { LogOnce("lobby", ex); }

            GUI.DragWindow();
        }

        private void DrawLobbyBody()
        {
            var run = Service;
            if (run == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("VALHEIM: THE SAGA", RunTheme.Header);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{ModVersion.VERSION}", RunTheme.Small);
            GUILayout.EndHorizontal();

            GUILayout.Label(run.LobbySummary(), RunTheme.Body);

            var cfg = Config;
            if (cfg != null)
            {
                GUILayout.Label(
                    $"Resources x{cfg.RunResourceRate:0.##}   Skills x{cfg.RunSkillGainRate:0.##}   " +
                    $"Par {cfg.RunParTimeMinutes:0} min",
                    RunTheme.Small);
            }

            if (run.CurrentScore > 0f)
            {
                GUILayout.Label($"Last run score: {run.CurrentScore:0.##}", RunTheme.Small);
            }

            // Outside a run the strip isn't drawn, so this is the only place a notice can be
            // seen — and the ones raised here (a refused start, an unreadable run save) are
            // exactly the ones the player must read before pressing Start again.
            string notice = _concrete?.HudNotice;
            if (!string.IsNullOrEmpty(notice))
            {
                GUILayout.Space(4f);
                GUILayout.Label(notice, _noticeStyle);
            }

            // A run parked for another world blocks Start (StartRun refuses, to protect that
            // world's only copy of its original rates) and cannot be abandoned from here, since
            // abandoning writes to the world it belongs to. If that world is never coming back,
            // this is the only way out — hence the same two-press confirm as Abandon.
            if (_concrete != null && _concrete.HasPendingResume)
            {
                GUILayout.Space(4f);
                GUILayout.Label($"Unfinished run on world '{_concrete.PendingResumeWorldName}'", RunTheme.Small);

                bool discardArmed = Time.realtimeSinceStartup - _lastDiscardPress <= AbandonConfirmSeconds;
                GUI.contentColor = discardArmed ? RunTheme.HeatRed : Color.white;
                if (GUILayout.Button(discardArmed ? "Discard saved run — click again" : "Discard saved run"))
                {
                    if (discardArmed)
                    {
                        _lastDiscardPress = float.NegativeInfinity;
                        _pendingDiscard = true; // Applied at the next Layout pass.
                    }
                    else
                    {
                        _lastDiscardPress = Time.realtimeSinceStartup;
                    }
                }
                GUI.contentColor = Color.white;
                GUILayout.Label("That world keeps Run Mode's rates.", RunTheme.Small);
            }

            GUILayout.Space(8f);
            // Deferred: starting a run here would change the window set mid-pass.
            if (GUILayout.Button("Begin the saga")) _pendingStart = true;

            GUILayout.Space(6f);
            GUILayout.Label("GM mode is disabled while a run is live.", RunTheme.Small);

            // No kill-hook warning here by design: KillHookAvailable is unconditionally true
            // outside a run (the grace clock only advances while one is in progress, and the
            // injected hook cannot prove itself until something dies), so a lobby line would
            // either never appear or cry wolf. The real surface is RunService's in-run notice,
            // raised on the strip once the 60s grace window closes with the hook still silent.
        }

        // --- Tracker panel (the "Hunter's Eye" boon) ---

        private void DrawTracker(int id)
        {
            try { DrawTrackerBody(); }
            catch (Exception ex) { LogOnce("tracker", ex); }

            GUI.DragWindow();
        }

        // --- Stash panel ---

        private void DrawStash(int id)
        {
            try { DrawStashBody(); }
            catch (Exception ex) { LogOnce("stash", ex); }

            GUI.DragWindow();
        }

        /// <summary>
        /// Nearby hostiles by distance, with health — the same reading the GM mode's Tracking
        /// window gives, earned as a boon instead of switched on at will.
        ///
        /// Pure observation: it reads live state and writes nothing, which is what makes it a
        /// legitimate loaned power — losing the boon takes the panel with it and leaves no trace
        /// to unwind. Tamed creatures and players are skipped; a summoned wolf is not a threat
        /// worth a row.
        /// </summary>
        private void DrawTrackerBody()
        {
            GUILayout.Label("HUNTER'S EYE", RunTheme.Header);

            var player = Player.m_localPlayer;
            if (player == null) return;

            _trackerBuffer.Clear();
            Character.GetCharactersInRange(player.transform.position, TrackerRange, _trackerBuffer);

            Vector3 origin = player.transform.position;
            _trackerBuffer.RemoveAll(c => c == null || c.IsPlayer() || c.IsTamed());
            _trackerBuffer.Sort((a, b) =>
                Utils.DistanceXZ(a.transform.position, origin)
                    .CompareTo(Utils.DistanceXZ(b.transform.position, origin)));

            if (_trackerBuffer.Count == 0)
            {
                GUILayout.Label("  nothing stirring", RunTheme.Small);
                return;
            }

            int rows = Mathf.Min(_trackerBuffer.Count, TrackerMaxRows);
            AssignTrackerColors(rows);

            for (int i = 0; i < rows; i++)
            {
                var c = _trackerBuffer[i];
                float dist = Utils.DistanceXZ(c.transform.position, origin);
                float hp01 = Mathf.Clamp01(c.GetHealthPercentage());
                Color species = SpeciesColor(c);

                GUILayout.BeginHorizontal();

                GUI.contentColor = species;
                GUILayout.Label(c.GetHoverName(), RunTheme.Small, GUILayout.Width(150f));
                GUILayout.Label($"{dist:0}m", RunTheme.Small, GUILayout.Width(38f));
                GUI.contentColor = Color.white;

                // The bar reads as CLOSING DISTANCE: full at the edge of the eye's reach, draining
                // to nothing as the thing arrives. An emptying bar is a countdown, which is the
                // right way round for something walking towards you.
                var barRect = GUILayoutUtility.GetRect(64f, 12f, GUILayout.Width(64f), GUILayout.Height(12f));
                RunTheme.Bar(barRect, Mathf.Clamp01(dist / TrackerRange), species);

                GUILayout.Label($"{hp01 * 100f:0}%", RunTheme.Small, GUILayout.Width(38f));
                GUILayout.EndHorizontal();
            }

            if (_trackerBuffer.Count > rows)
            {
                GUILayout.Label($"  ...and {_trackerBuffer.Count - rows} more", RunTheme.Small);
            }
        }

        /// <summary>
        /// True while the inventory/crafting window is up. Verified against the IL:
        /// InventoryGui.IsVisible is a public static. Any failure reads as "closed", so a game
        /// update that moves it costs an overlapping crafting window, never a HUD the player
        /// cannot get back.
        /// </summary>
        private static bool InventoryOpen()
        {
            try { return InventoryGui.IsVisible(); }
            catch { return false; }
        }

        /// <summary>True while the full-screen map is up (Minimap.IsOpen, a public static).</summary>
        private static bool MapOpen()
        {
            try { return Minimap.IsOpen(); }
            catch { return false; }
        }

        /// <summary>
        /// Assigns each species on screen a color no other visible species is using, for the rows
        /// about to be drawn.
        ///
        /// The hash alone was not enough. It gives a species a stable preferred slot, but nothing
        /// stopped two species preferring the same one, and this particular hash clusters hard at
        /// `% 8`: boar and deer both landed on parchment, and greyling, neck, greydwarf, troll and
        /// crow ALL landed on rose. Five species sharing a color is exactly the wall of same-colored
        /// text the palette exists to prevent.
        ///
        /// So the hash still picks a PREFERENCE, and a species alone on screen always gets it — but
        /// when a slot is already claimed, the next free one is taken instead. With as many colors
        /// as <see cref="TrackerMaxRows"/>, a free slot always exists.
        ///
        /// Assignment runs over the species keys in ORDINAL order, never in the buffer's distance
        /// order. Distance order changes as things move, so two contending species would swap colors
        /// every time they passed each other — the flicker would be worse than the collision.
        /// Sorting by key means the same set of species always produces the same assignment.
        ///
        /// The trade-off, stated plainly: a species CAN change color when a different species walks
        /// into range and takes the slot it preferred. Stable while it is the only claimant, which
        /// is the common case.
        /// </summary>
        private void AssignTrackerColors(int rows)
        {
            _trackerColors.Clear();
            _trackerKeys.Clear();
            _trackerSlotsTaken.Clear();

            for (int i = 0; i < rows; i++)
            {
                var c = _trackerBuffer[i];
                string key = c == null ? null : c.m_name;
                if (!string.IsNullOrEmpty(key) && !_trackerKeys.Contains(key)) _trackerKeys.Add(key);
            }

            _trackerKeys.Sort(StringComparer.Ordinal);

            foreach (var key in _trackerKeys)
            {
                int preferred = PreferredSlot(key);
                int slot = preferred;

                for (int probe = 0; probe < TrackerPalette.Length; probe++)
                {
                    slot = (preferred + probe) % TrackerPalette.Length;
                    if (!_trackerSlotsTaken.Contains(slot)) break;
                }

                _trackerSlotsTaken.Add(slot);
                _trackerColors[key] = TrackerPalette[slot];
            }
        }

        /// <summary>
        /// A species' preferred palette slot, keyed on Character.m_name — the shared localization
        /// token ("$enemy_greydwarf"), which every instance carries and which does not change with
        /// level or with the "(Clone)" suffix on the object name.
        ///
        /// Deliberately not string.GetHashCode: .NET does not guarantee it is stable across runtimes
        /// or runs, and a color that changed between sessions would defeat the point.
        /// </summary>
        private static int PreferredSlot(string key)
        {
            int hash = 17;
            for (int i = 0; i < key.Length; i++) hash = unchecked(hash * 31 + key[i]);

            return Mathf.Abs(hash) % TrackerPalette.Length;
        }

        /// <summary>This frame's color for a species, from <see cref="AssignTrackerColors"/>.</summary>
        private Color SpeciesColor(Character c)
        {
            string key = c == null ? null : c.m_name;

            return !string.IsNullOrEmpty(key) && _trackerColors.TryGetValue(key, out var color)
                ? color
                : RunTheme.TextParchment;
        }

        private static Color ParseTrackerColor(string hex)
        {
            return new Color(
                Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
                Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
                Convert.ToInt32(hex.Substring(4, 2), 16) / 255f,
                1f);
        }

        // --- Boon offer (display only; picks are handled in RunService.Tick) ---

        private void DrawOffer(int id)
        {
            try { DrawOfferBody(); }
            catch (Exception ex) { LogOnce("offer", ex); }

            GUI.DragWindow();
        }

        private void DrawOfferBody()
        {
            var boons = Service?.Boons;
            if (boons == null) return;

            var offer = boons.CurrentOffer;

            GUILayout.Label("BOON OFFER", RunTheme.Header);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < offer.Count; i++)
            {
                GUILayout.BeginVertical(GUILayout.Width(OfferWidth / 3f - 12f));

                GUILayout.BeginHorizontal();
                GUI.contentColor = RunTheme.AccentGold;
                GUILayout.Label($"{i + 1}", RunTheme.Header, GUILayout.Width(20f));
                GUI.contentColor = Color.white;
                GUILayout.Label(offer[i].Display, RunTheme.Body);
                GUILayout.EndHorizontal();

                GUILayout.Label(offer[i].IsPassive ? "passive" : "active", RunTheme.Small);
                if (!string.IsNullOrEmpty(offer[i].Description))
                    GUILayout.Label(offer[i].Description, RunTheme.Body);
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.Label("press Keypad 1/2/3", RunTheme.Small);
        }

        // --- Helpers ---

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>
        /// Logs a failure once per distinct site+message. OnGUI runs several times a frame, so
        /// an unfiltered log would flood — but a single bool would mean the first fault ever
        /// silences every later, unrelated one. Mirrors RunService.LogOnce, with the exception's
        /// message folded into the key so a different failure at the same site still surfaces.
        /// </summary>
        private void LogOnce(string site, Exception ex)
        {
            string key = site + ":" + (ex?.Message ?? string.Empty);
            if (_loggedFailures.Contains(key)) return;
            if (_loggedFailures.Count >= MaxLoggedFailures) return;

            _loggedFailures.Add(key);
            Debug.LogError(
                $"[ICanShowYouTheWorld] Run UI '{site}' failed (further identical occurrences suppressed): {ex}");
        }

        // TODO(sfx): Valheim SFX hooks for offer-appear, heat-tick and challenge-complete are a
        // follow-up — this pass is visuals-only.
    }
}
