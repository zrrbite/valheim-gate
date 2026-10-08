using System;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The ways' numbers (2026-10-08, class balance): docs/superpowers/specs/2026-10-08-class-balance-design.md.
/// </summary>
static class WayRulesTests
{
    public static void Run()
    {
        // The damage ceiling.
        Check.That(WayRules.WeaponProduct(new float[0]) == 1f, "no weapon bonus: x1");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 1.2f, 1.5f }) - 1.8f) < 0.001f, "bonuses multiply below the ceiling");
        Check.That(WayRules.WeaponProduct(new[] { 1.2f, 1.4f, 1.2f, 1.5f, 1.5f }) == 2.5f,
                   "Sharpened, Glass Cannon, Stoker, Reckless and Fury together stop at x2.5, not x4.5");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 0.8f }) - 0.8f) < 0.001f, "a factor below one still applies");
        Check.That(WayRules.WeaponProduct(null) == 1f, "nothing at all is x1, not an error");

        // Tempering: one per god from Bonemass to the Queen.
        Check.That(!WayRules.Tempered(2, TemperSlot.Rung1) && WayRules.Tempered(3, TemperSlot.Rung1) &&
                   !WayRules.Tempered(3, TemperSlot.Rung2) && WayRules.Tempered(4, TemperSlot.Rung2) &&
                   WayRules.Tempered(5, TemperSlot.Rung3) && !WayRules.Tempered(5, TemperSlot.Engine) &&
                   WayRules.Tempered(6, TemperSlot.Engine),
                   "Bonemass tempers rung I, Moder rung II, Yagluth rung III, the Queen the engine");
        Check.That(!WayRules.TemperShown(1, TemperSlot.Rung1) && WayRules.TemperShown(2, TemperSlot.Rung1) &&
                   !WayRules.TemperShown(2, TemperSlot.Rung2),
                   "a tempering is shown only once its god is the next to fall - no names from later acts");
        foreach (var way in ClassLadder.Catalog())
            foreach (TemperSlot slot in Enum.GetValues(typeof(TemperSlot)))
                Check.That(!string.IsNullOrEmpty(WayRules.TemperLine(way.Id, slot)), $"{way.Id} has a {slot} tempering line");
        Check.That(WayRules.TemperLine("nobody", TemperSlot.Rung1) == "", "an unknown way has none, not an error");

        // Menagerie's beasts by biome.
        Check.That(WayRules.MenagerieBeasts(0).SequenceEqual(new[] { "Boar", "Hen", "Chicken" }),
                   "the Meadows' beasts at first - no Lox to reroll for in Act II");
        Check.That(WayRules.MenagerieBeasts(3).Contains("Wolf") && !WayRules.MenagerieBeasts(3).Contains("Lox"),
                   "the wolf after Bonemass, when the Mountains open");
        Check.That(WayRules.MenagerieBeasts(4).Contains("Lox") && !WayRules.MenagerieBeasts(5).Contains("Asksvin") &&
                   WayRules.MenagerieBeasts(6).Contains("Asksvin"),
                   "the Lox after Moder, the Asksvin after the Queen");

        // The Hunter's tempering.
        Check.That(WayRules.PackSize(2) == 2 && WayRules.PackSize(3) == 3, "two wolves at a time; three after Bonemass");
        Check.That(WayRules.BowElementScale(3) == 1f && WayRules.BowElementScale(4) == 1.5f, "the bow's element x1.5 after Moder");
        Check.That(WayRules.UnseenSeconds(4) == 20f && WayRules.UnseenSeconds(5) == 30f, "Unseen 20 s, 30 s after Yagluth");
        Check.That(WayRules.PackRegenPerSecond(5) == 0f && WayRules.PackRegenPerSecond(6) == 2f, "the pack mends 2 a second after the Queen");
    }
}
