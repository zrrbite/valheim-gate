using System;
using System.Collections.Generic;
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

            /// <summary>Which saga action this boon is pressed with. The KEY is the layout's (<see cref="KeyLayout"/>).</summary>
            public SagaKey Slot;

            public KeyCode Key => Code(Slot);

            /// <summary>What the HUD and the offer card show. Short: the status column is 104px.</summary>
            public string Label => KeyLabel(Slot);

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
            new Binding { Id = "wind",      Slot = SagaKey.Wind },
            new Binding { Id = "ember",     Slot = SagaKey.Ember },
            new Binding { Id = "way",       Slot = SagaKey.Way },
            new Binding { Id = "windfall",  Slot = SagaKey.Windfall },
            new Binding { Id = "mend",      Slot = SagaKey.Mend },
            new Binding { Id = "farsight",  Slot = SagaKey.Farsight },

            // Rung 1 of each way.
            new Binding { Id = "brother",   Slot = SagaKey.Rung1 },
            new Binding { Id = "shaman",    Slot = SagaKey.Rung1 },
            new Binding { Id = "rend",      Slot = SagaKey.Rung1 },
            new Binding { Id = "bash",      Slot = SagaKey.Rung1 },
            new Binding { Id = "march",     Slot = SagaKey.Rung1 },
            new Binding { Id = "tide",      Slot = SagaKey.Rung1 },
            new Binding { Id = "fieldforge",Slot = SagaKey.Rung1 },

            // Rung 2.
            new Binding { Id = "menagerie", Slot = SagaKey.Rung2 },
            new Binding { Id = "bonecaller",Slot = SagaKey.Rung2 },
            new Binding { Id = "rage",      Slot = SagaKey.Rung2 },
            new Binding { Id = "bulwark",   Slot = SagaKey.Rung2 },
            new Binding { Id = "warsong",   Slot = SagaKey.Rung2 },
            new Binding { Id = "fairwind",  Slot = SagaKey.Rung2 },
            new Binding { Id = "mastersminute", Slot = SagaKey.Rung2 },

            // The Hunter's second rung-2 boon, beside Menagerie on [0]: a switch for Thor's bow,
            // so it gets keys that read as "next / previous". The arrow keys are free in the saga
            // and in vanilla play, and the GM mod's arrow bindings are gated dead during a run.
            new Binding { Id = "elemental", Slot = SagaKey.ElementNext },
            new Binding { Id = "elemental", Slot = SagaKey.ElementPrev, Reverse = true },

            // Rung 3.
            new Binding { Id = "unseen",    Slot = SagaKey.Rung3 },
            new Binding { Id = "wrath",     Slot = SagaKey.Rung3 },
            new Binding { Id = "warcry",    Slot = SagaKey.Rung3 },
            new Binding { Id = "laststand", Slot = SagaKey.Rung3 },
            new Binding { Id = "bragi",     Slot = SagaKey.Rung3 },
            new Binding { Id = "sealegs",   Slot = SagaKey.Rung3 },
            new Binding { Id = "reinforce", Slot = SagaKey.Rung3 },
        };

        /// <summary>The key label for a boon, or empty for a passive. Never null.</summary>
        // --- The layout (2026-10-06): which physical key each SagaKey is, from the config's runKeyLayout ---

        private static string _layout = KeyLayout.Numpad;
        private static readonly Dictionary<SagaKey, KeyCode> Codes = new Dictionary<SagaKey, KeyCode>();

        /// <summary>The layout in use: "numpad" or "laptop".</summary>
        public static string Layout => _layout;

        /// <summary>
        /// Resolves every saga key for <paramref name="layout"/>. An unknown name gets the numpad and
        /// says so (a typo in the config must not leave the saga with no keys). Returns the key names
        /// that are not Unity KeyCodes, for the self-check; KeyLayoutTests keeps that list empty in
        /// principle, but only the game can parse a KeyCode.
        /// </summary>
        public static List<string> UseLayout(string layout)
        {
            if (!KeyLayout.IsKnown(layout))
                Debug.LogWarning($"[ICanShowYouTheWorld] runKeyLayout '{layout}' is not a layout (numpad, laptop) - using the numpad.");

            _layout = KeyLayout.IsKnown(layout) ? layout.ToLowerInvariant() : KeyLayout.Numpad;
            Codes.Clear();

            var unparsed = new List<string>();
            foreach (var pair in KeyLayout.For(_layout))
            {
                try { Codes[pair.Key] = (KeyCode)Enum.Parse(typeof(KeyCode), pair.Value); }
                catch { Codes[pair.Key] = KeyCode.None; unparsed.Add(pair.Value); }
            }

            Debug.Log($"[ICanShowYouTheWorld] Saga keys: the {_layout} layout.");
            return unparsed;
        }

        /// <summary>The physical key for a saga action in the current layout.</summary>
        public static KeyCode Code(SagaKey key)
        {
            if (Codes.Count == 0) UseLayout(_layout);
            return Codes.TryGetValue(key, out var code) ? code : KeyCode.None;
        }

        /// <summary>A saga action's key as the HUD prints it, e.g. "[4]" or "[J]".</summary>
        public static string KeyLabel(SagaKey key) =>
            KeyLayout.Label(KeyLayout.For(_layout).TryGetValue(key, out var name) ? name : null);

        public static bool Pressed(SagaKey key) => Input.GetKeyDown(Code(key));

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
