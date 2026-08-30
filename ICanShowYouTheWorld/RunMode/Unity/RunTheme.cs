using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Self-contained visual theme for Run Mode's IMGUI (see <see cref="RunWindow"/>): a small
    /// parchment-and-gold palette, textures generated at runtime (nothing to ship as an asset),
    /// and a handful of GUIStyle factories built once and reused.
    ///
    /// Everything here is static and lazily initialised on first use — safe to touch from OnGUI,
    /// never touched outside it. No public member allocates a new Texture2D or GUIStyle on
    /// repeat calls; textures and styles are cached after their first build.
    /// </summary>
    public static class RunTheme
    {
        // --- Palette ---

        public static readonly Color TextParchment = ParseColor("E8DFC8");
        /// <summary>
        /// Secondary text. 0.86 rather than the 0.72 it was: GUI.contentColor MULTIPLIES with a
        /// style's own colour, so every "tint a Small label" in this window was being dimmed
        /// twice — once by the tint and once by this alpha — and the result was a colour nobody
        /// chose. See <see cref="HeatRed"/>, which is where it showed worst.
        /// </summary>
        public static readonly Color TextMuted = ParseColor("E8DFC8", 0.86f);
        public static readonly Color PanelFill = ParseColor("12100E", 0.80f);
        public static readonly Color PanelBorder = ParseColor("6B5F44", 0.95f);
        public static readonly Color AccentGold = ParseColor("C9A227");

        /// <summary>
        /// The gold for text drawn onto the WORLD rather than onto a panel.
        ///
        /// AccentGold was picked against the dark parchment panel, where it reads perfectly. The
        /// strip's bearing line has nothing behind it at all — it sits over daylit meadows, snow,
        /// and a lit fire — and a mid gold on a bright background is not a colour choice, it is an
        /// invisible line (owner: "The font we use to indicate distance to biomes, etc is too
        /// dark"). Brighter, and always drawn through <see cref="ShadowedLabel"/>, which is the
        /// half that actually does the work: no single colour survives every backdrop, an outline
        /// does.
        /// </summary>
        public static readonly Color AccentGoldBright = ParseColor("F7D65D");

        /// <summary>Backing for over-world text. Near-black rather than black, to read as depth.</summary>
        private static readonly Color TextShadow = ParseColor("000000", 0.85f);
        /// <summary>
        /// The warning red, and it is BRIGHT on purpose.
        ///
        /// B0433A is a handsome red on paper and unreadable here (owner: "the contrast of the
        /// text in the Run menu is off, dark red doesn't work"). Two things were stacking against
        /// it: the panel behind it is near-black, and every use tinted a muted style, so
        /// contentColor multiplied a dark red by a 72%-alpha parchment and produced mud. This
        /// survives the multiply.
        /// </summary>
        public static readonly Color HeatRed = ParseColor("E8564A");
        public static readonly Color CompleteGreen = ParseColor("5C9A56");
        public static readonly Color TrackFill = ParseColor("000000", 0.45f);
        public static readonly Color CooldownOverlay = ParseColor("000000", 0.55f);

        private static Color ParseColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            return Color.magenta; // loud on purpose: a bad literal should be obvious, not silent
        }

        // --- Runtime textures (built once, cached; never per-OnGUI-call) ---

        private static readonly Dictionary<Color, Texture2D> SolidCache = new Dictionary<Color, Texture2D>();

        /// <summary>A 1x1 texture of the given color, cached by value. Safe to call every frame.</summary>
        public static Texture2D Solid(Color color)
        {
            if (SolidCache.TryGetValue(color, out var tex) && tex != null) return tex;

            tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            SolidCache[color] = tex;
            return tex;
        }

        private const int PanelTexSize = 16;
        private const int PanelBorderPx = 2;
        private static Texture2D _panelTex;

        /// <summary>
        /// A dark translucent fill with a 1-2px lighter border, 9-slice-able via
        /// <see cref="PanelBorderOffset"/>. Built once and cached.
        /// </summary>
        public static Texture2D PanelTexture
        {
            get
            {
                if (_panelTex != null) return _panelTex;

                var tex = new Texture2D(PanelTexSize, PanelTexSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var pixels = new Color[PanelTexSize * PanelTexSize];
                for (int y = 0; y < PanelTexSize; y++)
                {
                    for (int x = 0; x < PanelTexSize; x++)
                    {
                        bool border = x < PanelBorderPx || y < PanelBorderPx ||
                                      x >= PanelTexSize - PanelBorderPx || y >= PanelTexSize - PanelBorderPx;
                        pixels[y * PanelTexSize + x] = border ? PanelBorder : PanelFill;
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();
                _panelTex = tex;
                return _panelTex;
            }
        }

        public static RectOffset PanelBorderOffset => new RectOffset(PanelBorderPx, PanelBorderPx, PanelBorderPx, PanelBorderPx);

        // --- Font ---

        private static Font _font;
        private static bool _fontAttempted;

        /// <summary>
        /// A serif OS font for the HUD, or null if none of the candidates were available — every
        /// style falls back to the default skin font in that case. Never throws.
        /// </summary>
        public static Font ThemedFont
        {
            get
            {
                if (_fontAttempted) return _font;
                _fontAttempted = true;
                try
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Georgia", "Palatino", "Palatino Linotype", "Times New Roman" }, 16);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ICanShowYouTheWorld] RunTheme: OS font creation failed, using default: " + ex.Message);
                    _font = null;
                }
                return _font;
            }
        }

        // --- Styles (built once; see EnsureStyles) ---

        private static bool _stylesReady;
        private static GUIStyle _panelStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _smallStyle;
        private static GUIStyle _readyStyle;
        private static GUIStyle _alertStyle;

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            var font = ThemedFont;

            _panelStyle = new GUIStyle(GUI.skin.window)
            {
                border = PanelBorderOffset,
                padding = new RectOffset(10, 10, 10, 8),
                normal = { background = PanelTexture, textColor = TextParchment },
                onNormal = { background = PanelTexture, textColor = TextParchment },
                focused = { background = PanelTexture, textColor = TextParchment },
                onFocused = { background = PanelTexture, textColor = TextParchment },
            };
            if (font != null) _panelStyle.font = font;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = AccentGold },
            };
            if (font != null) _headerStyle.font = font;

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = TextParchment },
                wordWrap = true,
            };
            if (font != null) _bodyStyle.font = font;

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = TextMuted },
                wordWrap = true,
            };
            if (font != null) _smallStyle.font = font;

            _alertStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                normal = { textColor = Color.white },
                wordWrap = true,
            };
            if (font != null) _alertStyle.font = font;

            _readyStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = AccentGold },
            };
            if (font != null) _readyStyle.font = font;
        }

        /// <summary>Window/panel chrome: the dark translucent panel texture as background.</summary>
        public static GUIStyle Panel { get { EnsureStyles(); return _panelStyle; } }

        /// <summary>Section/window headers: bold, gold.</summary>
        public static GUIStyle Header { get { EnsureStyles(); return _headerStyle; } }

        /// <summary>Body text: parchment.</summary>
        public static GUIStyle Body { get { EnsureStyles(); return _bodyStyle; } }

        /// <summary>Small/secondary text: muted parchment.</summary>
        public static GUIStyle Small { get { EnsureStyles(); return _smallStyle; } }

        /// <summary>Emphasis for a "ready"/actionable state: bold gold.</summary>
        public static GUIStyle Ready { get { EnsureStyles(); return _readyStyle; } }

        /// <summary>
        /// For a line whose colour is set by the CALLER through GUI.contentColor.
        ///
        /// Its own colour is white, which is the whole point: contentColor multiplies, so tinting
        /// a style that already carries a muted parchment gives you neither colour. Anything that
        /// wants to be exactly HeatRed, or exactly gold, asks for this and gets it.
        /// </summary>
        public static GUIStyle Alert { get { EnsureStyles(); return _alertStyle; } }

        // --- Drawing helpers ---

        private static void DrawBorder(Rect r, Color color, float thickness)
        {
            var tex = Solid(color);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), tex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), tex);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), tex);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), tex);
        }

        /// <summary>A thin 1px (by default) rectangular outline in the given color. Used for
        /// panel-less accents like ability slots, where the fill comes from a caller-chosen
        /// readiness tint rather than the standard panel texture.</summary>
        public static void Frame(Rect r, Color color, float thickness = 1f) => DrawBorder(r, color, thickness);

        /// <summary>
        /// A filled progress bar: background track, clipped fill, 1px border. Used for challenge
        /// progress. Pure drawing — no allocation beyond the cached 1x1 textures it reuses.
        /// </summary>
        /// <summary>
        /// A label with a one-pixel outline, for text drawn over the world.
        ///
        /// IMGUI has no outline, so this is the standard trick: the string four times in shadow,
        /// offset by a pixel on each axis, then once on top in its own colour. Five draws of a
        /// short line, once a frame, is nothing — and it is the difference between a bearing you
        /// can read against snow and one you cannot.
        ///
        /// GUI.contentColor is saved and restored rather than assumed white: every caller in this
        /// file tints, and a helper that quietly reset the tint would break the next thing drawn.
        /// </summary>
        public static void ShadowedLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text) || style == null) return;

            Color previous = GUI.contentColor;

            GUI.contentColor = TextShadow;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                for (int dy = -1; dy <= 1; dy += 2)
                    GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), text, style);
            }

            GUI.contentColor = color;
            GUI.Label(rect, text, style);

            GUI.contentColor = previous;
        }

        public static void Bar(Rect r, float fill01, Color fillColor)
        {
            fill01 = Mathf.Clamp01(fill01);

            GUI.DrawTexture(r, Solid(TrackFill));
            if (fill01 > 0f)
            {
                var fillRect = new Rect(r.x, r.y, r.width * fill01, r.height);
                GUI.DrawTexture(fillRect, Solid(fillColor));
            }
            DrawBorder(r, PanelBorder, 1f);
        }

        /// <summary>
        /// Cooldown indicator for an ability slot: darkens the slot proportionally to the
        /// fraction of cooldown remaining (1 = just used, 0 = ready). A true radial isn't worth
        /// it in IMGUI, so this is a left-to-right wipe that shrinks as the ability comes off
        /// cooldown.
        /// </summary>
        public static void Radialish(Rect r, float remaining01)
        {
            remaining01 = Mathf.Clamp01(remaining01);
            if (remaining01 <= 0f) return;

            var overlay = new Rect(r.x, r.y, r.width * remaining01, r.height);
            GUI.DrawTexture(overlay, Solid(CooldownOverlay));
        }
    }
}
