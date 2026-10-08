using System;
using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>Every saga action a key can press. Which key is the layout's business (<see cref="KeyLayout"/>).</summary>
    public enum SagaKey
    {
        // A card's lines: the boon offer (3), the helm's fittings (4), the thane's ways (7).
        Choice1, Choice2, Choice3, Choice4, Choice5, Choice6, Choice7,

        // General actives, then the three rungs of whichever way the run took up.
        Wind, Ember, Way, Windfall, Mend, Farsight,
        Rung1, Rung2, Rung3,
        ElementNext, ElementPrev,

        Homeward, GateBack, WindHorn,

        // Dev builds only. Star, Slash and Delete are read bare AND with a modifier; Plus, Minus and
        // Backspace only with one; the rest only bare. See RunService.HandleDevInput.
        DevStar, DevSlash, DevDot, DevEnter, DevPlus, DevMinus, DevDelete, DevHome, DevPageUp, DevBackspace,

        // MACBOOK-TEMP (2026-10-06): hop between the saga's places, bare forward, with a modifier back.
        // For testing on a MacBook with no mouse; remove with the rest of MACBOOK-TEMP.
        DevGoTo,

        // MACBOOK-TEMP (2026-10-06): a ship on the nearest deep water, with you at its helm. Always with a
        // modifier: a ship persists, like the anvil.
        DevShip,
    }

    /// <summary>
    /// Which physical key does what, per layout (2026-10-06).
    ///
    /// The saga was built on a full PC keyboard: the numpad for picks and actives, Insert for a third
    /// rung. The owner's MacBook has neither, so boons could not be chosen at all. The "laptop" layout
    /// maps every action onto keys a MacBook has AND Valheim leaves free: the letters on the right
    /// hand (choices on the home row J K L, the way's rungs above them on U I O), 9 for Homeward, and
    /// for the tester Z, 0, B and Backspace.
    ///
    /// Key names are Unity <c>KeyCode</c> names, so this file stays pure (the test runner compiles
    /// RunMode without Unity) and the game side parses them. Labels are what the HUD prints.
    /// </summary>
    public static class KeyLayout
    {
        public const string Numpad = "numpad";
        public const string Laptop = "laptop";

        /// <summary>
        /// The key a ladder rung is pressed with: rung 0 is <see cref="SagaKey.Rung1"/>, rung 1 is Rung2, and any
        /// later rung Rung3 (a way has three; a fourth would clash, which the run-start check reports). The
        /// rung itself is <c>ClassLadder.RungIndex</c>'s; BoonKeys composes the two for every rung boon.
        /// </summary>
        public static SagaKey RungKey(int rungIndex) =>
            rungIndex == 0 ? SagaKey.Rung1 : rungIndex == 1 ? SagaKey.Rung2 : SagaKey.Rung3;

        public static readonly SagaKey[] Choices =
        {
            SagaKey.Choice1, SagaKey.Choice2, SagaKey.Choice3, SagaKey.Choice4,
            SagaKey.Choice5, SagaKey.Choice6, SagaKey.Choice7,
        };

        /// <summary>Pressed in play, so no two may share a key. Choices may reuse these: every handler stands down while a card is up.</summary>
        public static readonly SagaKey[] PlayKeys =
        {
            SagaKey.Wind, SagaKey.Ember, SagaKey.Way, SagaKey.Windfall, SagaKey.Mend, SagaKey.Farsight,
            SagaKey.Rung1, SagaKey.Rung2, SagaKey.Rung3, SagaKey.ElementNext, SagaKey.ElementPrev,
            SagaKey.Homeward, SagaKey.GateBack, SagaKey.WindHorn,
        };

        /// <summary>Dev keys read with no modifier held: never a player's key.</summary>
        public static readonly SagaKey[] BareDevKeys =
        {
            SagaKey.DevStar, SagaKey.DevSlash, SagaKey.DevDot, SagaKey.DevEnter,
            SagaKey.DevDelete, SagaKey.DevHome, SagaKey.DevPageUp,
            SagaKey.DevGoTo,   // MACBOOK-TEMP
        };

        /// <summary>Dev keys read with Shift, Ctrl or Alt held. The player's handler stands down while one is.</summary>
        public static readonly SagaKey[] ModifiedDevKeys =
        {
            SagaKey.DevStar, SagaKey.DevSlash, SagaKey.DevDelete,
            SagaKey.DevPlus, SagaKey.DevMinus, SagaKey.DevBackspace,
            SagaKey.DevGoTo, SagaKey.DevShip,   // MACBOOK-TEMP
        };

        /// <summary>
        /// Every keyboard key Valheim 1.0.17 binds by default, as Unity KeyCode names - read from
        /// <c>ZInput</c>'s default bindings in assembly_utils (each <c>AddButton</c> beside a
        /// <c>KeyToPath(Key)</c>), not remembered. A saga key must avoid these, chat-only ones aside.
        /// </summary>
        public static readonly string[] VanillaBound =
        {
            "Space", "Return", "Tab", "Comma", "Period", "Escape",
            "W", "A", "S", "D", "C", "E", "F", "G", "M", "Q", "R", "T", "V", "X",
            "Alpha1", "Alpha2", "Alpha3", "Alpha4", "Alpha5", "Alpha6", "Alpha7", "Alpha8",
            "LeftShift", "LeftControl", "LeftCommand",
            "UpArrow", "DownArrow", "PageUp", "PageDown", "F5",
        };

        /// <summary>Bound by Valheim only to scroll the chat, so they act only while typing - when the saga's keys stand down.</summary>
        public static readonly string[] ChatOnly = { "UpArrow", "DownArrow", "PageUp", "PageDown" };

        private static readonly Dictionary<SagaKey, string> NumpadKeys = new Dictionary<SagaKey, string>
        {
            [SagaKey.Choice1] = "Keypad1", [SagaKey.Choice2] = "Keypad2", [SagaKey.Choice3] = "Keypad3",
            [SagaKey.Choice4] = "Keypad4", [SagaKey.Choice5] = "Keypad5", [SagaKey.Choice6] = "Keypad6",
            [SagaKey.Choice7] = "Keypad7",
            [SagaKey.Wind] = "Keypad4", [SagaKey.Ember] = "Keypad5", [SagaKey.Way] = "Keypad6",
            [SagaKey.Windfall] = "Keypad8", [SagaKey.Mend] = "KeypadPlus", [SagaKey.Farsight] = "KeypadMinus",
            [SagaKey.Rung1] = "Keypad7", [SagaKey.Rung2] = "Keypad0", [SagaKey.Rung3] = "Insert",
            [SagaKey.ElementNext] = "RightArrow", [SagaKey.ElementPrev] = "LeftArrow",
            [SagaKey.Homeward] = "Keypad9", [SagaKey.GateBack] = "PageDown", [SagaKey.WindHorn] = "UpArrow",
            [SagaKey.DevStar] = "KeypadMultiply", [SagaKey.DevSlash] = "KeypadDivide",
            [SagaKey.DevDot] = "KeypadPeriod", [SagaKey.DevEnter] = "KeypadEnter",
            [SagaKey.DevPlus] = "KeypadPlus", [SagaKey.DevMinus] = "KeypadMinus",
            [SagaKey.DevDelete] = "Delete", [SagaKey.DevHome] = "Home", [SagaKey.DevPageUp] = "PageUp",
            [SagaKey.DevBackspace] = "Backspace",
            [SagaKey.DevGoTo] = "DownArrow",   // MACBOOK-TEMP: free in play, Valheim scrolls chat with it
            [SagaKey.DevShip] = "KeypadPeriod",   // MACBOOK-TEMP: with a modifier; bare it is the light
        };

        private static readonly Dictionary<SagaKey, string> LaptopKeys = new Dictionary<SagaKey, string>
        {
            // The home row for the three lines every offer has; H for the helm's fourth; U I O, the
            // row above, for the thane's last three ways.
            [SagaKey.Choice1] = "J", [SagaKey.Choice2] = "K", [SagaKey.Choice3] = "L",
            [SagaKey.Choice4] = "H", [SagaKey.Choice5] = "U", [SagaKey.Choice6] = "I",
            [SagaKey.Choice7] = "O",
            // J K L double as the numpad's 4 5 6 did: they are actives only while no card is up.
            [SagaKey.Wind] = "J", [SagaKey.Ember] = "K", [SagaKey.Way] = "L",
            [SagaKey.Windfall] = "Y", [SagaKey.Mend] = "P", [SagaKey.Farsight] = "N",
            [SagaKey.Rung1] = "U", [SagaKey.Rung2] = "I", [SagaKey.Rung3] = "O",
            [SagaKey.ElementNext] = "RightArrow", [SagaKey.ElementPrev] = "LeftArrow",
            [SagaKey.Homeward] = "Alpha9", [SagaKey.GateBack] = "PageDown", [SagaKey.WindHorn] = "UpArrow",
            // A MacBook has Delete, Home and Page Up as fn + Backspace / Left / Up.
            [SagaKey.DevStar] = "Z", [SagaKey.DevSlash] = "Alpha0",
            [SagaKey.DevDot] = "B", [SagaKey.DevEnter] = "Backspace",
            [SagaKey.DevPlus] = "P", [SagaKey.DevMinus] = "N",
            [SagaKey.DevDelete] = "Delete", [SagaKey.DevHome] = "Home", [SagaKey.DevPageUp] = "PageUp",
            [SagaKey.DevBackspace] = "Backspace",
            [SagaKey.DevGoTo] = "DownArrow",   // MACBOOK-TEMP
            [SagaKey.DevShip] = "B",   // MACBOOK-TEMP: B for boat, with a modifier; bare it is the light
        };

        public static bool IsKnown(string layout) =>
            string.Equals(layout, Numpad, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(layout, Laptop, StringComparison.OrdinalIgnoreCase);

        /// <summary>The layout's keys. An unknown name gets the numpad: a typo in the config must not leave the saga with no keys.</summary>
        public static IReadOnlyDictionary<SagaKey, string> For(string layout) =>
            string.Equals(layout, Laptop, StringComparison.OrdinalIgnoreCase) ? LaptopKeys : NumpadKeys;

        /// <summary>A key as the HUD prints it: short, because the status column is 104 px.</summary>
        public static string Label(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return string.Empty;

            switch (keyName)
            {
                case "KeypadPlus": return "[+]";
                case "KeypadMinus": return "[-]";
                case "KeypadMultiply": return "[*]";
                case "KeypadDivide": return "[/]";
                case "KeypadPeriod": return "[.]";
                case "KeypadEnter": return "[Ent]";
                case "Insert": return "[Ins]";
                case "Delete": return "[Del]";
                case "Backspace": return "[Bksp]";
                case "PageDown": return "[PgDn]";
                case "PageUp": return "[PgUp]";
                case "RightArrow": return "[→]";
                case "LeftArrow": return "[←]";
                case "UpArrow": return "[↑]";
                case "DownArrow": return "[↓]";
            }

            if (keyName.StartsWith("Keypad", StringComparison.Ordinal)) return "[" + keyName.Substring(6) + "]";
            if (keyName.StartsWith("Alpha", StringComparison.Ordinal)) return "[" + keyName.Substring(5) + "]";
            return "[" + keyName + "]";
        }

        /// <summary>The line under a card: which keys pick, and that TAB and a click always work.</summary>
        public static string ChoiceHint(string layout, int count)
        {
            count = Math.Max(1, Math.Min(count, Choices.Length));
            const string click = ", or TAB and click";

            if (!string.Equals(layout, Laptop, StringComparison.OrdinalIgnoreCase))
                return (count == 1 ? "press Keypad 1" : "press Keypad 1–" + count) + click;

            var keys = For(layout);
            return "press " + string.Join(" ", Choices.Take(count).Select(c => keys[c]).ToArray()) + click;
        }
    }

    /// <summary>
    /// When the saga's lobby may open itself (2026-10-06). It opened over a new character's Valkyrie
    /// intro: the player exists from the intro's first frame, and the game counts the intro - like
    /// sleep and a cinematic video - as a cutscene (<c>Player.InCutscene</c>).
    /// </summary>
    public static class LobbyOffer
    {
        public static bool Shows(bool pending, bool runActive, bool playerInCutscene) =>
            pending && !runActive && !playerInCutscene;
    }
}
