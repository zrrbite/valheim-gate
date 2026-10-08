using System;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Sea danger's rules (2026-10-08, docs/superpowers/specs/2026-10-08-sea-danger-design.md): the heat dial, the
/// voyage clock, and what comes.
/// </summary>
static class SeaDangerTests
{
    static bool Near(double a, double b) => Math.Abs(a - b) < 1e-4;

    static Func<double> Dice(params double[] rolls)
    {
        int i = 0;
        return () => rolls[Math.Min(i++, rolls.Length - 1)];
    }

    public static void Run()
    {
        // The dial.
        Check.That(SeaDanger.Dial(0f, 40f) == 0f && SeaDanger.Dial(20f, 40f) == 0.5f && SeaDanger.Dial(80f, 40f) == 1f &&
                   SeaDanger.Dial(10f, 0f) == 0f && SeaDanger.Dial(-5f, 40f) == 0f,
                   "the dial is heat over full heat, clamped to 0..1, and 0 for a nonsense full heat");
        Check.That(Near(SeaDanger.GapMinutes(1f, 0.5f), 2) && Near(SeaDanger.GapMinutes(0.5f, 0.5f), 4) &&
                   Near(SeaDanger.GapMinutes(0.25f, 0.5f), 8) && double.IsPositiveInfinity(SeaDanger.GapMinutes(0f, 0.5f)),
                   "the average gap, cooldown included: 2, 4 and 8 minutes at full, half and a quarter heat; never at 0");
        Check.That(SeaDanger.ChancePerRoll(0f, 0.5f) == 0.0 && SeaDanger.ChancePerRoll(0.5f, 0f) == 0.0,
                   "heat 0 (or no peak) never rolls an encounter");
        Check.That(Near(SeaDanger.ChancePerRoll(1f, 0.5f), 1 - Math.Exp(-(1.0 / 6) / 0.5)) &&
                   Near(SeaDanger.ChancePerRoll(0.5f, 0.5f), 1 - Math.Exp(-(1.0 / 6) / 2.5)),
                   "each 10 s roll waits out the cooldown, then delivers the gap: 0.5 min left at full heat, 2.5 at half");
        Check.That(SeaDanger.ChancePerRoll(0.25f, 0.5f) < SeaDanger.ChancePerRoll(0.5f, 0.5f) &&
                   SeaDanger.ChancePerRoll(0.5f, 0.5f) < SeaDanger.ChancePerRoll(1f, 0.5f),
                   "more heat, likelier");
        Check.That(Near(SeaDanger.ChancePerRoll(1f, 10f), 1 - Math.Exp(-1)),
                   "a gap shorter than the cooldown rolls at most one per roll-time, not faster");

        // Strength.
        Check.That(SeaDanger.Level(0f) == 1 && SeaDanger.Level(0.32f) == 1 && SeaDanger.Level(1f / 3f) == 2 &&
                   SeaDanger.Level(0.66f) == 2 && SeaDanger.Level(2f / 3f) == 3 && SeaDanger.Level(1f) == 3,
                   "no star below a third, one star below two thirds, two stars above");
        Check.That(SeaDanger.Count(SeaCreature.Drake, 0.5f) == 1 && SeaDanger.Count(SeaCreature.Drake, 0.7f) == 2 &&
                   SeaDanger.Count(SeaCreature.Deathsquito, 0.5f) == 2 && SeaDanger.Count(SeaCreature.Deathsquito, 0.7f) == 3 &&
                   SeaDanger.Count(SeaCreature.Serpent, 1f) == 1 && SeaDanger.Count(SeaCreature.Gjall, 1f) == 1,
                   "drakes 1 (2 at high heat), deathsquitos 2 (3), the rest come alone");

        // What comes.
        var plain = SeaDanger.Choose(new SeaCoast[0], false, 1, 0.5f, Dice(0.0));
        Check.That(plain.Creature == SeaCreature.Serpent && plain.Prefab == "Serpent" && !plain.Flies && plain.From == null &&
                   plain.Level == 2 && plain.Count == 1 && plain.Message == "Something rises off the bow.",
                   "open sea in Act II: a Serpent, starred by heat");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mountain }, false, 2, 0.5f, Dice(0.0)).Creature == SeaCreature.Serpent,
                   "a mountain coast before Act IV sends a Serpent - no spoilers");
        var drake = SeaDanger.Choose(new[] { SeaCoast.Mountain }, false, 3, 0.7f, Dice(0.1, 0.0));
        Check.That(drake.Creature == SeaCreature.Drake && drake.Prefab == "Hatchling" && drake.Flies &&
                   drake.From == SeaCoast.Mountain && drake.Count == 2 && drake.Message == "Drakes come down off the mountains.",
                   "a reached mountain coast sends drakes two times in three");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mountain }, false, 3, 0.7f, Dice(0.9)).Creature == SeaCreature.Serpent,
                   "and a Serpent the third time");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mountain, SeaCoast.Plains }, false, 4, 0.5f, Dice(0.1, 0.99)).Creature == SeaCreature.Deathsquito &&
                   SeaDanger.Choose(new[] { SeaCoast.Plains, SeaCoast.Mountain }, false, 4, 0.5f, Dice(0.1, 0.0)).Creature == SeaCreature.Drake,
                   "several reached coasts: one at random, in a fixed order whatever order they were found in");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Mistlands, SeaCoast.Ashlands }, false, 5, 0.5f, Dice(0.1, 0.99)).Creature == SeaCreature.Gjall,
                   "an unreached coast is no candidate (the Ashlands in Act VI)");
        Check.That(SeaDanger.Choose(new SeaCoast[0], true, 6, 0.5f, Dice(0.0)).Creature == SeaCreature.Bonemaw &&
                   SeaDanger.Choose(new SeaCoast[0], true, 5, 0.5f, Dice(0.0)).Creature == SeaCreature.Serpent,
                   "the boiling sea gives up a Bonemaw - from Act VII only");
        Check.That(SeaDanger.Choose(new[] { SeaCoast.Ashlands }, true, 6, 0.5f, Dice(0.1, 0.0)).Creature == SeaCreature.FallenValkyrie,
                   "the ash coast sends fallen valkyries");
        Check.That(SeaDanger.All.Count() == 6 && SeaDanger.All.All(c => !string.IsNullOrEmpty(SeaDanger.PrefabOf(c)) &&
                   !string.IsNullOrEmpty(SeaDanger.MessageOf(c))),
                   "all six creatures have a prefab and a message");

        // The voyage clock.
        var v = new SeaVoyage();
        Check.That(v.Tick(0f, true) == SeaTurn.None && v.OnVoyage && v.Tick(59f, true) == SeaTurn.None,
                   "a voyage starts quiet: no roll in its first minute");
        Check.That(v.Tick(60f, true) == SeaTurn.Roll && v.Tick(61f, true) == SeaTurn.None && v.Tick(70f, true) == SeaTurn.Roll,
                   "then a roll every ten seconds");
        v.Arrived(70f);
        Check.That(v.Live == 1 && v.Tick(80f, true) == SeaTurn.None && v.Tick(159f, true) == SeaTurn.None &&
                   v.Tick(160f, true) == SeaTurn.Roll,
                   "an encounter starts a ninety-second cooldown");
        v.Arrived(160f);
        Check.That(v.Live == 2 && v.Tick(400f, true) == SeaTurn.None, "two live encounters: nothing more comes");
        v.Ended();
        Check.That(v.Live == 1 && v.Tick(401f, true) == SeaTurn.Roll, "one ends, and the rolls resume");
        v.Ended(); v.Ended();
        Check.That(v.Live == 0, "the live count never goes below zero");
        v.HornBlown();
        Check.That(v.Tick(411f, true) == SeaTurn.Certain && v.Tick(421f, true) == SeaTurn.Roll,
                   "the horn makes the next roll certain, once");
        // Last at sea at 421: under two minutes later (540) the voyage still holds.
        Check.That(v.Tick(430f, false) == SeaTurn.None && v.OnVoyage && v.Tick(540f, false) == SeaTurn.None && v.OnVoyage,
                   "off the ship for under two minutes: the voyage goes on");
        Check.That(v.Tick(541f, true) == SeaTurn.Roll, "and back at sea it rolls at once, not quiet");
        v.HornBlown();
        // Last at sea at 541: two minutes later (661) the voyage is over.
        Check.That(v.Tick(600f, false) == SeaTurn.None && v.OnVoyage && v.Tick(661f, false) == SeaTurn.None && !v.OnVoyage,
                   "two minutes off the sea end the voyage");
        Check.That(v.Tick(670f, true) == SeaTurn.None && v.Tick(729f, true) == SeaTurn.None && v.Tick(730f, true) == SeaTurn.Roll,
                   "the next voyage is quiet again for a minute, and the old horn was forgotten");
        var w = new SeaVoyage();
        w.HornBlown();
        Check.That(w.Tick(0f, true) == SeaTurn.None && w.Tick(30f, true) == SeaTurn.None && w.Tick(60f, true) == SeaTurn.Certain,
                   "a horn blown before the quiet minute is over waits for it");
    }
}
