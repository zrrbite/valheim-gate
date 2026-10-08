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
    }
}
