using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Coins that pay for the ship's fittings (2026-10-08). The fittings, ward included, cost about 2,400 coins, and
    /// a vanilla troll drops 20-30: during a run trolls drop more, and the creatures the sea sends pay too.
    /// </summary>
    public static class RunCoins
    {
        /// <summary>A vanilla troll's coin drop, which the extra is rolled from.</summary>
        public const int TrollRollMin = 20;
        public const int TrollRollMax = 30;

        /// <summary>
        /// The coins the saga adds beside a troll's own drop: (multiplier - 1) times a roll like the troll's, so x3
        /// means three times the coins in all. Never negative.
        /// </summary>
        public static int ExtraTrollCoins(float multiplier, int vanillaRoll) =>
            multiplier <= 1f || vanillaRoll <= 0 ? 0 : (int)Math.Round((multiplier - 1f) * vanillaRoll);

        public static int SeaBase(SeaCreature creature)
        {
            switch (creature)
            {
                case SeaCreature.Bonemaw: return 60;
                case SeaCreature.Drake: return 10;
                case SeaCreature.Deathsquito: return 5;
                case SeaCreature.Gjall: return 40;
                case SeaCreature.FallenValkyrie: return 60;
                default: return 30;
            }
        }

        /// <summary>What a creature the sea sent drops: its base, times its level (1-3), times the config's multiplier.</summary>
        public static int SeaCoins(SeaCreature creature, int level, float multiplier) =>
            level < 1 || multiplier <= 0f ? 0 : (int)Math.Round(SeaBase(creature) * level * multiplier);
    }
}
