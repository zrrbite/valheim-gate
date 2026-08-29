namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The synthetic names the saga measures itself by — the ones no prefab is called.
    ///
    /// They live in PURE code so the step predicates that read them can be unit-tested. Every
    /// bug this mode has shipped in a gate (a race that took no credit, a bearing pointing at a
    /// finished chase, a hunt whose own gate said it was not running) was pure logic stranded in
    /// game-coupled code where the harness could not see it.
    /// </summary>
    public static class SagaNames
    {
        public const string Deer = "Deer";
        public const string NightDeerKill = "__night_deer";
        public const string HeraldKill = "EikthyrHerald";
        public const string GathererKill = "__the_gatherer";
        public const string SpiritFound = "SpiritFound";
        public const string LightTaken = "SpiritTaken";
        public const string InterceptStepId = "bf-intercept";
    }
}
