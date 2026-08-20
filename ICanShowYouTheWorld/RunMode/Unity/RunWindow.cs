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

        private const float HudWidth = 340f;
        // Grown by the pinned QUEST section: it sits outside the scroll view, so without the extra
        // height it would simply eat the same number of pixels from the scrolling body.
        private const float HudHeight = 480f;
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
        private string _lastMainQuestId;
        private bool _mainQuestSeen;
        private float _questFlashAt = float.NegativeInfinity;

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
            if (!_pendingStart && !_pendingAbandon && !_pendingDiscard) return;
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            bool start = _pendingStart;
            bool abandon = _pendingAbandon;
            bool discard = _pendingDiscard;
            _pendingStart = false;
            _pendingAbandon = false;
            _pendingDiscard = false;

            try
            {
                var run = Service;
                if (run == null) return;

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

                // Nothing Run Mode draws belongs on the main menu — not the strip, not the HUD,
                // not the offer, and not the lobby (whose Start/Discard buttons act on a world
                // that isn't there). Suspending an active run already clears the in-world case;
                // this also covers the parked-run notice and any menu frame before that lands.
                if (Player.m_localPlayer == null) return;

                EnsureStyles();
                Layout(viewWidth, viewHeight);

                if (run.IsRunActive)
                {
                    UpdateHeatPulse(run.Heat);
                    UpdateCompletionFlashes(run.Challenges);
                    UpdateQuestFlash(run.Challenges);
                    UpdateOfferFadeState(run.Boons?.CurrentOffer?.Count ?? 0);

                    // The strip is the one piece that survives with the rest of the UI hidden.
                    DrawStrip(run, viewWidth);

                    if (Visible || CheatUiVisible)
                    {
                        _hudRect = GUILayout.Window(HudWindowId, _hudRect, DrawHud, GUIContent.none, RunTheme.Panel,
                            GUILayout.Width(HudWidth), GUILayout.Height(HudHeight));
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

            _hudRect = new Rect(viewWidth - HudWidth - 10f, 40f, HudWidth, HudHeight);
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

            if (font != null)
            {
                _stripStyle.font = font;
                _noticeStyle.font = font;
                _timerStyle.font = font;
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

        private void UpdateQuestFlash(ChallengeEngine challenges)
        {
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            string id = challenges?.CurrentMainQuest?.Def?.Id;
            if (_mainQuestSeen && id != _lastMainQuestId) _questFlashAt = Time.realtimeSinceStartup;

            _mainQuestSeen = true;
            _lastMainQuestId = id;
        }

        private void UpdateOfferFadeState(int offerCount)
        {
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            if (offerCount > 0 && _lastOfferCount <= 0) _offerShownAt = Time.realtimeSinceStartup;
            else if (offerCount <= 0) _offerShownAt = float.NegativeInfinity;
            _lastOfferCount = offerCount;
        }

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

            string notice = _concrete?.HudNotice;
            if (!string.IsNullOrEmpty(notice))
            {
                GUI.Label(new Rect(rect.x - 100f, rect.yMax + 2f, StripWidth + 200f, 20f), notice, _noticeStyle);
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

            // --- Header: stays out of the scroll view, so time/heat/score are always on screen. ---
            GUILayout.Label("RUN", RunTheme.Header);
            GUILayout.Label(FormatTime(run.ElapsedSeconds), _timerStyle);

            GUILayout.BeginHorizontal();
            GUI.contentColor = HeatDisplayColor();
            GUILayout.Label($"Heat {run.Heat:0.#}", _stripStyle);
            GUI.contentColor = Color.white;
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Score {run.CurrentScore:0.##}", RunTheme.Header);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // --- Main questline: pinned above the scroll, like the timer. It is the one thing on
            //     this HUD that says where the run is GOING, so it must never scroll out of view
            //     behind a long splits list. ---
            DrawQuestSection(run);

            GUILayout.Space(6f);

            // The window height is fixed, so the body — which grows with splits, challenges and
            // held boons — scrolls. Without this it would overflow and clip the Abandon button
            // out of reach, and with the input gate on, that button is the only way out of a run.
            _hudScroll = GUILayout.BeginScrollView(_hudScroll, GUILayout.ExpandHeight(true));
            // finally, not a plain call: a throw inside the body must still close the group,
            // or every window drawn after this one inherits a broken layout stack.
            try { DrawHudSections(run); }
            finally { GUILayout.EndScrollView(); }

            // --- Abandon, behind a two-press confirm so a stray click can't end a run. Outside
            //     the scroll view, so it is always reachable however long the body gets. ---
            bool armed = Time.realtimeSinceStartup - _lastAbandonPress <= AbandonConfirmSeconds;
            GUI.contentColor = armed ? RunTheme.HeatRed : Color.white;
            if (GUILayout.Button(armed ? "Abandon run — click again" : "Abandon run"))
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
        /// The main questline: the one step in play, its progress, and what finishing it hands
        /// over. Random tasks pay in heat and boons; the questline pays in ITEMS, which is why the
        /// reward line is spelled out here rather than left as a surprise — it is the thing the
        /// player is meant to be steering towards.
        ///
        /// Deliberately narrow: exactly one step is ever shown (the engine only keeps one), and
        /// nothing here is interactive — a questline step cannot be rerolled.
        /// </summary>
        private void DrawQuestSection(IRunService run)
        {
            GUILayout.Label("QUEST", RunTheme.Header);

            // Same gold flash a finished task row gets, decaying back to the steady color.
            float flash = 1f - Mathf.Clamp01((Time.realtimeSinceStartup - _questFlashAt) / CompletionFlashSeconds);

            var quest = run.Challenges?.CurrentMainQuest;
            if (quest == null)
            {
                // Chain exhausted (or none installed, on a run started before the questline
                // existed) — the Meadows arc is over and the rest of the run is the bosses.
                GUI.contentColor = Color.Lerp(RunTheme.CompleteGreen, RunTheme.AccentGold, flash);
                GUILayout.Label("  Act I complete", RunTheme.Body);
                GUI.contentColor = Color.white;
                return;
            }

            GUI.contentColor = Color.Lerp(RunTheme.TextParchment, RunTheme.AccentGold, flash);
            GUILayout.Label("  " + quest.Def.Display, RunTheme.Body);
            GUI.contentColor = Color.white;

            GUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            var barRect = GUILayoutUtility.GetRect(140f, 12f, GUILayout.Width(140f), GUILayout.Height(12f));
            float frac = quest.Def.Target > 0f ? quest.Progress / quest.Def.Target : 0f;
            RunTheme.Bar(barRect, frac, quest.Done ? RunTheme.CompleteGreen : RunTheme.AccentGold);
            GUILayout.Label($"{quest.Progress:0}/{quest.Def.Target:0}", RunTheme.Small, GUILayout.Width(46f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

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
                    GUILayout.Label(a.Def.Display, RunTheme.Small, GUILayout.Width(150f));
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
                    string label = free ? "reroll (free)" : "reroll";
                    float width = free ? 90f : 55f;

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
            var boons = run.Boons;
            if (boons == null || boons.Held.Count == 0)
            {
                GUILayout.Label("  none held", RunTheme.Small);
            }
            else
            {
                foreach (var h in boons.Held)
                {
                    bool ready = !h.Def.IsPassive && h.CooldownRemaining <= 0f &&
                        (h.Def.CooldownSeconds > 0f || h.Charges > 0);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  " + h.Def.Display, RunTheme.Small);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(BoonStatus(h), ready ? RunTheme.Ready : RunTheme.Small);
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
                default: return "";
            }
        }

        private static string BoonStatus(HeldBoon h)
        {
            if (h.Def.IsPassive) return "passive";

            string key = ActivationKey(h.Def.Id);
            string state = h.CooldownRemaining > 0f
                ? $"{h.CooldownRemaining:0}s"
                : h.Def.CooldownSeconds <= 0f ? $"x{h.Charges}" : "ready";

            return key == null ? state : $"{state}  [{key}]";
        }

        /// <summary>Mirrors RunService.HandleBoonActivationInput — Keypad4/5/6/7.</summary>
        private static string ActivationKey(string boonId)
        {
            switch (boonId)
            {
                case "wind": return "Keypad 4";
                case "ember": return "Keypad 5";
                case "way": return "Keypad 6";
                case "brother": return "Keypad 7";
                default: return null;
            }
        }

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

            GUILayout.Label("RUN MODE", RunTheme.Header);
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
            if (GUILayout.Button("Start Run")) _pendingStart = true;

            GUILayout.Space(6f);
            GUILayout.Label("GM mode is disabled while a run is live.", RunTheme.Small);

            // No kill-hook warning here by design: KillHookAvailable is unconditionally true
            // outside a run (the grace clock only advances while one is in progress, and the
            // injected hook cannot prove itself until something dies), so a lobby line would
            // either never appear or cry wolf. The real surface is RunService's in-run notice,
            // raised on the strip once the 60s grace window closes with the hook still silent.
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
