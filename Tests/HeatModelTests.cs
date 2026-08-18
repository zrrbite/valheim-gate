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

        ScoreCurveTests();

        Check.That(HeatEffects.EnemyDamageMultiplier(10f, 0.05f) == 1.5f, "enemy damage mult");
        Check.That(HeatEffects.EnemyLevelUpMultiplier(0f, 0.1f) == 1f, "no heat => 1x levelup");
    }

    /// <summary>
    /// score = 100 * (par / (par + actual)) * (1 + heat * weight).
    /// A smooth decay from a finite maximum, not the old par/actual hyperbola that was undefined
    /// at t=0 and cratered through the opening minutes.
    /// </summary>
    static void ScoreCurveTests()
    {
        const float Par = 7200f;

        // Half of par elapsed: 7200/(7200+3600) = 2/3; heat 10 @0.1 doubles it.
        float s = RunScore.Compute(Par, 3600f, 10f, 0.1f);
        Check.That(System.Math.Abs(s - 133.3333f) < 0.01f, "score: half par elapsed, 10 heat @0.1 => 133.33");

        // t=0 is a legitimate reading now, and it is the curve's finite maximum.
        float atZero = RunScore.Compute(Par, 0f, 0f, 0.1f);
        Check.That(System.Math.Abs(atZero - 100f) < 0.001f, "score at t=0 is 100 with no heat");
        Check.That(!float.IsNaN(atZero) && !float.IsInfinity(atZero), "score at t=0 is finite");
        Check.That(System.Math.Abs(RunScore.Compute(Par, 0f, 10f, 0.1f) - 200f) < 0.001f,
            "score at t=0 still takes the heat multiplier");

        // Halves at t = par, relative to t = 0, for the same heat.
        float atPar = RunScore.Compute(Par, Par, 4f, 0.1f);
        float atZeroSameHeat = RunScore.Compute(Par, 0f, 4f, 0.1f);
        Check.That(System.Math.Abs(atPar - atZeroSameHeat / 2f) < 0.001f,
            "score at t=par is exactly half the score at t=0 for the same heat");

        // Monotonic decrease with time at fixed heat, over a long sweep well past par.
        bool monotonic = true;
        float previous = RunScore.Compute(Par, 0f, 6f, 0.1f);
        for (float t = 60f; t <= Par * 4f; t += 60f)
        {
            float now = RunScore.Compute(Par, t, 6f, 0.1f);
            monotonic &= now < previous;
            previous = now;
        }
        Check.That(monotonic, "score decreases monotonically with elapsed time at fixed heat");

        // ...and never reaches zero or goes negative, however long the run drags on.
        Check.That(previous > 0f, "score stays positive far past par");

        // Heat scales the whole curve, at any point on it.
        Check.That(RunScore.Compute(Par, 1234f, 10f, 0.1f) > RunScore.Compute(Par, 1234f, 0f, 0.1f),
            "more heat scores higher at the same time");

        // A negative elapsed time is a nonsense input, clamped to 0 rather than zeroing the score.
        Check.That(System.Math.Abs(RunScore.Compute(Par, -5f, 0f, 0.1f) - 100f) < 0.001f,
            "negative elapsed clamps to t=0");

        // Degenerate par: nothing to measure pace against.
        Check.That(RunScore.Compute(0f, 100f, 5f, 0.1f) == 0f, "zero par yields 0");
        Check.That(RunScore.Compute(0f, 0f, 5f, 0.1f) == 0f, "zero par and zero elapsed yields 0, not NaN");
        Check.That(RunScore.Compute(-10f, 50f, 0f, 0.1f) == 0f, "negative par yields 0");
    }
}
