using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Wind for the voyage, as rules: the god's wind, and the Wind-horn's clock.
    /// </summary>
    /// <remarks>
    /// The owner's aim (2026-10-05): travelling by boat to a god in another biome should be easy, and
    /// "maybe consider that Moder power that gives us wind. Be able to cast that as a skill?"
    ///
    /// Both use the GAME's own Moder wind - a status effect with the SailingPower attribute, which
    /// Ship.IsWindControllActive reads for everyone aboard and EnvMan.UpdateWind turns into a wind at
    /// the ship's back. So the sail, the visuals and the physics are the game's; nothing is faked.
    ///
    /// THE GOD'S WIND is free and passive: aboard, with the act's god pinned on the map, the prow
    /// within <see cref="GodWindToleranceDegrees"/> of the altar. It only helps the trip that
    /// matters, and only ever toward a place the map already shows - so it can never spoil an altar.
    ///
    /// THE WIND-HORN is bought at the helm (ShipFittings) and blown with the up arrow at sea: two
    /// minutes of wind at your back, any heading, ten minutes to recover. After Moder falls the
    /// game's own power does the same for five minutes; the horn stays a second, shorter one.
    /// </remarks>
    public static class SagaWind
    {
        public const float GodWindToleranceDegrees = 40f;
        public const float HornSeconds = 120f;
        public const float HornCooldownSeconds = 600f;

        /// <summary>Compass heading of a ground-plane direction, 0-360: 0 along +z, 90 along +x.</summary>
        public static float Heading(float dx, float dz)
        {
            double deg = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (deg < 0) deg += 360.0;
            return (float)deg;
        }

        /// <summary>The smaller angle between two headings, 0-180.</summary>
        public static float Between(float a, float b)
        {
            float d = ((a - b) % 360f + 360f) % 360f;
            return d > 180f ? 360f - d : d;
        }

        /// <summary>Whether the god's wind blows for a ship: aboard, the god pinned, the prow toward the altar.</summary>
        public static bool GodWindBlows(bool aboard, bool godPinned, float shipHeading, float bearingToAltar) =>
            aboard && godPinned && Between(shipHeading, bearingToAltar) <= GodWindToleranceDegrees;

        /// <summary>Seconds until the horn can be blown again; 0 when ready (or never blown).</summary>
        public static float HornCooldownLeft(float now, float lastBlownAt) =>
            Math.Max(0f, lastBlownAt + HornCooldownSeconds - now);
    }
}
