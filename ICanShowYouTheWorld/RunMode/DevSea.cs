using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// MACBOOK-TEMP (2026-10-06): where the dev ship key puts its ship, to be removed with the rest of
    /// the MacBook test keys. Testing the fittings and the winds wants a ship on open water, and on a
    /// MacBook with no mouse neither building one nor walking to a coast is practical (the owner: "it
    /// would be nice if i could be teleported to water to spawn said ship").
    ///
    /// Pure: the game side passes the world generator's ground height and the sea level.
    /// </summary>
    public static class DevSea
    {
        /// <summary>Water under the ship's own spot.</summary>
        public const float ShipDepth = 3f;

        /// <summary>Water under its bow, stern and both sides: a Karve is about 12 m long and 5 wide.</summary>
        public const float HullDepth = 1.5f;
        public const float HalfLength = 6f;
        public const float HalfBeam = 3f;

        /// <summary>Valheim's playable radius; past it the edge of the world pulls ships under.</summary>
        public const float WorldRadius = 10000f;

        /// <summary>Where the ship goes, and the way its prow points: out, away from where you stood.</summary>
        public struct Berth
        {
            public float X, Z, DirX, DirZ;
        }

        /// <summary>
        /// The nearest place a whole hull floats in deep water, searched ring by ring outward from
        /// (<paramref name="x"/>, <paramref name="z"/>). Close rings are fine-grained, because that is
        /// where a coast is usually found; far ones coarser, because they only have to find a sea.
        /// Null when there is none within <paramref name="maxRadius"/>.
        /// </summary>
        public static Berth? Find(Func<float, float, float> groundAt, float waterLevel, float x, float z, float maxRadius)
        {
            for (float r = 10f; r <= maxRadius; r += r < 200f ? 4f : 25f)
            {
                int angles = r < 200f ? 32 : 64;
                for (int i = 0; i < angles; i++)
                {
                    double a = 2.0 * Math.PI * i / angles;
                    float dx = (float)Math.Cos(a), dz = (float)Math.Sin(a);
                    float px = x + dx * r, pz = z + dz * r;

                    if (px * px + pz * pz > WorldRadius * WorldRadius) continue;
                    if (groundAt(px, pz) > waterLevel - ShipDepth) continue;
                    if (!Floats(groundAt, waterLevel, px, pz, dx, dz)) continue;

                    return new Berth { X = px, Z = pz, DirX = dx, DirZ = dz };
                }
            }

            return null;
        }

        private static bool Floats(Func<float, float, float> groundAt, float waterLevel, float px, float pz, float dx, float dz)
        {
            float deepest = waterLevel - HullDepth;
            return groundAt(px + dx * HalfLength, pz + dz * HalfLength) <= deepest &&
                   groundAt(px - dx * HalfLength, pz - dz * HalfLength) <= deepest &&
                   groundAt(px - dz * HalfBeam, pz + dx * HalfBeam) <= deepest &&
                   groundAt(px + dz * HalfBeam, pz - dx * HalfBeam) <= deepest;
        }
    }
}
