using System;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>Heat: raised by challenges, drained by deaths/rerolls, floored at 0.</summary>
    public class HeatModel
    {
        public float Heat { get; private set; }
        public void Add(float amount) => Heat = Math.Max(0f, Heat + amount);
        public void Remove(float amount) => Heat = Math.Max(0f, Heat - amount);
    }

    public static class RunScore
    {
        /// <summary>
        /// Score out of 100 at par pace, scaled up by heat.
        ///
        /// The shape is <c>par / (par + actual)</c>, not the <c>par / actual</c> hyperbola this
        /// used to be. That one was unusable as a LIVE readout: it is undefined at t=0 and falls
        /// off a cliff through the opening minutes (at 1% of par it reads 100x par pace, at 10%
        /// still 10x), so the number a player watched spent the whole early run collapsing and
        /// told them nothing. This curve starts at its maximum, decays smoothly and monotonically,
        /// and passes through exactly half of that maximum at t = par — a plain, readable pace
        /// gauge rather than a spike.
        ///
        /// A negative elapsed time is clamped to 0 rather than zeroing the score: t=0 is now a
        /// legitimate reading (a run that has not started yet is, trivially, at par pace), so the
        /// only thing left to defend against is a nonsense input.
        /// </summary>
        public static float Compute(float parSeconds, float actualSeconds, float heat, float heatScoreWeight)
        {
            if (actualSeconds < 0f) actualSeconds = 0f;

            // Guards the degenerate par<=0 config (and, with it, 0/0): no par means no pace to
            // measure against, so there is no score to report.
            float denominator = parSeconds + actualSeconds;
            if (parSeconds <= 0f || denominator <= 0f) return 0f;

            return 100f * (parSeconds / denominator) * (1f + heat * heatScoreWeight);
        }
    }

    public static class HeatEffects
    {
        public static float EnemyDamageMultiplier(float heat, float weight) => 1f + heat * weight;
        public static float EnemyLevelUpMultiplier(float heat, float weight) => 1f + heat * weight;
    }
}
