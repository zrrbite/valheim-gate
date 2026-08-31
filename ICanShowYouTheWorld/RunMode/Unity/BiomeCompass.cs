using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Points at things. Two jobs: turn a direction into a compass word, and find the nearest
    /// ground belonging to a given biome.
    ///
    /// The second exists because "Reach the Black Forest" was the one act-opening step with no
    /// help at all — every other landmark the saga sends you after either sits on the map or has
    /// a Herald-style bearing, while a biome you have never seen is just a direction you have to
    /// guess. The world generator already knows where every biome is; this asks it.
    /// </summary>
    internal static class BiomeCompass
    {
        /// <summary>
        /// A compass word for a direction. Shared with the Herald so both bearings read the same —
        /// two different vocabularies for the same eight directions would be a needless tell that
        /// they came from different code.
        /// </summary>
        public static string Compass(Vector3 delta)
        {
            // atan2(x, z) so that 0 is north (+Z) and the angle grows clockwise, which is what a
            // compass reads; the usual atan2(y, x) would put 0 at east and run anticlockwise.
            float degrees = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;

            string[] points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

            // +22.5 so each name owns the 45 degrees CENTRED on its direction rather than starting
            // at it — without it, due north would read as north-east.
            int index = (int)((degrees + 22.5f) / 45f) % points.Length;
            return points[index];
        }

        /// <summary>
        /// The same bearing rounded to four points instead of eight — a direction to walk, not a
        /// fix to navigate by.
        ///
        /// For the things the mode wants SUSPECTED rather than found. Eight points plus a distance
        /// is a map reference: it takes the player to the creature and leaves nothing to work out.
        /// Four points and no range gives them a heading and makes the rest their problem, which is
        /// the whole point of a hunt whose quarry is only a shade warmer than its neighbours.
        /// </summary>
        public static string Coarse(Vector3 delta)
        {
            float degrees = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;

            string[] points = { "north", "east", "south", "west" };

            // +45 for the same centring reason as above, at twice the arc.
            int index = (int)((degrees + 45f) / 90f) % points.Length;
            return points[index];
        }

        /// <summary>How far out to look before giving up, and how far apart the rings are.</summary>
        private const float RingSpacing = 64f;
        private const float MaxRange = 3000f;

        /// <summary>
        /// Arc length between samples on a ring. Sampling a fixed NUMBER of directions would leave
        /// kilometre-wide gaps at the outer rings while wasting samples at the inner ones; holding
        /// the arc roughly constant instead keeps the mesh honest at every radius.
        /// </summary>
        private const float SampleArc = 60f;
        private const int MinSamples = 12;
        private const int MaxSamples = 72;

        /// <summary>
        /// The nearest point of <paramref name="biome"/>, or null if none within
        /// <see cref="MaxRange"/>.
        ///
        /// Rings outward, nearest first, and stops at the first hit — so the answer is the closest
        /// edge of the biome rather than its middle, which is what someone walking there wants.
        /// Costs a couple of thousand noise lookups, so callers must cache; see RunService.
        /// </summary>
        public static Vector3? Nearest(Vector3 from, Heightmap.Biome biome)
        {
            var world = WorldGenerator.instance;
            if (world == null) return null;

            for (float radius = RingSpacing; radius <= MaxRange; radius += RingSpacing)
            {
                int samples = Mathf.Clamp(
                    Mathf.CeilToInt(2f * Mathf.PI * radius / SampleArc), MinSamples, MaxSamples);

                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    var probe = new Vector3(
                        from.x + Mathf.Sin(angle) * radius,
                        0f,
                        from.z + Mathf.Cos(angle) * radius);

                    if (world.GetBiome(probe) == biome) return probe;
                }
            }

            return null;
        }

        /// <summary>Valheim's sea level. Fixed at 30 in world units; everything below it is water.</summary>
        public const float WaterLevel = 30f;

        /// <summary>
        /// Picks a point on DRY LAND at roughly the given range, or null if it cannot find one.
        ///
        /// Both of this mode's named quarries used to pick a random bearing and a random distance
        /// and simply trust it. That put the pale light in the sea — "I had to go out to trigger
        /// it" — and could equally have put the Herald there. A spawn point nobody can stand on is
        /// not a destination.
        ///
        /// The returned point carries its own ground height, so callers place things ON the
        /// terrain rather than at whatever altitude the player happened to be.
        /// </summary>
        public static Vector3? LandNear(Vector3 from, float minRange, float maxRange, System.Random rng, int tries = 24)
        {
            var world = WorldGenerator.instance;
            if (world == null || rng == null) return null;

            for (int i = 0; i < tries; i++)
            {
                float angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                float range = minRange + (float)(rng.NextDouble() * (maxRange - minRange));

                var probe = new Vector3(
                    from.x + Mathf.Sin(angle) * range,
                    0f,
                    from.z + Mathf.Cos(angle) * range);

                try
                {
                    if (world.GetBiome(probe) == Heightmap.Biome.Ocean) continue;

                    // Biome alone is not enough: lakes and inlets sit inside every land biome, and
                    // the height is what actually says whether you could stand there.
                    float ground = world.GetHeight(probe.x, probe.z);
                    if (ground <= WaterLevel + 1f) continue;

                    probe.y = ground;
                    return probe;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// What to call a biome in a sentence. Valheim's enum names are compressed
        /// ("BlackForest", "AshLands") and the saga's voice is not.
        /// </summary>
        public static string FriendlyName(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows:     return "The meadows";
                case Heightmap.Biome.BlackForest: return "The Black Forest";
                case Heightmap.Biome.Swamp:       return "The swamps";
                case Heightmap.Biome.Mountain:    return "The mountains";
                case Heightmap.Biome.Plains:      return "The plains";
                case Heightmap.Biome.Ocean:       return "Open water";
                case Heightmap.Biome.Mistlands:   return "The Mistlands";
                case Heightmap.Biome.AshLands:    return "The Ashlands";
                case Heightmap.Biome.DeepNorth:   return "The Deep North";
                default:                          return biome.ToString();
            }
        }
    }
}
