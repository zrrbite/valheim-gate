using System;
using System.Collections.Generic;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.Services;
using System.Linq;
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

        /// <summary>
        /// The GM page's own size. Thirty-four bindings, each a key and a sentence, need more room
        /// than four buttons and a paragraph do - so this page is both taller and wider than the
        /// saga page, and the window grows down and to the right when you switch to it.
        /// </summary>
        private const float GmPageWidth = 470f;
        private const float GmPageHeight = 460f;
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

        /// <summary>Indent of a sub-objective row, and the width its count column gets.</summary>
        private const float SubIndent = 26f;

        /// <summary>
        /// Wide enough for "100/100" at this font. Fixed, because a column that resizes per row is
        /// not a column and the counts stop lining up - which was the original complaint.
        /// </summary>
        private const float SubCountColumn = 56f;

        /// <summary>A "Not now" click on the lobby, applied at the next Layout pass.</summary>
        private bool _pendingLobbyClose;

        /// <summary>An "open/hide the GM windows" click, applied at the next Layout pass.</summary>
        private bool _pendingGmToggle;

        /// <summary>The Hud whose tip list we have already added to; guards against adding twice.</summary>
        private Hud _tippedHud;

        public void ToggleVisible() => Visible = !Visible;

        /// <summary>Which page of the run window is showing.</summary>
        private enum HudPage
        {
            /// <summary>What you act on: the numbers, the step in play, the tasks, the boons.</summary>
            Run,

            /// <summary>
            /// What you have done, and the detail there was never room for. Labelled BOOK.
            /// </summary>
            /// <remarks>
            /// Called QUESTS for one build and renamed on the owner's question ("We should make sure
            /// that it tells a story of what we went through, so its sort of a book. 'Quest log' or
            /// just 'Book'?"). BOOK, because it is the shorter word and the truer one: a quest log
            /// lists what is outstanding, and this page's centre of gravity is the CHRONICLE - what
            /// the run has already been through, in the words that were said at the time. The enum
            /// member keeps its old name so the diff stays about the page rather than about renaming.
            /// </remarks>
            Quests,
        }

        /// <summary>
        /// The page in view. One window with two pages rather than a second window, and rather than
        /// a new key.
        /// </summary>
        /// <remarks>
        /// The window had grown to act headline, score, gates, three quest tracks with their
        /// sub-objectives, splits, homestead, tasks and boons in one scroll (owner: "Its getting kind
        /// of cluttered... a seperate quest log that has the main quests which gives us a chance to
        /// be a bit more descriptive, leaving some spare room in the main run menu").
        ///
        /// The rule the split follows, which is broader than "move the quests out" and is the part
        /// worth stating: THE RUN PAGE HOLDS WHAT YOU ACT ON, THE QUESTS PAGE HOLDS WHAT YOU HAVE
        /// DONE AND THE DETAIL BEHIND IT. So splits and homestead move (records, not decisions), the
        /// step hints move (they are spoken aloud as a step opens now, so the panel copy was a second
        /// telling), and tasks and boons stay where they are, because those are live choices.
        ///
        /// What does NOT move is the step in play. There is a decision recorded below that the
        /// questline is pinned above the scroll because it is the one thing on this panel that says
        /// where the run is GOING - learned in play, and not undone by a tidy-up.
        ///
        /// A page rather than a key because the keypad and the Home/End cluster are both full, and a
        /// log is a page of this window rather than a mode of its own. Not a separate window either:
        /// a second draggable panel is more clutter, differently arranged.
        /// </remarks>
        private HudPage _page = HudPage.Run;

        /// <summary>Which page the menu shows when no run is running.</summary>
        private enum LobbyPage
        {
            /// <summary>Begin, resume, discard — the saga's own door.</summary>
            Saga,

            /// <summary>The old cheat mod's door and its key table. GM builds only.</summary>
            Gm,
        }

        /// <summary>
        /// The page shown outside a run, making this window the mode's general menu rather than the
        /// saga's lobby.
        /// </summary>
        /// <remarks>
        /// Owner: "the 'run' menu... should be a sort of general menu. IF it's built with -Dev mode
        /// we can access both the GM mod of old and the new Saga mode and we can use the menu to go
        /// back and forth."
        ///
        /// Two things are deliberately narrower than that sentence. The GM page exists only in a GM
        /// BUILD (ModVersion.GmEnabled), not with dev mode, because the two ended up being different
        /// switches living in different places - dev mode is the tester's step-skips and kits, and
        /// the flavour is baked into the DLL where nobody can edit it.
        ///
        /// And "back and forth" means outside a run only. There is no GM page during one: the input
        /// gate exists because heat and score assume GM is dead, and a menu does not get to
        /// negotiate that.
        /// </remarks>
        private LobbyPage _lobbyPage = LobbyPage.Saga;

        /// <summary>The GM page's key table scrolls; a saga build never draws it.</summary>
        private Vector2 _gmScroll;

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
        /// True when the saga is waiting to offer itself, so UIManager knows this pass must reach
        /// <see cref="Draw"/> even with nothing visible yet. Never throws: a broken lookup reads as
        /// "no", and a missed offer is a key press rather than a fault.
        /// </summary>
        public bool LobbyWanted
        {
            get
            {
                try
                {
                    var run = Service;
                    return run != null && run.WantsLobbyShown;
                }
                catch (Exception ex)
                {
                    LogOnce("lobby-wanted", ex);
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
                !_pendingDeposit && _pendingWithdraw < 0 && !_pendingLobbyClose &&
                !_pendingGmToggle) return;
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            // Closing the lobby removes a window, so it waits for Layout with the rest. It touches
            // no service, so it is handled here and returns nothing to the try below.
            if (_pendingLobbyClose)
            {
                _pendingLobbyClose = false;
                Visible = false;
            }

            // Adds or removes the GM windows, so it waits for Layout with everything else that
            // changes which windows exist.
            if (_pendingGmToggle)
            {
                _pendingGmToggle = false;
                try { UIManager.Instance?.ToggleVisible(); }
                catch (Exception ex) { LogOnce("gm-toggle", ex); }
            }

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
                // Withdraw BEFORE deposit: the index was read from the list as it looked last frame,
                // and the stash is kept sorted, so a deposit that adds a new kind can shift every
                // row after it. Two stash buttons cannot realistically be clicked in one frame, but
                // the order costs nothing and removes the question.
                if (withdraw >= 0) run.WithdrawStash(withdraw);
                if (deposit) run.DepositMaterials();

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

                // The act-transition card: the same big type as the loading title, in-world, for
                // ten seconds after a boss falls. Faded in and out so it reads as an event rather
                // than a popup. Drawn before the strip so nothing sits on top of it.
                DrawActCard(run, viewWidth, viewHeight);

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
                    // The TRACKER lives outside the End toggle: it is the panel a player glances
                    // at constantly, and hiding it with the main window made End an all-or-nothing
                    // choice (owner: "End should leave track window open"). Only the map claims
                    // the whole screen.
                    //
                    // The stash used to sit here beside it and no longer does (owner: "I think we
                    // should also remove the stash from END"). It fails the same test the tracker
                    // passes: nothing about it changes while you play, so a permanently open
                    // deposit panel is a box of buttons taking up a corner. It is drawn with the
                    // HUD below, so End brings it up along with everything else you came to press.
                    if (!MapOpen())
                    {
                        _trackerRect = GUILayout.Window(TrackerWindowId, _trackerRect, DrawTracker,
                            GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(TrackerWidth), GUILayout.Height(TrackerHeight));
                    }

                    if ((Visible || CheatUiVisible) && !MapOpen())
                    {
                        _stashRect = GUILayout.Window(StashWindowId, _stashRect, DrawStash,
                            GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(StashWidth), GUILayout.Height(StashHeight));

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
                else
                {
                    // The saga offers itself on entering a world, once. The service owns the WHEN
                    // (see RunService.WantsLobbyShown, which keys on world identity rather than on
                    // the player reference for a reason worth reading); this only opens the door and
                    // says it has been opened, so a CANCEL stays cancelled.
                    // At a LAYOUT event only. Unity runs OnGUI several times a frame, and flipping
                    // which windows exist part-way through leaves IMGUI's layout groups mismatched
                    // for every window drawn after - the same rule ApplyPendingActions obeys, and
                    // Layout is the first pass, so every later pass of the frame agrees with it.
                    if (!Visible && run.WantsLobbyShown &&
                        Event.current != null && Event.current.type == EventType.Layout)
                    {
                        Visible = true;
                        run.LobbyOfferTaken();
                    }

                    if (Visible)
                    {
                        UpdateOfferFadeState(0);
                        // The GM page carries a thirty-four row key table and wants the room; the
                        // saga page is four buttons and a paragraph and does not.
                        bool gmPage = ModVersion.GmEnabled && _lobbyPage == LobbyPage.Gm;
                        float lobbyWidth  = gmPage ? GmPageWidth  : LobbyWidth;
                        float lobbyHeight = gmPage ? GmPageHeight : LobbyHeight;

                        _lobbyRect = GUILayout.Window(LobbyWindowId, _lobbyRect, DrawLobby, GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(lobbyWidth), GUILayout.Height(lobbyHeight));
                    }
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
            float panelX = _config?.RunSidePanelX ?? 75f;

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
        private const float ActCardSeconds = 10f;

        /// <summary>
        /// ACT II — WHERE THE LIGHT GOES, large, centre-upper screen, with the act's epigraph
        /// under it. The chat line and the game's own center message were both missable in the
        /// chaos right after a boss kill, which is exactly when an act changes.
        /// </summary>
        private void DrawActCard(IRunService run, float viewWidth, float viewHeight)
        {
            float age = Time.time - run.ActCardShownAt;
            if (age < 0f || age > ActCardSeconds || string.IsNullOrEmpty(run.ActCardTitle)) return;

            // One second in, one second out.
            float alpha = Mathf.Clamp01(age) * Mathf.Clamp01(ActCardSeconds - age);

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            float y = viewHeight * 0.24f;
            GUI.Label(new Rect(0f, y, viewWidth, 60f), run.ActCardTitle, _titleStyle);

            if (!string.IsNullOrEmpty(run.ActCardEpigraph))
            {
                GUI.contentColor = RunTheme.AccentGold;
                GUI.Label(new Rect(0f, y + 58f, viewWidth, 30f), run.ActCardEpigraph, _subtitleStyle);
                GUI.contentColor = Color.white;
            }

            GUI.color = prev;
        }

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

            float abilityHeight = DrawAbilityBar(run, rect);

            // The Herald's direction, under the strip and therefore ALWAYS visible — the run window
            // does not have to be open (owner, alpha39: "it would be nice to have more frequent
            // hints about the Herald's whereabouts"). It was only ever drawn inside the quest panel,
            // so while actually playing there was nothing to follow.
            //
            // A standing line rather than repeated messages: the direction changes as you walk, and
            // something you can glance at beats something that interrupts you every thirty seconds.
            // Stacked, not overlaid: the notice line already lives immediately under the strip, and
            // two labels sharing one rect render on top of each other.
            float lineY = rect.yMax + 2f + abilityHeight;

            string bearing = run.QuestBearing;
            if (!string.IsNullOrEmpty(bearing))
            {
                // Outlined, not just tinted. This line has the whole world behind it and no
                // panel, so no single colour survives every backdrop.
                RunTheme.ShadowedLabel(new Rect(rect.x - 100f, lineY, StripWidth + 200f, 20f),
                    bearing, _noticeStyle, RunTheme.AccentGoldBright);
                lineY += 20f;
            }

            // A BAR under the rumour, because the rumour is deliberately vague and vague prose
            // cannot tell warm from cold. Owner, having chased the light: "not sure how close I
            // was" — after reading atmosphere lines that sound like hints and carry nothing.
            //
            // Two things can want it, never both: the light burning down at a carcass is always
            // the more urgent, so it wins.
            float urgency = run.LightUrgency;
            float closeness = run.SpiritCloseness;

            if (urgency >= 0f || closeness >= 0f)
            {
                bool racing = urgency >= 0f;
                float fill = racing ? urgency : closeness;

                // Red as a light burns out, gold as you close on one. The colour says which
                // question the bar is answering without a label.
                Color tint = racing
                    ? Color.Lerp(RunTheme.HeatRed, RunTheme.AccentGold, urgency)
                    : Color.Lerp(RunTheme.AccentGold, RunTheme.CompleteGreen, closeness);

                const float BarWidth = 160f;
                var bar = new Rect(rect.x + (StripWidth - BarWidth) * 0.5f, lineY + 2f, BarWidth, 6f);
                RunTheme.Bar(bar, Mathf.Clamp01(fill), tint);

                lineY += 12f;

                if (racing && run.LightsBurning > 1)
                {
                    RunTheme.ShadowedLabel(new Rect(rect.x - 100f, lineY, StripWidth + 200f, 18f),
                        $"{run.LightsBurning} lights burning", _noticeStyle, RunTheme.TextParchment);
                    lineY += 18f;
                }
            }

            string notice = _concrete?.HudNotice;
            if (!string.IsNullOrEmpty(notice))
            {
                RunTheme.ShadowedLabel(new Rect(rect.x - 100f, lineY, StripWidth + 200f, 20f),
                    notice, _noticeStyle, RunTheme.AccentGoldBright);
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

            // Body type, not Header: the header style carries extra line height that made this
            // row taller than its neighbours for no reason — the wonk was padding, not content.
            GUILayout.Label($"Saga score {run.CurrentScore:0.##}", RunTheme.Body);

            // Heat's counterweight, shown beside it: every completion raises both, and seeing only
            // the cost would make the trade look worse than it is. Hidden until earned rather than
            // sitting at +0, so it reads as something the run gave you.
            if (run.EarnedHealth > 0f || run.HomewardCharges > 0 || run.HomewardReady || run.HomewardCooldown > 0f)
            {
                GUILayout.BeginHorizontal();

                if (run.EarnedHealth > 0f)
                {
                    GUI.contentColor = RunTheme.CompleteGreen;
                    GUILayout.Label($"+{run.EarnedHealth:0} health earned", RunTheme.Small);
                    GUI.contentColor = Color.white;
                }

                GUILayout.FlexibleSpace();

                // Charges when held, otherwise the free gate's state. Always one line, because a
                // way home you have to open a window to check is not a safety net.
                if (run.HomewardCharges > 0)
                {
                    GUI.contentColor = RunTheme.AccentGold;
                    GUILayout.Label($"Homeward x{run.HomewardCharges}  [9]", RunTheme.Small);
                }
                else if (run.HomewardReady)
                {
                    GUI.contentColor = RunTheme.AccentGold;
                    GUILayout.Label("Homeward ready  [9]", RunTheme.Small);
                }
                else
                {
                    GUI.contentColor = RunTheme.TextMuted;
                    int secs = Mathf.CeilToInt(run.HomewardCooldown);
                    GUILayout.Label($"Homeward {secs / 60}:{secs % 60:00}", RunTheme.Small);
                }

                GUI.contentColor = Color.white;

                GUILayout.EndHorizontal();
            }

            // The way back to a corpse, shown only while there IS one. Its key has to be named
            // here: PageDown is a key the saga borrows from the cheat mod, and the rule that makes
            // borrowing safe carries the condition that the saga says which key it took.
            if (run.CorpseWaiting)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (run.CorpseGateCooldown > 0f)
                {
                    GUI.contentColor = RunTheme.TextMuted;
                    int secs = Mathf.CeilToInt(run.CorpseGateCooldown);
                    GUILayout.Label($"Where you fell {secs / 60}:{secs % 60:00}", RunTheme.Small);
                }
                else
                {
                    GUI.contentColor = RunTheme.AccentGold;
                    GUILayout.Label("Where you fell  [PgDn]", RunTheme.Small);
                }

                GUI.contentColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);

            DrawPageTabs();

            GUILayout.Space(2f);

            // --- Main questline: pinned above the scroll, like the timer. It is the one thing on
            //     this HUD that says where the run is GOING, so it must never scroll out of view
            //     behind a long splits list. Kept on the RUN page for exactly that reason when the
            //     window gained pages - see HudPage. ---
            if (_page == HudPage.Run) DrawQuestSection(run);

            GUILayout.Space(4f);

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
            try
            {
                if (_page == HudPage.Quests) DrawQuestLog(run);
                else DrawHudSections(run);
            }
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
        /// <summary>
        /// The page selector: two buttons that look like a choice and cost no key.
        /// </summary>
        /// <remarks>
        /// Deliberately drawn ABOVE the scroll view and below the header, so it does not move when
        /// the page under it changes length. A tab row that shifts as you switch pages is a tab row
        /// you misclick.
        /// </remarks>
        private void DrawPageTabs()
        {
            GUILayout.BeginHorizontal();

            DrawPageTab("RUN", HudPage.Run);
            DrawPageTab("BOOK", HudPage.Quests);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawPageTab(string label, HudPage page)
        {
            bool active = _page == page;

            // The selected tab is gold and unclickable; the other is muted and is the button. No
            // custom style: contentColor over the skin's button is enough to say which is which,
            // and this panel already tints everything else the same way.
            GUI.contentColor = active ? RunTheme.AccentGoldBright : RunTheme.TextMuted;
            if (GUILayout.Button(active ? "• " + label : label, GUILayout.Width(88f)) && !active)
                _page = page;
            GUI.contentColor = Color.white;
        }

        /// <summary>
        /// The quest log: what each track has already done, what it is doing, and how much is left
        /// as a COUNT.
        /// </summary>
        /// <remarks>
        /// No new state. <see cref="QuestTrack"/> already carries the whole Chain plus the Index
        /// into it, so everything before the index is finished, the index is in play, and the rest
        /// is to come - the log is a different reading of what the HUD already has.
        ///
        /// Finished steps are the reason this page is worth having. A completed step used to simply
        /// vanish, so a run had no memory the player could read, which for a mode calling itself a
        /// saga is the page it was missing. Each one shows its Opening line where it has one, since
        /// that line WAS the beat: "The forest sent nothing for this one" reads as a record of the
        /// night it was said.
        ///
        /// Steps still to come are a COUNT and never a list. Naming them would spoil the act, and
        /// the same objection already took reward text off the step rows ("takes up too much space
        /// and ruins surprise"). A number answers "how much of this act is left" without answering
        /// "what happens next", which is the only one of those two questions the player wants.
        /// </remarks>
        private void DrawQuestLog(IRunService run)
        {
            DrawTitlePage();

            // The book opens on what has HAPPENED. The live tracks come after it, because a book
            // whose first page is a to-do list is a to-do list.
            string writtenAct = DrawChronicle(run);

            var tracks = run.Challenges?.Tracks;
            if (tracks == null || tracks.Count == 0)
            {
                GUILayout.Label("  no questline", RunTheme.Small);
                return;
            }

            var act = run.CurrentAct;

            // The heading only where the chronicle has not already opened this chapter. An act
            // whose beats are written above is the chapter being read, so repeating its title here
            // printed the same words twice within a few rows - the same objection that took the
            // duplicate act banner off the transition card.
            if (act != null && act.Numeral != writtenAct) OpenChapter(run, act.Numeral);

            // Where the writing stops. Said as a line in the book rather than as a section label,
            // because "ACT II - NOW" was the one row on this page that read like a UI.
            GUI.contentColor = RunTheme.TextMuted;
            GUILayout.Label(act == null
                ? "  The rest is not written yet."
                : "  Here the writing stops. The rest is yours to do.", RunTheme.Small);
            GUI.contentColor = Color.white;
            GUILayout.Space(4f);

            if (act != null && !string.IsNullOrEmpty(act.Epigraph))
            {
                GUI.contentColor = RunTheme.AccentGold;
                GUILayout.Label(act.Epigraph, RunTheme.Small);
                GUI.contentColor = Color.white;
                GUILayout.Space(4f);
            }

            foreach (var track in tracks)
            {
                if (track == null) continue;

                var chain = track.Chain ?? new List<ChallengeDefinition>();

                GUILayout.BeginHorizontal();
                GUILayout.Label(track.Label, RunTheme.Header);
                GUILayout.FlexibleSpace();
                GUI.contentColor = RunTheme.TextMuted;
                GUILayout.Label($"{Mathf.Min(track.Index, chain.Count)}/{chain.Count}", RunTheme.Small);
                GUI.contentColor = Color.white;
                GUILayout.EndHorizontal();

                // --- Done, in the order it happened.
                for (int i = 0; i < chain.Count && i < track.Index; i++)
                {
                    var def = chain[i];
                    if (def == null) continue;

                    GUI.contentColor = RunTheme.CompleteGreen;
                    GUILayout.Label($"  ✓ {def.Display}", RunTheme.Small);
                    GUI.contentColor = Color.white;

                    if (!string.IsNullOrEmpty(def.Opening))
                    {
                        GUI.contentColor = RunTheme.TextMuted;
                        GUILayout.Label("      " + def.Opening, RunTheme.Small);
                        GUI.contentColor = Color.white;
                    }
                }

                // --- In play, with the detail the RUN page has no room for.
                var quest = track.Current;
                if (quest != null)
                {
                    GUI.contentColor = track.Blocked ? RunTheme.TextMuted : RunTheme.AccentGoldBright;
                    GUILayout.Label($"  ▸ {quest.Def.Display}", RunTheme.Body);
                    GUI.contentColor = Color.white;

                    if (track.Blocked && !string.IsNullOrEmpty(quest.Def.BlockedText))
                    {
                        GUI.contentColor = RunTheme.AccentGold;
                        GUILayout.Label("      " + quest.Def.BlockedText, RunTheme.Small);
                        GUI.contentColor = Color.white;
                    }

                    // The hint lives HERE now rather than on the step row. It is also spoken aloud
                    // when the step opens, so the panel is the place you go to hear it again -
                    // which means it no longer has to be brief, and no longer has to disappear the
                    // moment you make any progress.
                    if (!string.IsNullOrEmpty(quest.Def.Hint))
                    {
                        GUI.contentColor = RunTheme.TextParchment;
                        GUILayout.Label("      " + quest.Def.Hint, RunTheme.Small);
                        GUI.contentColor = Color.white;
                    }

                    // And the reward, which the step rows deliberately do not show. Here it is a
                    // thing you went looking for rather than a thing that spoiled itself.
                    if (!string.IsNullOrEmpty(quest.Def.RewardText))
                    {
                        GUI.contentColor = RunTheme.TextMuted;
                        GUILayout.Label("      pays: " + quest.Def.RewardText, RunTheme.Small);
                        GUI.contentColor = Color.white;
                    }
                }

                int left = chain.Count - track.Index - (quest != null ? 1 : 0);
                if (left > 0)
                {
                    GUI.contentColor = RunTheme.TextMuted;
                    GUILayout.Label($"  {left} more on this track", RunTheme.Small);
                    GUI.contentColor = Color.white;
                }

                GUILayout.Space(6f);
            }

            DrawRecordSections(run);
        }

        /// <summary>
        /// The run's own account of itself: every main-quest step finished, in order, under the act
        /// it happened in.
        /// </summary>
        /// <remarks>
        /// This is the page's reason to exist. The tracks below it say what is outstanding, which the
        /// RUN page already says more briefly - what nothing said before is what the run HAS BEEN.
        /// Each entry carries the line spoken when that step opened, so read top to bottom it is the
        /// saga in its own words rather than a list of verbs (owner: "We should make sure that it
        /// tells a story of what we went through, so its sort of a book").
        ///
        /// It comes from RunService, not from the tracks, because the tracks cannot answer it: they
        /// are re-seated when an act flips, so by Act III there is nothing left in memory that
        /// remembers Act I. It is persisted for the same reason - a record that forgets itself on
        /// resume is not a record.
        /// </remarks>
        /// <summary>
        /// The book's title page: whose saga this is, and the one sentence the whole thing is about.
        /// </summary>
        /// <remarks>
        /// Two lines, and they are the reason the page now reads as a story at all (owner: "like
        /// we're building a story, and that should be apparent to the user"). A reader who opens the
        /// BOOK mid-run needs to be told, before any deed, that they are holding an account of
        /// something - and the character's own name on it is what turns a log into a saga.
        ///
        /// The name is read live rather than stored with the run: a chronicle that outlives one
        /// character is a thing this mode allows, and the name over it should be whoever is reading.
        /// </remarks>
        private void DrawTitlePage()
        {
            string name = null;
            try { name = Player.m_localPlayer?.GetPlayerName(); }
            catch { /* Never let a cosmetic line take the page down. */ }

            GUI.contentColor = RunTheme.AccentGoldBright;
            GUILayout.Label(string.IsNullOrEmpty(name)
                ? "THE SAGA"
                : $"THE SAGA OF {name.ToUpperInvariant()}", RunTheme.Header);
            GUI.contentColor = Color.white;

            GUI.contentColor = RunTheme.TextParchment;
            GUILayout.Label("Something is stealing the light from the world. This is the account of "
                          + "what was done about it.", RunTheme.Small);
            GUI.contentColor = Color.white;

            GUILayout.Space(8f);
        }

        /// <returns>The numeral of the last act it drew a chapter heading for, or null.</returns>
        private string DrawChronicle(IRunService run)
        {
            var entries = run.Chronicle;
            if (entries == null) return null;

            string act = null;
            bool any = false;

            foreach (var entry in entries)
            {
                // A chapter heading only where the act CHANGES, so the book reads as chapters
                // rather than as a table with a repeated column.
                if (entry.Act != act)
                {
                    // The chapter you are leaving gets its last line before the next one opens.
                    CloseChapter(run, act);

                    act = entry.Act;
                    GUILayout.Space(any ? 6f : 0f);
                    OpenChapter(run, act);
                }

                any = true;

                // The DEED is the marginal note and the LINE is the text. It used to be the other
                // way round - a bright step label with its line whispered underneath - and that is
                // what made the page read as a checklist with annotations rather than as a story
                // with a record beside it. Same two strings, opposite weights.
                GUI.contentColor = RunTheme.CompleteGreen;
                GUILayout.Label("  ✓ " + entry.Step, RunTheme.Small);
                GUI.contentColor = Color.white;

                if (!string.IsNullOrEmpty(entry.Line))
                {
                    GUI.contentColor = RunTheme.TextParchment;
                    GUILayout.Label("    " + entry.Line, RunTheme.Body);
                    GUI.contentColor = Color.white;
                    GUILayout.Space(3f);
                }
            }

            // The last act in the chronicle is closed too, when it is not the one being played.
            // CurrentAct is null outside a run, which closes the final chapter of a finished saga -
            // correct, and the only place that line is ever read.
            var current = run.CurrentAct;
            if (act != null && (current == null || current.Numeral != act)) CloseChapter(run, act);

            if (!any)
            {
                GUI.contentColor = RunTheme.TextMuted;
                GUILayout.Label("  Nothing written yet.", RunTheme.Small);
                GUI.contentColor = Color.white;
                return null;
            }

            GUILayout.Space(6f);
            return act;
        }

        /// <summary>
        /// A chapter heading - numeral, title, and the act's opening passage.
        /// </summary>
        /// <remarks>
        /// The title was missing entirely: the book said "ACT II" where the act card had said
        /// "Where the Light Goes", so the one page meant to hold the story was the one place its
        /// chapters had no names.
        /// </remarks>
        private void OpenChapter(IRunService run, string numeral)
        {
            var def = run.ActByNumeral(numeral);

            string heading =
                string.IsNullOrEmpty(numeral) ? "THE SAGA" :
                def == null || string.IsNullOrEmpty(def.Title) ? $"ACT {numeral}" :
                $"ACT {numeral} — {def.Title.ToUpperInvariant()}";

            GUILayout.Label(heading, RunTheme.Header);

            if (def != null && !string.IsNullOrEmpty(def.Chapter))
            {
                GUI.contentColor = RunTheme.TextParchment;
                GUILayout.Label(def.Chapter, RunTheme.Body);
                GUI.contentColor = Color.white;
                GUILayout.Space(5f);
            }
        }

        /// <summary>The act's closing line, if it has one and the act is behind the player.</summary>
        private void CloseChapter(IRunService run, string numeral)
        {
            if (string.IsNullOrEmpty(numeral)) return;

            var def = run.ActByNumeral(numeral);
            if (def == null || string.IsNullOrEmpty(def.ChapterClose)) return;

            GUILayout.Space(3f);
            GUI.contentColor = RunTheme.TextParchment;
            GUILayout.Label(def.ChapterClose, RunTheme.Body);
            GUI.contentColor = Color.white;
        }

        private void DrawQuestSection(IRunService run)
        {
            // No act line here: since alpha38 the act IS the HUD's headline, drawn above by
            // DrawHudBody. Repeating it would be the third time the same words appear on one panel.
            // Never silently on. A testing aid nobody can see is one somebody forgets is running,
            // and then reports its effects as bugs.
            if (run.DevMode)
            {
                // AlertSmall, not Small: contentColor multiplies, so tinting a muted style gave a
                // dark red times a 72%-alpha parchment and produced something unreadable. Small
                // in SIZE without being muted in colour is what this line actually wanted — it
                // has to be readable and it has to stop dominating the panel it sits above.
                //
                // The text comes from RunService.DevKeyHelp rather than living here, because the
                // copy that lived here went stale the moment two of the keys gained a modifier -
                // and a help line that is wrong is worse than none, since the tester trusts it and
                // concludes the feature is broken. See the remarks on that field.
                GUI.contentColor = RunTheme.HeatRed;
                foreach (var line in RunService.DevKeyHelp)
                    GUILayout.Label(line, RunTheme.AlertSmall);
                GUI.contentColor = Color.white;
            }

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

            // A blocked step is shown, not hidden: the player needs to know what is waiting and
            // why. Dimmed, because it is not something they can work on yet.
            GUI.contentColor = track.Blocked
                ? RunTheme.TextMuted
                : Color.Lerp(RunTheme.TextParchment, RunTheme.AccentGold, flash);
            // ONE label, not a horizontal group. Splitting it across fixed-width cells let IMGUI
            // squeeze the middle one until "Build a chest" rendered as "Build a" — a step name is
            // the single thing on this panel that must never be clipped.
            //
            // The progress bar this replaced was a third of the quest section's height carrying
            // information the "3/10" already gives.
            // A COMBINED step — an act's kill list — carries its count per clause below rather
            // than at the top: its own Progress/Target are unused filler (see
            // ChallengeDefinition.Subs), and "0/0" beside "Clear the mire" would be a lie.
            bool composite = quest.Def.Subs != null && quest.Def.Subs.Count > 0;

            string count = !track.Blocked && !composite && quest.Def.Target > 1f
                ? $"   {quest.Progress:0}/{quest.Def.Target:0}"
                : string.Empty;

            GUILayout.Label($"  {track.Label}   {quest.Def.Display}{count}", RunTheme.Body);
            GUI.contentColor = Color.white;

            // A deadline nobody can see is not a deadline, it is an ambush. A timed step therefore
            // carries its clock on its own row, and turns red for the last two minutes — the point
            // at which the answer changes from "go and find one" to "go and find one NOW".
            //
            // Hidden while the step is blocked, because a blocked step is not on its clock (see
            // ChallengeEngine.Tick) and a counting-down number beside it would say otherwise.
            if (!track.Blocked && quest.Def.TimeLimitSeconds > 0f)
            {
                float left = quest.TimeRemaining;
                bool urgent = left <= 120f;

                GUI.contentColor = urgent ? RunTheme.HeatRed : RunTheme.TextMuted;
                GUILayout.Label($"      {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00} left",
                                RunTheme.Small);
                GUI.contentColor = Color.white;
            }

            // A PROPER bar, on its own row. It has now been wrong twice in both directions: gone
            // entirely (alpha50.1, "the x/y seems lackluster"), then a 2px underline that "almost
            // can't be seen". Progress is the thing this panel exists to show, so it gets the
            // height it needs and the tighter spacing elsewhere pays for it.
            //
            // A combined step's bar reads CLAUSES DONE, so the one number on the row still means
            // "how far through this step am I" — the same thing it means everywhere else.
            if (composite)
            {
                int done = 0;
                for (int s = 0; s < quest.Def.Subs.Count; s++)
                {
                    float p = quest.SubProgress != null && s < quest.SubProgress.Count ? quest.SubProgress[s] : 0f;
                    if (p >= quest.Def.Subs[s].Target) done++;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(10f);
                var compositeRect = GUILayoutUtility.GetRect(160f, 10f, GUILayout.Width(160f), GUILayout.Height(10f));
                RunTheme.Bar(compositeRect, Mathf.Clamp01((float)done / quest.Def.Subs.Count),
                    quest.Done ? RunTheme.CompleteGreen : RunTheme.AccentGold);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (quest.Def.Target > 0f)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10f);
                var barRect = GUILayoutUtility.GetRect(160f, 10f, GUILayout.Width(160f), GUILayout.Height(10f));
                RunTheme.Bar(barRect, Mathf.Clamp01(quest.Progress / quest.Def.Target),
                    quest.Done ? RunTheme.CompleteGreen : RunTheme.AccentGold);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // The list itself, one line per quarry, all of them counting at once. This is the
            // whole point of the change: the player can see that the thing in front of them is on
            // the list before they kill it (owner: "I would like a combined kill quest list").
            // Drawn before the blocked early-return below, so a waiting step still shows what it
            // will ask for.
            if (composite && !track.Blocked)
            {
                for (int s = 0; s < quest.Def.Subs.Count; s++)
                {
                    var sub = quest.Def.Subs[s];
                    float p = quest.SubProgress != null && s < quest.SubProgress.Count ? quest.SubProgress[s] : 0f;
                    bool subDone = p >= sub.Target;

                    // The COUNT first, in its own fixed-width column, and brighter than the label.
                    //
                    // It used to trail the label - "  . Hunt 4 Boar   3/4" - which put the only
                    // number that changes at a ragged right edge, at the same size and the same
                    // muted colour as everything else on the panel. Two clauses of different name
                    // lengths and the figures no longer even lined up with each other (owner: "the
                    // x/y steps (e.g. 3/4 boar) is a bit difficult to see or not very apparent").
                    // A left-aligned column of counts can be read down, which is the whole job.
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(SubIndent);

                    GUI.contentColor = subDone ? RunTheme.CompleteGreen : RunTheme.AccentGoldBright;
                    GUILayout.Label(subDone ? "✓" : $"{p:0}/{sub.Target:0}",
                        RunTheme.Small, GUILayout.Width(SubCountColumn));

                    GUI.contentColor = subDone ? RunTheme.CompleteGreen : RunTheme.TextMuted;
                    GUILayout.Label(sub.Label, RunTheme.Small);

                    GUI.contentColor = Color.white;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }

            if (track.Blocked)
            {
                GUI.contentColor = RunTheme.AccentGold;
                GUILayout.Label("  " + (quest.Def.BlockedText ?? "Not yet."), RunTheme.Small);
                GUI.contentColor = Color.white;
                return;
            }

            // Act I's light race, on the step that IS the race. Both halves, because a race you
            // cannot see the other side of is not a race — and the number the forest has is the
            // number the Gatherer arrives with.
            if (quest.Def.Kind == ChallengeKind.PlayerEvent && quest.Def.Param == StolenLights.TakenEvent)
            {
                GUI.contentColor = RunTheme.TextMuted;
                GUILayout.Label($"     you {Service?.LightsTaken ?? 0}   \u2014   the forest {Service?.LightsLost ?? 0}",
                    RunTheme.Small);
                GUI.contentColor = Color.white;
            }


            // The hint used to be here, and is now on the QUESTS page. Three reasons, in order of
            // weight: it is SPOKEN when the step opens, so the panel copy was a second telling of
            // something the player has already been told; it was the largest variable-height thing
            // on a row that must stay compact; and it had been narrowed to "only before you have
            // made progress", which meant the one place to re-read it disappeared the moment you
            // started. On its own page it is always there and no longer has to be brief.

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
                    // Body, not Small: the panel's copy of the bearing was 10px at 72% alpha,
                    // which is the same "too dark to read" complaint one panel over. A direction
                    // is the most-read line in the window and gets full size and full brightness.
                    GUI.contentColor = RunTheme.AccentGoldBright;
                    GUILayout.Label("  " + bearing, RunTheme.Body);
                    GUI.contentColor = Color.white;
                }
            }

            // The reward is deliberately NOT shown under the step any more (owner, 2026-09-12:
            // "takes up too much space and ruins surprise"). It is still said once, when the step
            // completes — RunService's "Quest reward:" message — which is where a surprise belongs.
        }

        /// <summary>
        /// Splits and homestead records — what the run HAS been, drawn on the QUESTS page.
        /// </summary>
        /// <remarks>
        /// They were on the RUN page and are records rather than decisions: nobody acts on a split
        /// mid-fight, and both were spending height on the panel whose job is the three live tracks.
        /// That is also what the homestead's config flag was really working around - it was switched
        /// off by default "because the panel was competing for room with the three quest tracks",
        /// which is a room problem answered better by a page than by hiding the records. So it is
        /// shown here unconditionally, and <c>RunShowHomestead</c> now decides only whether it ALSO
        /// appears on the RUN page.
        /// </remarks>
        private void DrawRecordSections(IRunService run)
        {
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

            GUILayout.Space(4f);

            DrawHomestead(run.Records);
        }

        /// <summary>
        /// Splits measure the run against the clock; these measure it against itself. Nothing at all
        /// until something has happened, so a fresh run is not headed by six empty rows.
        /// </summary>
        private void DrawHomestead(HearthRecords records)
        {
            if (records == null || !records.Achieved.Any()) return;

            GUILayout.Label("HOMESTEAD", RunTheme.Header);

            foreach (var record in records.Achieved)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {record.Label}", RunTheme.Small, GUILayout.Width(110f));

                GUI.contentColor = record.IsPersonalBest ? RunTheme.AccentGold : RunTheme.TextParchment;
                GUILayout.Label(record.Text + (record.IsPersonalBest ? "  ★ best" : string.Empty), RunTheme.Small);
                GUI.contentColor = Color.white;

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
        }

        /// <summary>Tasks and held boons — the part of the RUN page that scrolls.</summary>
        private void DrawHudSections(IRunService run)
        {
            // Splits and the homestead records moved to the QUESTS page - see DrawRecordSections.
            // The homestead can still be asked for here, for anyone who wants it where it was.
            if (_config?.RunShowHomestead ?? false) DrawHomestead(run.Records);

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
                            : $"  {p:0}/{sub.Target:0}  {sub.Label}";

                        // Bright for the unfinished ones here too: this strip is the version read
                        // mid-fight, which is exactly when a muted fraction is no use.
                        GUI.contentColor = subDone ? RunTheme.CompleteGreen : RunTheme.AccentGoldBright;
                        GUILayout.Label(text, RunTheme.Small);
                        GUI.contentColor = Color.white;
                    }
                }

                // No standing "reroll costs N heat" line: it never changed, so it was furniture
                // (owner, 2026-09-12). The refusal when heat is short still names the cost.
            }

            GUILayout.Space(4f);

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
        /// <summary>Returns the height it used, so the bearing stack starts below it — both
        /// drew at strip.yMax + 2 and the direction text sat on top of the ability keys.</summary>
        private float DrawAbilityBar(IRunService run, Rect strip)
        {
            var boons = run.Boons;
            if (boons == null) return 0f;

            var actives = new List<HeldBoon>();
            foreach (var h in boons.Held)
            {
                if (!h.Def.IsPassive) actives.Add(h);
            }
            if (actives.Count == 0) return 0f;

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

            return slotH + 4f;
        }

        /// <summary>
        /// The activation key for a held boon, from the one table that also drives the input
        /// handler (<see cref="BoonKeys"/>). It was a second copy of that list until an active was
        /// added to one and not the other — a boon that worked and never said which key worked it.
        /// </summary>
        private static string ShortActivationKey(string boonId) => BoonKeys.Label(boonId);

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

            // The title row, OPENED AND CLOSED before anything else draws. It used to stay open
            // across the tab row and across the GM page, which put that whole page - blurb, button,
            // key table and scroll view - side by side in a single 360px-wide row, and returned from
            // the method with a layout group still on the stack. Reported as "the text doesn't fit
            // and there are no buttons", which is exactly what a vertical page laid out horizontally
            // looks like. One group at a time, balanced before the next begins.
            GUILayout.BeginHorizontal();
            GUILayout.Label("VALHEIM: THE SAGA", RunTheme.Header);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{ModVersion.VERSION}", RunTheme.Small);
            GUILayout.EndHorizontal();

            // Only a GM build has anywhere else to go, so a saga build never draws a tab row it
            // would be the only occupant of.
            if (ModVersion.GmEnabled)
            {
                GUILayout.BeginHorizontal();
                DrawLobbyTab("SAGA", LobbyPage.Saga);
                DrawLobbyTab("GM", LobbyPage.Gm);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);

                if (_lobbyPage == LobbyPage.Gm)
                {
                    DrawGmPage();
                    return;
                }
            }

            // What the saga IS, before any numbers. This lobby used to open with mechanical
            // facts (rates, par, a mode-gating note) and never said what the player was signing
            // up for — the one place with room to speak, spent on configuration.
            GUI.contentColor = RunTheme.AccentGold;
            GUILayout.Label("Something is stealing the light from the world.", RunTheme.Body);
            GUI.contentColor = Color.white;
            GUILayout.Label(
                "A story-driven campaign across every biome: build a home worth defending, " +
                "hunt what hunts you, and answer each of the old gods in turn. Quests on the " +
                "left, your stash on the right. Every task raises the Heat \u2014 and the world " +
                "rises with it.",
                RunTheme.Small);

            GUILayout.Space(4f);
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

            GUILayout.Space(5f);
            // Deferred: starting a run here would change the window set mid-pass.
            if (GUILayout.Button("Begin the saga")) _pendingStart = true;

            // A visible way out, and it is not decoration. This window opens ITSELF now, on entering
            // a world - and a window that appears unbidden and can only be dismissed by a key nobody
            // told you about is worse than the key press it replaced. Closing needs no deferral: it
            // changes nothing but this window's own visibility.
            if (GUILayout.Button("Not now")) _pendingLobbyClose = true;

            GUILayout.Space(4f);
            // Said in the saga's voice, not the mod's. "GM mode" is a developer's phrase for a
            // thing the player never sees during a run anyway — what they need to know is that
            // this mode plays fair.
            GUI.contentColor = RunTheme.TextMuted;
            GUILayout.Label("The saga is played without cheats. Power must be earned here.", RunTheme.Small);
            GUI.contentColor = Color.white;

            // No kill-hook warning here by design: KillHookAvailable is unconditionally true
            // outside a run (the grace clock only advances while one is in progress, and the
            // injected hook cannot prove itself until something dies), so a lobby line would
            // either never appear or cry wolf. The real surface is RunService's in-run notice,
            // raised on the strip once the 60s grace window closes with the hook still silent.
        }

        private void DrawLobbyTab(string label, LobbyPage page)
        {
            bool active = _lobbyPage == page;

            GUI.contentColor = active ? RunTheme.AccentGoldBright : RunTheme.TextMuted;
            if (GUILayout.Button(active ? "\u2022 " + label : label, GUILayout.Width(84f)) && !active)
                _lobbyPage = page;
            GUI.contentColor = Color.white;
        }

        /// <summary>
        /// The GM mod's door, and its key table.
        /// </summary>
        /// <remarks>
        /// The table is generated from <see cref="CommandRegistry.All"/>, which is the list the
        /// input manager was registered FROM - so a binding that exists is listed and a binding
        /// that was skipped is not. That is the same medicine as BoonKeys and DevKeyHelp, and it is
        /// applied here for the third time because this codebase has now twice shipped a command
        /// nobody could discover and once shipped a help line that had gone stale.
        ///
        /// It is also the first time the GM mod's keys have been written down anywhere inside the
        /// game.
        ///
        /// The button toggles the EXISTING cheat windows rather than re-implementing anything: the
        /// whole point of a general menu is a second door onto what is already there.
        /// </remarks>
        private void DrawGmPage()
        {
            GUI.contentColor = RunTheme.TextMuted;
            GUILayout.Label("The old sandbox. Nothing here is scored, and none of it is reachable " +
                            "once a saga is running.", RunTheme.Small);
            GUI.contentColor = Color.white;

            GUILayout.Space(4f);

            bool shown = CheatUiVisible;
            if (GUILayout.Button(shown ? "Hide the GM windows  [F1]" : "Open the GM windows  [F1]"))
                _pendingGmToggle = true;

            GUILayout.Space(4f);
            GUILayout.Label("KEYS", RunTheme.Header);

            var all = CommandRegistry.All;
            if (all == null || all.Count == 0)
            {
                GUILayout.Label("  none registered", RunTheme.Small);
                return;
            }

            _gmScroll = GUILayout.BeginScrollView(_gmScroll, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
            try
            {
                foreach (var cmd in all)
                {
                    if (cmd == null) continue;

                    GUILayout.BeginHorizontal();

                    GUI.contentColor = RunTheme.AccentGoldBright;
                    GUILayout.Label(KeyLabel(cmd.Key), RunTheme.Small, GUILayout.Width(92f));

                    // No FlexibleSpace after this: the style wraps, and a flexible space in the
                    // same row competes with a wrapping label for the width, which squeezes the
                    // description into a column two words wide.
                    GUI.contentColor = RunTheme.TextMuted;
                    GUILayout.Label(cmd.Description ?? "", RunTheme.Small);

                    GUI.contentColor = Color.white;
                    GUILayout.EndHorizontal();
                }
            }
            finally { GUILayout.EndScrollView(); }
        }

        /// <summary>A KeyCode as a player would say it. "Keypad7" is not a key anybody names.</summary>
        private static string KeyLabel(KeyCode key)
        {
            string name = key.ToString();

            if (name.StartsWith("Keypad", StringComparison.Ordinal))
                return "Num " + name.Substring("Keypad".Length);

            if (name.StartsWith("Alpha", StringComparison.Ordinal))
                return name.Substring("Alpha".Length);

            return name;
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

                // An active says its key on the card, not just once it is held: the decision the
                // offer asks for includes "have I got a finger free for this".
                string offerKey = BoonKeys.Label(offer[i].Id);
                GUILayout.Label(
                    offer[i].IsPassive ? "passive"
                        : string.IsNullOrEmpty(offerKey) ? "active" : "active  " + offerKey,
                    RunTheme.Small);
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
