using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The star rule for the player's animals (2026-10-08, class balance). A star is the game's own: Character.SetLevel
    /// makes health base x level and Attack adds +50% damage per level above one, so level 1 is no star and 3 is two.
    /// Pure, so the numbers are tested; BoonEffects applies them.
    /// </summary>
    public static class TameStars
    {
        /// <summary>Two stars: the game's own ceiling (its HUD draws no third).</summary>
        public const int MaxLevel = 3;

        /// <summary>What the old Shepherd (the GM mod's pet buff) set every tame's health to.</summary>
        public const float LegacyBlessingHealth = 5000f;

        /// <summary>A summon's level: no star in the Meadows, one per god felled, two at most.</summary>
        public static int SummonLevel(int gods) => Math.Max(1, Math.Min(MaxLevel, 1 + gods));

        /// <summary>The Hunter's Shepherd: one star more, inside the same two.</summary>
        public static int WithShepherd(int level) => Math.Min(MaxLevel, Math.Max(1, level) + 1);

        /// <summary>A tame still carrying the old Shepherd's health, saved into the world by runs before 2026-10-08.</summary>
        public static bool IsLegacyBlessing(float maxHealth) => maxHealth >= LegacyBlessingHealth - 1f;
    }
}
