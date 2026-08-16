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
    /// </summary>
    public class RunWindow
    {
        private const int HudWindowId = 10;
        private const int LobbyWindowId = 11;
        private const int OfferWindowId = 12;

        private const float HudWidth = 340f;
        private const float HudHeight = 420f;
        private const float LobbyWidth = 360f;
        private const float LobbyHeight = 240f;
        private const float OfferWidth = 420f;
        private const float OfferHeight = 160f;
        private const float StripWidth = 300f;
        private const float StripHeight = 24f;

        /// <summary>Seconds within which a second [Abandon run] press counts as confirmation.</summary>
        private const float AbandonConfirmSeconds = 2f;

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
        private Vector2 _hudScroll;

        // Deferred lifecycle actions. A button that flips IsRunActive mid-pass would change the
        // window set between GUILayout's Layout and Repaint passes, which IMGUI answers with a
        // stream of "Mismatched LayoutGroup" errors. The buttons only raise these flags; they
        // are consumed at a Layout event (see ApplyPendingActions), so the window set only ever
        // changes between passes.
        private bool _pendingStart;
        private bool _pendingAbandon;

        // Failure sites already logged, keyed by site + message: a new fault still gets a line,
        // a repeating one doesn't flood OnGUI. Capped so a fault with a varying message
        // (positions, timings) can't grow the set without bound.
        private const int MaxLoggedFailures = 32;
        private readonly HashSet<string> _loggedFailures = new HashSet<string>();

        // Styles are built once and reused; a new GUIStyle per OnGUI call would churn every frame.
        private GUIStyle _stripStyle;
        private GUIStyle _noticeStyle;
        private GUIStyle _timerStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _smallStyle;

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
            if (!_pendingStart && !_pendingAbandon) return;
            if (Event.current == null || Event.current.type != EventType.Layout) return;

            bool start = _pendingStart;
            bool abandon = _pendingAbandon;
            _pendingStart = false;
            _pendingAbandon = false;

            try
            {
                var run = Service;
                if (run == null) return;

                // Abandon wins if both somehow queued: it is the safer of the two to honour.
                if (abandon) run.AbandonRun();
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
                Layout(viewWidth, viewHeight);

                if (run.IsRunActive)
                {
                    // The strip is the one piece that survives with the rest of the UI hidden.
                    DrawStrip(run, viewWidth);

                    if (Visible || CheatUiVisible)
                    {
                        _hudRect = GUILayout.Window(HudWindowId, _hudRect, DrawHud, "Run",
                            GUILayout.Width(HudWidth), GUILayout.Height(HudHeight));
                    }

                    var boons = run.Boons;
                    if (boons != null && boons.CurrentOffer.Count > 0)
                    {
                        _offerRect = GUILayout.Window(OfferWindowId, _offerRect, DrawOffer, "Boon offer",
                            GUILayout.Width(OfferWidth), GUILayout.Height(OfferHeight));
                    }
                }
                else if (Visible)
                {
                    _lobbyRect = GUILayout.Window(LobbyWindowId, _lobbyRect, DrawLobby, "Run Mode",
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

            _stripStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            _noticeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = Color.yellow }
            };
            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 28,
                normal = { textColor = Color.white }
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
        }

        // --- Strip (always on during a run, F1 or no F1) ---

        private void DrawStrip(IRunService run, float viewWidth)
        {
            var rect = new Rect((viewWidth - StripWidth) * 0.5f, 6f, StripWidth, StripHeight);

            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Box(rect, GUIContent.none);
            GUI.backgroundColor = Color.white;

            GUI.Label(rect, $"{FormatTime(run.ElapsedSeconds)}   Heat {run.Heat:0.#}", _stripStyle);

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

            Backdrop(_hudRect);

            // --- Header: stays out of the scroll view, so time/heat/score are always on screen. ---
            GUILayout.Label(FormatTime(run.ElapsedSeconds), _timerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Heat {run.Heat:0.#}", _headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Score {run.CurrentScore:0.##}", _headerStyle);
            GUILayout.EndHorizontal();

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
            GUI.contentColor = armed ? Color.red : Color.white;
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

        /// <summary>Splits, challenges and held boons — the part of the HUD that scrolls.</summary>
        private void DrawHudSections(IRunService run)
        {
            // --- Splits ---
            GUILayout.Label("SPLITS", _headerStyle);
            var splits = run.Splits;
            if (splits == null || splits.Count == 0)
            {
                GUILayout.Label("  no bosses down yet", _smallStyle);
            }
            else
            {
                foreach (var split in splits) GUILayout.Label("  " + split, _smallStyle);
            }

            GUILayout.Space(6f);

            // --- Challenges ---
            GUILayout.Label("CHALLENGES", _headerStyle);
            var challenges = run.Challenges;
            if (challenges == null || challenges.Active.Count == 0)
            {
                GUILayout.Label("  none active", _smallStyle);
            }
            else
            {
                // Indexed, not foreach: the reroll button needs the slot index the engine uses.
                for (int i = 0; i < challenges.Active.Count; i++)
                {
                    var a = challenges.Active[i];

                    GUILayout.BeginHorizontal();
                    GUI.contentColor = a.Done ? Color.green : Color.white;
                    GUILayout.Label($"{a.Def.Display}  {a.Progress:0}/{a.Def.Target:0}", _smallStyle);
                    GUI.contentColor = Color.white;
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("reroll", GUILayout.Width(55f))) run.RerollChallenge(i);
                    GUILayout.EndHorizontal();
                }

                float cost = Config?.RunRerollHeatCost ?? 0f;
                if (cost > 0f) GUILayout.Label($"  reroll costs {cost:0.#} heat", _smallStyle);
            }

            GUILayout.Space(6f);

            // --- Boons ---
            GUILayout.Label("BOONS", _headerStyle);
            var boons = run.Boons;
            if (boons == null || boons.Held.Count == 0)
            {
                GUILayout.Label("  none held", _smallStyle);
            }
            else
            {
                foreach (var h in boons.Held)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("  " + h.Def.Display, _smallStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(BoonStatus(h), _smallStyle);
                    GUILayout.EndHorizontal();
                }
            }

        }

        /// <summary>Cooldown/charges plus the activation key for the three active boons.</summary>
        private static string BoonStatus(HeldBoon h)
        {
            if (h.Def.IsPassive) return "passive";

            string key = ActivationKey(h.Def.Id);
            string state = h.CooldownRemaining > 0f
                ? $"{h.CooldownRemaining:0}s"
                : h.Def.CooldownSeconds <= 0f ? $"x{h.Charges}" : "ready";

            return key == null ? state : $"{state}  [{key}]";
        }

        /// <summary>Mirrors RunService.HandleBoonActivationInput — Keypad4/5/6.</summary>
        private static string ActivationKey(string boonId)
        {
            switch (boonId)
            {
                case "wind": return "Keypad 4";
                case "ember": return "Keypad 5";
                case "way": return "Keypad 6";
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

            Backdrop(_lobbyRect);

            GUILayout.Label("Run Mode", _headerStyle);
            GUILayout.Label(run.LobbySummary(), _smallStyle);

            var cfg = Config;
            if (cfg != null)
            {
                GUILayout.Label(
                    $"Resources x{cfg.RunResourceRate:0.##}   Skills x{cfg.RunSkillGainRate:0.##}   " +
                    $"Par {cfg.RunParTimeMinutes:0} min",
                    _smallStyle);
            }

            if (run.CurrentScore > 0f)
            {
                GUILayout.Label($"Last run score: {run.CurrentScore:0.##}", _smallStyle);
            }

            GUILayout.Space(8f);
            // Deferred: starting a run here would change the window set mid-pass.
            if (GUILayout.Button("Start Run")) _pendingStart = true;

            GUILayout.Space(6f);
            GUILayout.Label("GM mode is disabled while a run is live.", _smallStyle);

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

            Backdrop(_offerRect);

            var offer = boons.CurrentOffer;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < offer.Count; i++)
            {
                GUILayout.BeginVertical(GUILayout.Width(OfferWidth / 3f - 12f));
                GUI.contentColor = Color.cyan;
                GUILayout.Label($"{i + 1}. {offer[i].Display}", _headerStyle);
                GUI.contentColor = Color.white;
                GUILayout.Label(offer[i].IsPassive ? "passive" : "active", _smallStyle);
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.Label("press Keypad 1/2/3", _smallStyle);
        }

        // --- Helpers ---

        private static void Backdrop(Rect windowRect)
        {
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Box(new Rect(0f, 0f, windowRect.width, windowRect.height), GUIContent.none);
            GUI.backgroundColor = Color.white;
        }

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
    }
}
