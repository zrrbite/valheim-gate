using System;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The ways' numbers (2026-10-08, class balance): docs/superpowers/specs/2026-10-08-class-balance-design.md.
/// </summary>
static class WayRulesTests
{
    public static void Run()
    {
        // The damage ceiling.
        Check.That(WayRules.WeaponProduct(new float[0]) == 1f, "no weapon bonus: x1");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 1.2f, 1.5f }) - 1.8f) < 0.001f, "bonuses multiply below the ceiling");
        Check.That(WayRules.WeaponProduct(new[] { 1.2f, 1.4f, 1.2f, 1.5f, 1.5f }) == 2.5f,
                   "Sharpened, Glass Cannon, Stoker, Reckless and Fury together stop at x2.5, not x4.5");
        Check.That(Math.Abs(WayRules.WeaponProduct(new[] { 0.8f }) - 0.8f) < 0.001f, "a factor below one still applies");
        Check.That(WayRules.WeaponProduct(null) == 1f, "nothing at all is x1, not an error");
    }
}
