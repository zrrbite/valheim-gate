using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The saga's keys, per layout (2026-10-06). The owner's MacBook has no numpad and no Insert, so
/// boons could not be chosen at all. A "laptop" layout maps every saga action to keys the laptop has
/// and Valheim leaves free - read from 1.0.17's own ZInput defaults, not remembered.
/// </summary>
static class KeyLayoutTests
{
    public static void Run()
    {
        var numpad = KeyLayout.For(KeyLayout.Numpad);
        var laptop = KeyLayout.For(KeyLayout.Laptop);

        // The numpad layout IS the keys the saga has always had: changing it would retrain every hand.
        Check.That(numpad[SagaKey.Choice1] == "Keypad1" && numpad[SagaKey.Choice7] == "Keypad7" &&
                   numpad[SagaKey.Wind] == "Keypad4" && numpad[SagaKey.Ember] == "Keypad5" &&
                   numpad[SagaKey.Way] == "Keypad6" && numpad[SagaKey.Windfall] == "Keypad8" &&
                   numpad[SagaKey.Mend] == "KeypadPlus" && numpad[SagaKey.Farsight] == "KeypadMinus" &&
                   numpad[SagaKey.Rung1] == "Keypad7" && numpad[SagaKey.Rung2] == "Keypad0" &&
                   numpad[SagaKey.Rung3] == "Insert" && numpad[SagaKey.Homeward] == "Keypad9" &&
                   numpad[SagaKey.GateBack] == "PageDown" && numpad[SagaKey.WindHorn] == "UpArrow" &&
                   numpad[SagaKey.ElementNext] == "RightArrow" && numpad[SagaKey.ElementPrev] == "LeftArrow",
                   "the numpad layout is exactly the saga's keys as they were");
        Check.That(numpad[SagaKey.DevStar] == "KeypadMultiply" && numpad[SagaKey.DevSlash] == "KeypadDivide" &&
                   numpad[SagaKey.DevDot] == "KeypadPeriod" && numpad[SagaKey.DevEnter] == "KeypadEnter" &&
                   numpad[SagaKey.DevPlus] == "KeypadPlus" && numpad[SagaKey.DevMinus] == "KeypadMinus" &&
                   numpad[SagaKey.DevDelete] == "Delete" && numpad[SagaKey.DevHome] == "Home" &&
                   numpad[SagaKey.DevPageUp] == "PageUp" && numpad[SagaKey.DevBackspace] == "Backspace",
                   "the numpad layout keeps the dev keys as they were");

        foreach (var (name, map) in new[] { ("numpad", numpad), ("laptop", laptop) })
        {
            Check.That(System.Enum.GetValues(typeof(SagaKey)).Cast<SagaKey>().All(k => map.ContainsKey(k) && !string.IsNullOrEmpty(map[k])),
                       $"{name}: every saga action has a key");

            var choices = KeyLayout.Choices.Select(k => map[k]).ToList();
            Check.That(choices.Distinct().Count() == choices.Count, $"{name}: the seven choice keys are distinct");

            // Abilities, rungs and the rest are pressed in play, so no two may share a key. Choices may
            // reuse them: every handler stands down while a card is up (the numpad's [4] already does this).
            var play = KeyLayout.PlayKeys.Select(k => map[k]).ToList();
            Check.That(play.Distinct().Count() == play.Count, $"{name}: no two play keys share a key");

            // A bare dev key must never be a player's key, or one press is both the tester's and the player's.
            var bareDev = KeyLayout.BareDevKeys.Select(k => map[k]).ToList();
            Check.That(bareDev.Distinct().Count() == bareDev.Count && !bareDev.Intersect(play.Concat(choices)).Any(),
                       $"{name}: bare dev keys are distinct and never a player's key");

            var modDev = KeyLayout.ModifiedDevKeys.Select(k => map[k]).ToList();
            Check.That(modDev.Distinct().Count() == modDev.Count, $"{name}: dev keys taken with a modifier are distinct");

            // Only keys Valheim's own defaults leave free. The chat-only keys are exempt: the game reads
            // them only while typing, and the saga's keys stand down then.
            var taken = KeyLayout.VanillaBound.Except(KeyLayout.ChatOnly).ToList();
            var clash = play.Concat(choices).Concat(bareDev).Concat(modDev).Intersect(taken).ToList();
            Check.That(clash.Count == 0, $"{name}: no saga key is one Valheim binds ({string.Join(", ", clash.ToArray())})");
        }

        // The point of the laptop layout: nothing a MacBook does not have.
        Check.That(!laptop.Values.Any(v => v.StartsWith("Keypad") || v == "Insert"),
                   "the laptop layout uses no numpad key and no Insert");

        Check.That(KeyLayout.IsKnown("laptop") && KeyLayout.IsKnown("NUMPAD") && !KeyLayout.IsKnown("laptpo"),
                   "layout names are known case-insensitively, and a typo is not known");
        Check.That(KeyLayout.For("laptpo")[SagaKey.Wind] == "Keypad4",
                   "an unknown layout falls back to the numpad rather than leaving keys unbound");

        Check.That(KeyLayout.Label("Keypad4") == "[4]" && KeyLayout.Label("KeypadPlus") == "[+]" &&
                   KeyLayout.Label("KeypadMinus") == "[-]" && KeyLayout.Label("KeypadMultiply") == "[*]" &&
                   KeyLayout.Label("KeypadDivide") == "[/]" && KeyLayout.Label("KeypadPeriod") == "[.]" &&
                   KeyLayout.Label("KeypadEnter") == "[Ent]" && KeyLayout.Label("Insert") == "[Ins]",
                   "numpad keys keep the short labels the HUD has always shown");
        Check.That(KeyLayout.Label("J") == "[J]" && KeyLayout.Label("Alpha9") == "[9]" &&
                   KeyLayout.Label("RightArrow") == "[→]" && KeyLayout.Label("LeftArrow") == "[←]" &&
                   KeyLayout.Label("UpArrow") == "[↑]" && KeyLayout.Label("PageDown") == "[PgDn]" &&
                   KeyLayout.Label("Backspace") == "[Bksp]" && KeyLayout.Label("Delete") == "[Del]",
                   "letters, digits, arrows and named keys read as a player would say them");

        Check.That(KeyLayout.ChoiceHint(KeyLayout.Numpad, 3) == "press Keypad 1–3, or TAB and click",
                   "the numpad's choice hint names the keypad and the click");
        Check.That(KeyLayout.ChoiceHint(KeyLayout.Laptop, 3) == "press J K L, or TAB and click" &&
                   KeyLayout.ChoiceHint(KeyLayout.Laptop, 1) == "press J, or TAB and click",
                   "the laptop's choice hint lists the letters, only as many as there are choices");

        LobbyOfferTests();
    }

    /// <summary>
    /// 2026-10-06: the lobby opened over a new character's Valkyrie intro. The game counts the intro
    /// (and sleep, and a cinematic video) as a cutscene; the lobby waits for it to end.
    /// </summary>
    static void LobbyOfferTests()
    {
        Check.That(LobbyOffer.Shows(pending: true, runActive: false, playerInCutscene: false),
                   "a pending offer shows once the player is free");
        Check.That(!LobbyOffer.Shows(pending: true, runActive: false, playerInCutscene: true),
                   "a pending offer waits while the player is in a cutscene (the Valkyrie intro)");
        Check.That(!LobbyOffer.Shows(pending: true, runActive: true, playerInCutscene: false) &&
                   !LobbyOffer.Shows(pending: false, runActive: false, playerInCutscene: false),
                   "no offer during a run, and none that was not made");
    }
}
