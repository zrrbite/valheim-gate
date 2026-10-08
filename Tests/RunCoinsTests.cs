using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Coins for the fittings (2026-10-08). The owner: "money is difficult to come by, so maybe we could increase the coin
/// drops for trolls?" - trolls drop three times their coins during a run, and the creatures the sea sends pay.
/// </summary>
static class RunCoinsTests
{
    public static void Run()
    {
        Check.That(RunCoins.ExtraTrollCoins(3f, 25) == 50 && RunCoins.ExtraTrollCoins(3f, 20) == 40,
                   "at x3 a troll's own 20-30 coins get twice as many again beside them");
        Check.That(RunCoins.ExtraTrollCoins(1f, 25) == 0 && RunCoins.ExtraTrollCoins(0.5f, 25) == 0 && RunCoins.ExtraTrollCoins(0f, 25) == 0,
                   "x1 or less adds nothing (and never takes coins away)");
        Check.That(RunCoins.ExtraTrollCoins(2.5f, 20) == 30, "a fractional multiplier rounds");

        Check.That(RunCoins.SeaCoins(SeaCreature.Serpent, 1, 1f) == 30 && RunCoins.SeaCoins(SeaCreature.Serpent, 3, 1f) == 90 &&
                   RunCoins.SeaCoins(SeaCreature.Bonemaw, 3, 1f) == 180 && RunCoins.SeaCoins(SeaCreature.Drake, 2, 1f) == 20 &&
                   RunCoins.SeaCoins(SeaCreature.Deathsquito, 1, 1f) == 5 && RunCoins.SeaCoins(SeaCreature.Gjall, 1, 1f) == 40 &&
                   RunCoins.SeaCoins(SeaCreature.FallenValkyrie, 2, 1f) == 120,
                   "the sea pays a base per creature, times its level");
        Check.That(RunCoins.SeaCoins(SeaCreature.Serpent, 2, 2f) == 120 && RunCoins.SeaCoins(SeaCreature.Serpent, 2, 0f) == 0 &&
                   RunCoins.SeaCoins(SeaCreature.Serpent, 0, 1f) == 0 && RunCoins.SeaCoins(SeaCreature.Serpent, 2, -1f) == 0,
                   "times the config's multiplier; nothing for 0, a negative multiplier, or a level below 1");
    }
}
