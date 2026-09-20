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

        // Act I's opening arc, before the pale light (2026-09-19). Three beats that teach the
        // act's one rule instead of narrating it: the errand, the failed experiment, the vigil.
        // None of the three is a prefab name.
        //
        // DayDeerKill is the daylight twin of NightDeerKill and exists to be USED as a step
        // param, not merely reported: killing a deer by day is a quest whose whole content is
        // that nothing happens.
        public const string RavenHeard = "RavenHeard";
        public const string DayDeerKill = "__day_deer";
        public const string NightWatch = "NightWatch";

        // Act I's hunter's shade (2026-09-12): spoken to, then paid. Both are events the shade's
        // interact raises; neither is a prefab name.
        public const string ShadeFound = "ShadeFound";
        public const string ShadeDelivered = "ShadeDelivered";

        /// <summary>The delivery step. The saga's bow recipe registers while this step is DONE.</summary>
        public const string ShadeBringStepId = "mq-shade-bring";

        /// <summary>
        /// Act I's troll. The shield's recipe registers while this step is DONE, and "done" includes
        /// FAILED: a step that ran out its clock still advances its track, so the bench learns the
        /// shape either way and the missing troll hide is what the loss actually costs.
        /// </summary>
        public const string BreakerStepId = "mq-troll";

        /// <summary>
        /// PlayerState measures the Stormward raises: how many times it has discharged, and whether
        /// the player has stood out in a thunderstorm holding it. Named here because a step's Param
        /// and the code that reports it must be the same string, and two literals are two chances to
        /// mistype one.
        /// </summary>
        public const string StormwardAnswered = "StormwardAnswered";
        public const string StormVigil = "StormVigil";

        // Act VIII stand-ins (2026-09-12). Valheim 1.0's Deep North boss exists — the assembly has
        // GP_DeepNorth and a "frozen king" item token — but its prefab, altar location and defeat
        // key are asset data. These are deliberately un-guessed: a guess that happened to be right
        // would be a design nobody made. The run-start "Boss registry" log lines print the real
        // names; put them in RunService's boss table and delete these.
        public const string DeepNorthBoss = "__deep_north_boss";
        public const string DeepNorthAltar = "__deep_north_altar";
        public const string DeepNorthBossKey = "__deep_north_defeated";
    }
}
