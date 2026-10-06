using System;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// MACBOOK-TEMP (2026-10-06): the dev ship key finds the nearest deep sea, so a tester with no mouse
/// can test the ship fittings and the winds without building a ship or walking to a coast ("it would
/// be nice if i could be teleported to water to spawn said ship").
/// </summary>
static class DevSeaTests
{
    const float Water = 30f;

    public static void Run()
    {
        // Land west of x = 100, a deep sea east of it.
        Func<float, float, float> coast = (x, z) => x > 100f ? 0f : 40f;
        var berth = DevSea.Find(coast, Water, 0f, 0f, 4000f);
        Check.That(berth.HasValue && berth.Value.X > 106f && berth.Value.X < 112f && Math.Abs(berth.Value.Z) < 1f,
                   "the nearest berth is the first ring where a whole hull fits in deep water");
        Check.That(berth.HasValue && berth.Value.DirX > 0.99f,
                   "the prow points away from where you stood, out to sea");

        Func<float, float, float> atSea = (x, z) => 0f;
        var here = DevSea.Find(atSea, Water, 500f, 500f, 4000f);
        Check.That(here.HasValue && Dist(here.Value, 500f, 500f) <= 12f,
                   "standing at sea, the ship comes alongside");

        Check.That(!DevSea.Find((x, z) => 40f, Water, 0f, 0f, 4000f).HasValue,
                   "no sea within reach is no berth");
        Check.That(!DevSea.Find((x, z) => x > 100f ? 29f : 40f, Water, 0f, 0f, 4000f).HasValue,
                   "water a metre deep is not a berth: the hull would sit on the bottom");

        // A sea that begins only past the edge of the world, where Valheim pulls ships under.
        Check.That(!DevSea.Find((x, z) => x > 10050f ? 0f : 40f, Water, 9900f, 0f, 4000f).HasValue,
                   "nothing past the edge of the world");

        // A pond: deep at its middle, too small around it for a hull.
        Func<float, float, float> pond = (x, z) => (x - 50f) * (x - 50f) + z * z < 4f ? 0f : 40f;
        Check.That(!DevSea.Find(pond, Water, 0f, 0f, 4000f).HasValue,
                   "a hole that a Karve does not fit in is not a berth");
    }

    static float Dist(DevSea.Berth b, float x, float z) =>
        (float)Math.Sqrt((b.X - x) * (b.X - x) + (b.Z - z) * (b.Z - z));
}
