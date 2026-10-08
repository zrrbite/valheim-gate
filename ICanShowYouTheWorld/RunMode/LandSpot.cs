using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Somewhere dry at a given distance (2026-10-08). The Gatherer was sent 28 m from a player at sea, into the
    /// water, and swam after them into the deep ocean. Pure, so the choice is tested; the game's ground comes in as
    /// a function (WorldGenerator.GetHeight), the way DevSea takes it.
    /// </summary>
    public static class LandSpot
    {
        /// <summary>Ground at least this far above the water counts as land: a beach, not a shoal.</summary>
        public const float Margin = 0.5f;

        /// <summary>How many evenly spaced bearings are tried, all the way round.</summary>
        public const int Tries = 12;

        /// <summary>
        /// The first of <see cref="Tries"/> evenly spaced bearings, from <paramref name="startAngle"/> (radians),
        /// whose point <paramref name="radius"/> from (x, z) is land. False when none is: the caller waits.
        /// </summary>
        public static bool Find(Func<float, float, float> groundAt, float waterLevel, float x, float z, float radius,
                                double startAngle, out float spotX, out float spotZ)
        {
            for (int i = 0; i < Tries; i++)
            {
                double a = startAngle + i * 2.0 * Math.PI / Tries;
                float px = x + (float)Math.Cos(a) * radius;
                float pz = z + (float)Math.Sin(a) * radius;
                if (groundAt(px, pz) < waterLevel + Margin) continue;
                spotX = px;
                spotZ = pz;
                return true;
            }
            spotX = x;
            spotZ = z;
            return false;
        }

        /// <summary>Over open water: the ground a metre or more under the surface, the sea danger's own test.</summary>
        public static bool OpenWater(float ground, float waterLevel) => ground <= waterLevel - 1f;
    }
}
