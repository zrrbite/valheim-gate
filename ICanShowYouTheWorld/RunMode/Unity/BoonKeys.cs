using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Which key activates which boon, in ONE place.
    ///
    /// It lived in three: an if/else chain in RunService that read the keys, a switch in RunWindow
    /// that printed them, and a hand-written "[Ins]" at the end of some descriptions. Three copies
    /// of one fact, and the failure mode is the quiet one this codebase keeps meeting — a boon that
    /// activates perfectly and never tells the player which key does it. The owner named the
    /// requirement exactly: "as long as the key is indicated by the saga mode".
    ///
    /// So the table is the definition, the input handler walks it, the HUD labels from it, and the
    /// offer panel appends the label itself. Adding an active is one row here, and forgetting to
    /// show its key is no longer possible.
    ///
    /// It lives in the Unity layer rather than on <see cref="BoonDefinition"/> because
    /// <c>KeyCode</c> is UnityEngine, and RunMode's pure half is compiled without Unity at all by
    /// the test runner. The boon's IDENTITY is pure; which key a keyboard presses for it is not.
    ///
    /// Keys are scoped per MODE, which is what makes reuse safe: the GM mod's bindings go through
    /// InputManager.Gate and are dead while a run is live, and the dev keys sit behind Shift
    /// because they share this handler and this mode. See CLAUDE.md.
    /// </summary>
    internal static class BoonKeys
    {
        public struct Binding
        {
            public string Id;
            public KeyCode Key;

            /// <summary>What the HUD and the offer card show. Short: the status column is 104px.</summary>
            public string Label;
        }

        /// <summary>
        /// Every activatable boon and its key. Keypad1-3 are deliberately absent — they pick from an
        /// offer, and the activation handler stands down while one is up.
        /// </summary>
        public static readonly Binding[] Actives =
        {
            new Binding { Id = "wind",      Key = KeyCode.Keypad4,     Label = "[4]" },
            new Binding { Id = "ember",     Key = KeyCode.Keypad5,     Label = "[5]" },
            new Binding { Id = "way",       Key = KeyCode.Keypad6,     Label = "[6]" },
            new Binding { Id = "brother",   Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "windfall",  Key = KeyCode.Keypad8,     Label = "[8]" },
            new Binding { Id = "bonecaller",Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "menagerie", Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "shaman",    Key = KeyCode.KeypadPlus,  Label = "[+]" },
            new Binding { Id = "unseen",    Key = KeyCode.KeypadMinus, Label = "[-]" },
        };

        /// <summary>The key label for a boon, or empty for a passive. Never null.</summary>
        public static string Label(string boonId)
        {
            if (string.IsNullOrEmpty(boonId)) return string.Empty;

            foreach (var binding in Actives)
                if (binding.Id == boonId) return binding.Label;

            return string.Empty;
        }

        /// <summary>True when this build has a key for that boon — i.e. the player can be told one.</summary>
        public static bool IsBound(string boonId) => !string.IsNullOrEmpty(Label(boonId));
    }
}
