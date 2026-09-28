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
    /// InputManager.Gate and are dead while a run is live.
    ///
    /// Within the saga, the three ability keys are per SLOT, shared across the ways: [7], [0] and
    /// [Ins] are rungs 1, 2 and 3 of whichever way the run took up. A run holds one way, so at most
    /// one id on a shared key is ever held, and the activation handler fires the HELD row rather
    /// than the first row that matches. Keeping a way's three rungs on three distinct keys is
    /// checked at run start (RunService.ValidateClassLadder). General actives keep keys of their own.
    ///
    /// Keypad+ and Keypad- were freed when Mending and Unseen moved into the ways, and taken again
    /// the same day by two general actives, Mending Hands and Farsight. The dev layer, which shares
    /// this handler and this mode, takes them only with a modifier held — for the original reason
    /// once more: without it one press would be both the tester's and the player's. See CLAUDE.md
    /// and RunService.HandleDevInput. No two GENERAL actives may share a key (a run can hold them
    /// all); ValidateClassLadder checks that too.
    /// </summary>
    internal static class BoonKeys
    {
        public struct Binding
        {
            public string Id;
            public KeyCode Key;

            /// <summary>What the HUD and the offer card show. Short: the status column is 104px.</summary>
            public string Label;

            /// <summary>
            /// The second key of a boon that has two, pressed to go the other way. Only Elemental
            /// Arrows has one: Right cycles Thor's bow forward, Left back. A reversed row is never
            /// the one <see cref="Label"/> answers with, because the forward row is listed first.
            /// </summary>
            public bool Reverse;
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
            new Binding { Id = "windfall",  Key = KeyCode.Keypad8,     Label = "[8]" },
            new Binding { Id = "mend",      Key = KeyCode.KeypadPlus,  Label = "[+]" },
            new Binding { Id = "farsight",  Key = KeyCode.KeypadMinus, Label = "[-]" },

            // Rung 1 of each way.
            new Binding { Id = "brother",   Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "shaman",    Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "rend",      Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "bash",      Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "march",     Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "tide",      Key = KeyCode.Keypad7,     Label = "[7]" },
            new Binding { Id = "fieldforge",Key = KeyCode.Keypad7,     Label = "[7]" },

            // Rung 2.
            new Binding { Id = "menagerie", Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "bonecaller",Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "rage",      Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "bulwark",   Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "warsong",   Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "fairwind",  Key = KeyCode.Keypad0,     Label = "[0]" },
            new Binding { Id = "mastersminute", Key = KeyCode.Keypad0, Label = "[0]" },

            // The Hunter's second rung-2 boon, beside Menagerie on [0]: a switch for Thor's bow,
            // so it gets keys that read as "next / previous". The arrow keys are free in the saga
            // and in vanilla play, and the GM mod's arrow bindings are gated dead during a run.
            new Binding { Id = "elemental", Key = KeyCode.RightArrow,  Label = "[\u2192]" },
            new Binding { Id = "elemental", Key = KeyCode.LeftArrow,   Label = "[\u2190]", Reverse = true },

            // Rung 3.
            new Binding { Id = "unseen",    Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "wrath",     Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "warcry",    Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "laststand", Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "bragi",     Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "sealegs",   Key = KeyCode.Insert,      Label = "[Ins]" },
            new Binding { Id = "reinforce", Key = KeyCode.Insert,      Label = "[Ins]" },
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
