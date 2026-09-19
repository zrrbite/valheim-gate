using System;
using System.Reflection;
using UnityEngine;

namespace ICanShowYouTheWorld
{
    /// <summary>
    /// "SAGA" and the build version, appended to the game's own version line on the main menu.
    ///
    /// The mod loads itself at startup now, which is the point — and it also means there is
    /// nothing to DO that proves it worked. This is the standing answer to "is it loaded?", in
    /// the one place on the menu where a version already lives.
    ///
    /// Since 2026-09-19 it is the ONLY answer: the activation popup was removed on the owner's
    /// call ("I guess we dont need to show the popup except if something fails"), because a
    /// dialog to dismiss on every launch is a toll for something that worked. That makes this
    /// line load-bearing rather than decorative.
    ///
    /// Not an IMGUI overlay, deliberately: the game's label is already placed, already styled,
    /// already scaled to the resolution, and already where a player looks for a version. An
    /// overlay would have to guess all four and would guess differently on every display.
    ///
    /// Reflection, because the label is a TMPro.TMP_Text and the mod does not reference
    /// TextMeshPro. One field and one property is a smaller price than another game assembly in
    /// libraries/ to refresh on every update.
    /// </summary>
    internal static class MenuBadge
    {
        // The saga's bright gold (RunTheme.AccentGoldBright). Bright rather than the mid gold
        // because this sits on the menu's artwork, not on the dark parchment the mid gold was
        // picked against.
        private const string Gold = "F7D65D";
        /// <summary>
        /// Short on purpose. The first version of this appended "VALHEIM: THE SAGA  v1.0.15-run..."
        /// at full size, which is LONGER than the game's own "Version 1.0.15 (n-40)" — so TMP
        /// wrapped it onto a third line and the block overflowed its rect and drew on top of
        /// itself (owner, with a screenshot: "the mod-text in the bottom right is on top of each
        /// other").
        ///
        /// The fix is not a bigger rect, which is not ours to resize: it is one line that cannot
        /// wrap. "SAGA" plus the build, at 70%, is comfortably narrower than the line above it.
        /// </summary>
        private const string Title = "SAGA";

        private static object _label;          // the TMP_Text component, per FejdStartup
        private static PropertyInfo _textProp;
        private static FejdStartup _seenStartup;
        private static bool _complained;

        /// <summary>
        /// Every frame from CheatController.Update. Two null checks and a string test while the
        /// menu is up; a single early return once the game scene has replaced it.
        /// </summary>
        public static void Tick()
        {
            try
            {
                // FejdStartup exists only in the start scene, so this is also the test for
                // "are we at the menu". A destroyed instance compares equal to null, which is
                // exactly the reading wanted here.
                var fejd = FejdStartup.instance;
                if (fejd == null)
                {
                    _label = null;
                    _seenStartup = null;
                    return;
                }

                // A new FejdStartup means a reloaded start scene — returning to the menu from a
                // world — and a fresh label object to find. ReferenceEquals, because the old one
                // is destroyed and would compare equal to the new one's null test.
                if (!ReferenceEquals(fejd, _seenStartup))
                {
                    _seenStartup = fejd;
                    _label = null;
                }

                if (_label == null && !Resolve(fejd)) return;

                string current = _textProp.GetValue(_label, null) as string;
                if (current == null) return;

                // SetupGui writes the version line AFTER our entry point runs, so the first
                // frames legitimately have no badge. Re-appending whenever it is missing covers
                // that, a language change, and anything else that rewrites the label.
                if (current.Contains(Title)) return;

                _textProp.SetValue(_label, current + BadgeLine(), null);
            }
            catch (Exception e)
            {
                if (!_complained)
                {
                    _complained = true;
                    UnityEngine.Debug.LogWarning($"[ICanShowYouTheWorld] Could not brand the menu version label: {e.Message}");
                }
                _label = null;
                _seenStartup = null;
            }
        }

        private static string BadgeLine()
        {
            return $"\n<size=70%><color=#{Gold}>{Title} v{ModVersion.VERSION}</color></size>";
        }

        /// <summary>Sets a property if this build of TMP has it. Silent either way.</summary>
        private static void TrySet(object target, string property, object value)
        {
            try
            {
                var prop = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || !prop.CanWrite) return;

                object coerced = prop.PropertyType.IsEnum
                    ? Enum.ToObject(prop.PropertyType, value)
                    : Convert.ChangeType(value, prop.PropertyType);

                prop.SetValue(target, coerced, null);
            }
            catch { /* the line is short enough without it */ }
        }

        private static bool Resolve(FejdStartup fejd)
        {
            var field = typeof(FejdStartup).GetField("m_versionLabel",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return false;

            var label = field.GetValue(fejd);

            // The field is a UnityEngine.Object, so an unassigned one is a live-looking null.
            if (label == null || ((UnityEngine.Object)label) == null) return false;

            var prop = label.GetType().GetProperty("text",
                BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanRead || !prop.CanWrite) return false;

            _label = label;
            _textProp = prop;

            // Belt and braces for the wrap. The line is already short enough not to need it, but a
            // longer version string one day would put the overlap straight back, and this costs one
            // reflective set that is allowed to fail.
            TrySet(label, "enableWordWrapping", false);
            TrySet(label, "textWrappingMode", 0);   // TMP renamed it; 0 is NoWrap in both

            return true;
        }
    }
}
