using System.Collections.Generic;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The gate questions the act's live systems ask.
///
/// Every case below is a bug that SHIPPED, written as the assertion that would have caught it.
/// These predicates were properties on RunService — pure logic stranded in game-coupled code the
/// harness cannot compile — which is the only reason they rotted while everything behind an
/// interface stayed correct.
/// </summary>
static class StepPredicateTests
{
    static QuestTrack Track(string id, ChallengeDefinition current, bool blocked = false) =>
        new QuestTrack
        {
            Id = id,
            Label = id.ToUpperInvariant(),
            Chain = new List<ChallengeDefinition> { current },
            Current = current == null ? null : new ActiveChallenge { Def = current },
            Blocked = blocked,
        };

    static ChallengeDefinition Step(string id, ChallengeKind kind, string param) =>
        new ChallengeDefinition { Id = id, MainQuest = true, Kind = kind, Param = param, Target = 1, Display = id };

    public static void Run()
    {
        // --- alpha61-70: the race became a PlayerEvent and the hunt gate did not know -------
        var race = new List<QuestTrack>
        {
            Track("hunt", Step("mq-deer", ChallengeKind.PlayerEvent, SagaNames.LightTaken)),
        };
        Check.That(StepPredicates.DeerHunt(race),
                   "the light race IS the deer hunt — the gate that missed this killed lights and packs for nine versions");
        Check.That(StepPredicates.LightRace(race), "and it is a light race");
        Check.That(StepPredicates.DarkStep(race), "and it is night-only work");

        // Kills still count, all three names.
        foreach (var p in new[] { SagaNames.Deer, SagaNames.NightDeerKill, SagaNames.HeraldKill })
        {
            var t = new List<QuestTrack> { Track("hunt", Step("k", ChallengeKind.KillPrefab, p)) };
            Check.That(StepPredicates.DeerHunt(t), $"a kill step for '{p}' is a deer hunt");
        }

        // --- alpha67.2: surfaces keyed on Found, not on the step ---------------------------
        var chase = new List<QuestTrack>
        {
            Track("hunt", Step("mq-spirit", ChallengeKind.PlayerState, SagaNames.SpiritFound)),
        };
        Check.That(StepPredicates.SpiritChase(chase), "the chase is in play while its step is current");

        var afterChase = new List<QuestTrack>
        {
            Track("hunt", Step("mq-herald", ChallengeKind.KillPrefab, SagaNames.HeraldKill)),
        };
        Check.That(!StepPredicates.SpiritChase(afterChase),
                   "and NOT once the track has moved on — a dev-skip leaves Found false forever, which shadowed the Herald's bearing");
        Check.That(StepPredicates.Herald(afterChase), "the Herald is what is wanted there");

        // --- alpha73.1: the score narrated fights that were not the race --------------------
        Check.That(!StepPredicates.LightRace(afterChase),
                   "no race is running during the Herald hunt, so no score should be announced");

        // --- Blocked steps are not in play -------------------------------------------------
        var blocked = new List<QuestTrack>
        {
            Track("hunt", Step("mq-deer", ChallengeKind.PlayerEvent, SagaNames.LightTaken), blocked: true),
        };
        Check.That(!StepPredicates.DeerHunt(blocked), "a blocked step is not in play");
        Check.That(!StepPredicates.DarkStep(blocked), "nor does it impose the dark rule");

        // --- Multi-track: any track can carry the step -------------------------------------
        var spread = new List<QuestTrack>
        {
            Track("hunt", Step("mq-eik", ChallengeKind.KillPrefab, "Eikthyr")),
            Track("craft", Step("mq-axe", ChallengeKind.StatDelta, "Crafts")),
            Track("hearth", Step("mq-deer", ChallengeKind.PlayerEvent, SagaNames.LightTaken)),
        };
        Check.That(StepPredicates.DeerHunt(spread), "a race on ANY track counts — routing has moved it twice already");

        // --- The Gatherer and the couriers, by their own names -----------------------------
        var gatherer = new List<QuestTrack>
        {
            Track("hunt", Step("mq-gatherer", ChallengeKind.KillPrefab, SagaNames.GathererKill)),
        };
        Check.That(StepPredicates.Gatherer(gatherer), "the Gatherer's step is recognised");
        Check.That(!StepPredicates.DeerHunt(gatherer),
                   "but it is NOT a deer hunt — packs and lights must not fire during it");

        var couriers = new List<QuestTrack>
        {
            Track("hunt", Step(SagaNames.InterceptStepId, ChallengeKind.PlayerEvent, SagaNames.LightTaken)),
        };
        Check.That(StepPredicates.CourierIntercept(couriers), "the intercept is found by id");
        Check.That(StepPredicates.LightRace(couriers), "and is a light race, so it shares the machinery");

        // --- Degenerate input ---------------------------------------------------------------
        Check.That(!StepPredicates.DeerHunt(null), "a null track list is not a hunt");
        Check.That(!StepPredicates.DeerHunt(new List<QuestTrack>()), "nor is an empty one");
        Check.That(!StepPredicates.DeerHunt(new List<QuestTrack> { Track("hunt", null) }),
                   "nor is an exhausted track");
    }
}
