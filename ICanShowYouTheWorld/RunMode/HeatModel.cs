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
        public static float Compute(float parSeconds, float actualSeconds, float heat, float heatScoreWeight)
        {
            if (actualSeconds <= 0f) return 0f;
            return (parSeconds / actualSeconds) * (1f + heat * heatScoreWeight);
        }
    }

    public static class HeatEffects
    {
        public static float EnemyDamageMultiplier(float heat, float weight) => 1f + heat * weight;
        public static float EnemyLevelUpMultiplier(float heat, float weight) => 1f + heat * weight;
    }
}
