using System;
using System.Reflection;
using UnityEngine;

namespace ICanShowYouTheWorld
{
    /// <summary>
    /// Frees the mouse pointer while one of the mod's own windows is open, so the buttons in them can
    /// be clicked without first opening the inventory.
    /// </summary>
    /// <remarks>
    /// Owner: "i have to TAB to use the mouse to click things. Is there a better way?" There is, and
    /// it is the game's own.
    ///
    /// Valheim keeps the cursor locked and hidden while you play, and frees it in exactly one place:
    /// <c>GameCamera.UpdateMouseCapture</c>, which unlocks and shows it whenever
    /// <c>m_mouseCapture</c> is false OR one of the game's own panels is up — the inventory, the map,
    /// a store, the menu. TAB works because it satisfies the second clause. This satisfies the first.
    ///
    /// <c>m_mouseCapture</c> is also what **vanilla F1 toggles**, read straight out of
    /// <c>UpdateMouseCapture</c>'s IL: <c>ZInput.GetKeyDown((KeyCode)282)</c>, and 282 is F1. So
    /// forcing it false is not a new state invented by this mod — it is the state the game itself
    /// enters when you press F1, which is the strongest argument available that nothing else breaks.
    /// The field is private, hence the one cached <see cref="FieldInfo"/>; the alternative was writing
    /// <c>ZCursor</c> directly every frame and fighting <c>UpdateMouseCapture</c> for it, which would
    /// have been our state rather than the game's.
    ///
    /// It restores capture when the last mod window closes, and only if it was this class that took
    /// it away — so a player who pressed F1 themselves keeps the cursor they asked for.
    /// </remarks>
    internal static class ModCursor
    {
        private static FieldInfo _capture;
        private static bool _resolved;
        private static bool _weFreedIt;
        private static bool _complained;

        /// <summary>
        /// Called every frame from <see cref="CheatController"/>. Two field writes at most, and
        /// nothing at all once the state already matches.
        /// </summary>
        /// <param name="wantCursor">True while any of the mod's windows is on screen.</param>
        public static void Tick(bool wantCursor)
        {
            try
            {
                var camera = GameCamera.instance;
                if (camera == null)
                {
                    // No camera means no game scene; nothing to restore to either.
                    _weFreedIt = false;
                    return;
                }

                var field = Capture();
                if (field == null) return;

                if (wantCursor)
                {
                    // Written every frame rather than once, because UpdateMouseCapture runs every
                    // frame too and F1 or a closing panel would otherwise take it back while our
                    // window is still up.
                    if ((bool)field.GetValue(camera))
                    {
                        field.SetValue(camera, false);
                    }
                    _weFreedIt = true;
                    return;
                }

                // Give it back exactly once, and only what we took.
                if (_weFreedIt)
                {
                    _weFreedIt = false;
                    field.SetValue(camera, true);
                }
            }
            catch (Exception e)
            {
                if (!_complained)
                {
                    _complained = true;
                    Debug.LogWarning("[ICanShowYouTheWorld] Could not free the cursor: " + e.Message +
                                     " — press F1 or TAB instead.");
                }
            }
        }

        private static FieldInfo Capture()
        {
            if (_resolved) return _capture;
            _resolved = true;

            _capture = typeof(GameCamera).GetField("m_mouseCapture",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_capture == null || _capture.FieldType != typeof(bool))
            {
                _capture = null;
                Debug.LogWarning("[ICanShowYouTheWorld] GameCamera.m_mouseCapture is not where it was; " +
                                 "the cursor will stay locked. F1 is the game's own way to free it.");
            }

            return _capture;
        }
    }
}
