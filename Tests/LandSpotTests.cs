using System;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The Gatherer comes over land (2026-10-08): it was sent 28 m from a player at sea, into the water, and swam
/// after them into the deep ocean (owner: "maybe not ideal").
/// </summary>
static class LandSpotTests
{
    const float Water = 30f;

    public static void Run()
    {
        float x, z;
        Check.That(LandSpot.Find((px, pz) => 40f, Water, 0f, 0f, 28f, 0.0, out x, out z) &&
                   Math.Abs(x - 28f) < 0.01f && Math.Abs(z) < 0.01f,
                   "all land: the first bearing, at the given distance");

        // The sea to the east (x > 10), land to the west: whichever bearing it starts on, it lands west.
        Func<float, float, float> coast = (px, pz) => px > 10f ? 0f : 40f;
        Check.That(LandSpot.Find(coast, Water, 0f, 0f, 28f, 0.0, out x, out z) && x <= 10f &&
                   Math.Abs(Math.Sqrt(x * x + z * z) - 28.0) < 0.01,
                   "a bearing over the sea is passed over for one over land, still 28 m out");

        Check.That(!LandSpot.Find((px, pz) => 0f, Water, 0f, 0f, 28f, 1.0, out x, out z),
                   "at sea, no land within reach: no spot, so it waits");
        Check.That(!LandSpot.Find((px, pz) => Water + 0.2f, Water, 0f, 0f, 28f, 1.0, out x, out z),
                   "a shoal barely above the water is not land");

        Check.That(LandSpot.OpenWater(28f, Water) && !LandSpot.OpenWater(29.5f, Water) && !LandSpot.OpenWater(40f, Water),
                   "open water: the ground a metre or more under the surface, as the sea danger reads it");
    }
}
