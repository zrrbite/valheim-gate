using System;
using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The ways' numbers and rules (2026-10-08, class balance; docs/superpowers/specs/2026-10-08-class-balance-design.md).
    /// Pure, so every number is tested; BoonEffects applies them.
    /// </summary>
    public static class WayRules
    {
        /// <summary>Stacked weapon bonuses stop here (spec, "The damage ceiling"): x4.5 was reachable.</summary>
        public const float WeaponCeiling = 2.5f;

        /// <summary>The product of the live weapon factors, capped at <see cref="WeaponCeiling"/>.</summary>
        public static float WeaponProduct(IEnumerable<float> factors)
        {
            float p = 1f;
            if (factors != null)
                foreach (var f in factors) p *= f;
            return Math.Min(WeaponCeiling, p);
        }

        /// <summary>Gods felled for each tempering: Bonemass, Moder, Yagluth, the Queen (spec, "Tempering").</summary>
        public static readonly int[] TemperGods = { 3, 4, 5, 6 };

        public static bool Tempered(int gods, TemperSlot slot) => gods >= TemperGods[(int)slot];

        /// <summary>Shown once its god is the next to fall: naming a later act's god would spoil it.</summary>
        public static bool TemperShown(int gods, TemperSlot slot) => gods >= TemperGods[(int)slot] - 1;

        private static readonly Dictionary<string, string[]> TemperLines = new Dictionary<string, string[]>
        {
            { "hunter",    new[] { "Packbrother calls three", "Thor's bow's element strikes half again as hard",
                                   "Unseen lasts 30 s", "your pack mends 2 a second" } },
            { "volva",     new[] { "Bonecaller raises three", "Mending every 60 s", "Thor's Wrath reaches 9 m",
                                   "Hearthlight mends up to 12 a second" } },
            { "berserker", new[] { "Rend reaches half again as far", "Blood Rage holds 25 s", "Warcry reaches 12 m",
                                   "Fury to +75% (fifteen hits)" } },
            { "huskarl",   new[] { "Shield Bash reaches 6 m", "Shield Wall stands 30 s", "Last Stand lasts 10 s",
                                   "Guard mends double" } },
            { "skald",     new[] { "the Marching Song: +30%", "the War Song: +35%", "Bragi's saga mends up to 9 a second",
                                   "two songs at once" } },
            { "saefari",   new[] { "Undertow reaches 9 m", "Stormcaller lasts 30 s", "Sea Legs lasts 10 min",
                                   "your Ward on land is as strong as at sea" } },
            { "smidr",     new[] { "two watch-posts at once", "the Field Forge stands 3 min", "the Master's Minute lasts 2 min",
                                   "Forge-skin: armour doubled" } },
        };

        /// <summary>What a tempering does, for the HUD's BOONS page. Empty for an unknown way.</summary>
        public static string TemperLine(string classId, TemperSlot slot) =>
            classId != null && TemperLines.TryGetValue(classId, out var lines) ? lines[(int)slot] : "";

        /// <summary>
        /// Menagerie's beasts by biome (spec, "The Hunter"): it rolled from all six from Act II, so a Hunter rerolled every
        /// 90 s until a Lox (1000 health) came.
        /// </summary>
        public static IList<string> MenagerieBeasts(int gods)
        {
            var beasts = new List<string> { "Boar", "Hen", "Chicken" };
            if (gods >= 3) beasts.Add("Wolf");
            if (gods >= 4) beasts.Add("Lox");
            if (gods >= 6) beasts.Add("Asksvin");
            return beasts;
        }

        // --- The Hunter ---
        public static int PackSize(int gods) => Tempered(gods, TemperSlot.Rung1) ? 3 : 2;
        public static float BowElementScale(int gods) => Tempered(gods, TemperSlot.Rung2) ? 1.5f : 1f;
        public static float UnseenSeconds(int gods) => Tempered(gods, TemperSlot.Rung3) ? 30f : 20f;
        public static float PackRegenPerSecond(int gods) => Tempered(gods, TemperSlot.Engine) ? 2f : 0f;

        // --- The Völva ---
        /// <summary>3 a second, +1 per god, to 8; the Queen's tempering lifts it to 12 at once, so "Hearthlight mends up to
        /// 12 a second" is true the moment its line turns green (3 + gods reached only 9 at the Queen).</summary>
        public static float HearthlightPerSecond(int gods) =>
            Tempered(gods, TemperSlot.Engine) ? 12f : Math.Min(8f, 3f + Math.Max(0, gods));
        public static int BoneCount(int gods) => Tempered(gods, TemperSlot.Rung1) ? 3 : 2;
        public static float MendingCooldown(int gods) => Tempered(gods, TemperSlot.Rung2) ? 60f : 90f;
        public static float WrathRadius(int gods) => Tempered(gods, TemperSlot.Rung3) ? 9f : 6f;

        // --- The Berserker ---
        public const float BloodiedShare = 0.1f;
        public static int FuryMax(int gods) => Tempered(gods, TemperSlot.Engine) ? 15 : 10;
        public static float RageSeconds(int gods) => Tempered(gods, TemperSlot.Rung2) ? 25f : 15f;
        public static float RendRadius(int gods) => Tempered(gods, TemperSlot.Rung1) ? 7.5f : 5f;
        public static float WarcryRadius(int gods) => Tempered(gods, TemperSlot.Rung3) ? 12f : 8f;

        // --- The Húskarl ---
        public const float GuardStaminaRefund = 0.5f;
        private static float GuardFactor(int gods, bool wall) => (wall ? 2f : 1f) * (Tempered(gods, TemperSlot.Engine) ? 2f : 1f);
        public static float GuardBlockHeal(int gods, bool wall) => 4f * GuardFactor(gods, wall);
        public static float GuardParryHeal(int gods, bool wall) => 10f * GuardFactor(gods, wall);
        public static float BashRadius(int gods) => Tempered(gods, TemperSlot.Rung1) ? 6f : 4f;
        public static float WallSeconds(int gods) => Tempered(gods, TemperSlot.Rung2) ? 30f : 20f;
        public static float LastStandSeconds(int gods) => Tempered(gods, TemperSlot.Rung3) ? 10f : 6f;

        // --- The Skald ---
        public const float CrescendoSeconds = 5f;
        public const float CrescendoEvery = 20f;
        public static float MarchSpeed(int gods) => Tempered(gods, TemperSlot.Rung1) ? 0.3f : 0.2f;
        public static float WarDamage(int gods) => Tempered(gods, TemperSlot.Rung2) ? 1.35f : 1.25f;
        /// <summary>3 a second, +1 per god, to 6; Yagluth's tempering lifts it to 9 at once, so "Bragi's saga mends up to
        /// 9 a second" is true the moment its line turns green (3 + gods was 8 at Yagluth).</summary>
        public static float BragiPerSecond(int gods) =>
            Tempered(gods, TemperSlot.Rung3) ? 9f : Math.Min(6f, 3f + Math.Max(0, gods));
        public static bool CrescendoDue(float now, float last) => now - last >= CrescendoEvery;
        public static int SongsAtOnce(int gods) => Tempered(gods, TemperSlot.Engine) ? 2 : 1;
    }

    /// <summary>What a god tempers: a way's three rungs, then its engine.</summary>
    public enum TemperSlot { Rung1 = 0, Rung2 = 1, Rung3 = 2, Engine = 3 }

    /// <summary>The Skald's standing songs, one per rung.</summary>
    public enum SkaldSong { None, March, War, Bragi }
}
