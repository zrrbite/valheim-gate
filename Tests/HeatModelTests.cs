using ICanShowYouTheWorld.RunMode;

static class HeatModelTests
{
    public static void Run()
    {
        var h = new HeatModel();
        Check.That(h.Heat == 0f, "heat starts at 0");
        h.Add(3f);
        Check.That(h.Heat == 3f, "add raises heat");
        h.Remove(5f);
        Check.That(h.Heat == 0f, "heat floors at 0");
        h.Add(2.5f);
        h.Remove(1f);
        Check.That(h.Heat == 1.5f, "partial removal");
        h.Add(-10f);
        Check.That(h.Heat == 0f, "add of negative floors at 0");

        // score = (par/actual) * (1 + heat * weight)
        float s = RunScore.Compute(7200f, 3600f, 10f, 0.1f);
        Check.That(System.Math.Abs(s - 4f) < 0.001f, "score: 2x speed, 10 heat @0.1 => 4.0");
        Check.That(RunScore.Compute(7200f, 0f, 0f, 0.1f) == 0f, "zero elapsed yields 0, not Inf");
        Check.That(RunScore.Compute(7200f, -5f, 0f, 0.1f) == 0f, "negative elapsed yields 0");

        Check.That(HeatEffects.EnemyDamageMultiplier(10f, 0.05f) == 1.5f, "enemy damage mult");
        Check.That(HeatEffects.EnemyLevelUpMultiplier(0f, 0.1f) == 1f, "no heat => 1x levelup");
    }
}
