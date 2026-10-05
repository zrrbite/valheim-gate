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

        // --- The night watch is dark work too (2026-09-19) ---------------------------------
        //
        // It is advanced by the nightly whisper, so a strip that did not tell the player to wait
        // for dark would leave the act's first dark step looking like a step that does nothing.
        var watch = new List<QuestTrack>
        {
            Track("hunt", Step("mq-watch", ChallengeKind.PlayerEvent, SagaNames.NightWatch)),
        };
        Check.That(StepPredicates.DarkStep(watch), "the night watch imposes the dark rule");
        Check.That(!StepPredicates.DeerHunt(watch), "but it is not a deer hunt " + "—" + " no pack, no herd");
        Check.That(!StepPredicates.LightRace(watch), "and nothing is being raced for yet");

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

        // --- The hunter's shade: wanted while either step is live, done when passed ---------
        var shadeFind = new List<QuestTrack>
        {
            Track("craft", Step("mq-shade-find", ChallengeKind.PlayerEvent, SagaNames.ShadeFound)),
        };
        Check.That(StepPredicates.Shade(shadeFind) && StepPredicates.ShadeFind(shadeFind), "the shade stands while it is to be found");
        Check.That(!StepPredicates.ShadeDelivery(shadeFind), "but is not yet taking payment");

        var shadeBring = new List<QuestTrack>
        {
            Track("craft", Step(SagaNames.ShadeBringStepId, ChallengeKind.PlayerEvent, SagaNames.ShadeDelivered)),
        };
        Check.That(StepPredicates.Shade(shadeBring) && StepPredicates.ShadeDelivery(shadeBring), "and while it is to be paid");
        Check.That(!StepPredicates.StepDone(shadeBring, SagaNames.ShadeBringStepId), "the delivery is not done while current");

        var find = Step("mq-shade-find", ChallengeKind.PlayerEvent, SagaNames.ShadeFound);
        var bring = Step(SagaNames.ShadeBringStepId, ChallengeKind.PlayerEvent, SagaNames.ShadeDelivered);
        var bow = Step("mq-bow", ChallengeKind.CollectItem, "$item_bow_finewood");
        var paid = new List<QuestTrack>
        {
            new QuestTrack
            {
                Id = "craft", Label = "CRAFT",
                Chain = new List<ChallengeDefinition> { find, bring, bow },
                Index = 2, Current = new ActiveChallenge { Def = bow },
            },
        };
        Check.That(StepPredicates.StepDone(paid, SagaNames.ShadeBringStepId), "the delivery is done once the track has moved past it — this is what unlocks the recipe");
        Check.That(StepPredicates.StepDone(paid, "mq-shade-find"), "and so is the step before it");
        Check.That(!StepPredicates.StepDone(paid, "mq-bow"), "the current step is not done");
        Check.That(!StepPredicates.Shade(paid), "and the shade is no longer wanted");
        Check.That(!StepPredicates.StepDone(paid, "no-such-step"), "a step in no chain is never done — a dropped step unlocks nothing");
        Check.That(!StepPredicates.StepDone(null, "mq-bow") && !StepPredicates.StepDone(paid, null), "null input is not done");

        // --- The thane (2026-09-27): wanted while his step is live, not once it is passed -----
        //
        // His presence after the step rides StepDone("mq-bed") in the host, so this predicate is
        // only the "to be found" half - and it must go false once the track moves on, or the
        // bearing strip keeps pointing at a man already met.
        var thaneStep = Step("mq-thane", ChallengeKind.PlayerEvent, SagaNames.ThaneFound);
        var thaneLive = new List<QuestTrack> { Track("hearth", thaneStep) };
        Check.That(StepPredicates.Thane(thaneLive), "the thane is to be found while mq-thane is current");
        Check.That(!StepPredicates.Thjalfi(thaneLive) && !StepPredicates.ShadeFind(thaneLive),
                   "and his event is his own - neither Thjalfi nor the shade answers to it");

        var home = Step("mq-home", ChallengeKind.StatDelta, "TimeInBase");
        var thanePassed = new List<QuestTrack>
        {
            new QuestTrack
            {
                Id = "hearth", Label = "HEARTH",
                Chain = new List<ChallengeDefinition> { thaneStep, home },
                Index = 1, Current = new ActiveChallenge { Def = home },
            },
        };
        Check.That(!StepPredicates.Thane(thanePassed), "and NOT once the hearth track has moved past him");
        Check.That(StepPredicates.StepDone(thanePassed, "mq-thane"), "his step is done then");
        Check.That(!StepPredicates.Thane(new List<QuestTrack> { Track("hearth", thaneStep, blocked: true) }),
                   "a blocked thane step is not in play");

        // --- 2026-10-05: Act II's voices ---------------------------------------------------
        var keeperFind = new List<QuestTrack> { Track("craft", Step("bf-keeper", ChallengeKind.PlayerEvent, SagaNames.KeeperFound)) };
        var keeperPay = new List<QuestTrack> { Track("craft", Step("bf-keeper-light", ChallengeKind.PlayerEvent, SagaNames.KeeperPaid)) };
        Check.That(StepPredicates.KeeperFind(keeperFind) && StepPredicates.Keeper(keeperFind), "the keeper is wanted while his first step is live");
        Check.That(StepPredicates.KeeperPayment(keeperPay) && StepPredicates.Keeper(keeperPay), "and while his payment is");
        Check.That(!StepPredicates.KeeperPayment(keeperFind) && !StepPredicates.KeeperFind(keeperPay), "the two phases are not confused");

        var ask = new List<QuestTrack> { Track("hunt", Step("bf-haldor-ask", ChallengeKind.PlayerEvent, SagaNames.HaldorTold)) };
        Check.That(StepPredicates.HaldorAsk(ask), "Haldor's ask is live on its step");
        Check.That(!StepPredicates.HaldorAsk(keeperFind), "and not on the keeper's");

        // The pin waits for the ALTAR's discovery, not any discovery: the tomb and the trader are
        // DiscoverLocation steps too, and keying on the kind alone pinned the Elder while you were
        // still looking for the burial chambers.
        var tomb = new List<QuestTrack> { Track("craft", Step("bf-tomb", ChallengeKind.DiscoverLocation, "Crypt2")) };
        var trader = new List<QuestTrack> { Track("hunt", Step("bf-haldor", ChallengeKind.DiscoverLocation, "Vendor_BlackForest")) };
        var altar = new List<QuestTrack> { Track("hunt", Step("bf-find", ChallengeKind.DiscoverLocation, "GDKing")) };
        Check.That(!StepPredicates.AltarDiscovery(tomb, "GDKing"), "no altar pin while the tomb is the discovery");
        Check.That(!StepPredicates.AltarDiscovery(trader, "GDKing"), "nor while the trader is");
        Check.That(StepPredicates.AltarDiscovery(altar, "GDKing"), "the pin comes with the altar's own step");
        Check.That(!StepPredicates.AltarDiscovery(altar, null), "a null altar name pins nothing");
        Check.That(!StepPredicates.AltarDiscovery(
                       new List<QuestTrack> { Track("hunt", Step("bf-find", ChallengeKind.DiscoverLocation, "GDKing"), blocked: true) }, "GDKing"),
                   "a blocked track's step is not live");

        // Haldor's key: run-scoped, lower case, and recognisable for cleanup.
        Check.That(SagaNames.HaldorKey(12345) == "saga_haldor_12345", "Haldor's key carries the run seed");
        Check.That(SagaNames.HaldorKey(-7) != SagaNames.HaldorKey(7), "a negative seed is its own key");
        Check.That(SagaNames.HaldorKey(-7) == SagaNames.HaldorKey(-7).ToLowerInvariant(), "and lower case, as the game stores keys");
        Check.That(SagaNames.IsHaldorKey(SagaNames.HaldorKey(99)) && SagaNames.IsHaldorKey("SAGA_HALDOR_1"), "cleanup recognises any of them");
        Check.That(!SagaNames.IsHaldorKey("defeated_gdking") && !SagaNames.IsHaldorKey(null), "and nothing else");

        // --- 2026-10-05: Act III's voices --------------------------------------------------
        var drownedFind = new List<QuestTrack> { Track("craft", Step("sw-drowned", ChallengeKind.PlayerEvent, SagaNames.DrownedFound)) };
        var letGo = new List<QuestTrack> { Track("craft", Step("sw-letgo", ChallengeKind.KillPrefab, SagaNames.DrownedKill)) };
        var witch = new List<QuestTrack> { Track("marsh", Step("sw-witch", ChallengeKind.PlayerEvent, SagaNames.WitchMet)) };
        Check.That(StepPredicates.DrownedFind(drownedFind) && !StepPredicates.DrownedLetGo(drownedFind), "the drowned one is first spoken to");
        Check.That(StepPredicates.DrownedLetGo(letGo) && !StepPredicates.DrownedFind(letGo), "and later let go - never confused");
        Check.That(StepPredicates.WitchFind(witch) && !StepPredicates.WitchFind(drownedFind), "the witch's step is her own");
        Check.That(StepPredicates.StepLive(drownedFind, "sw-drowned") && !StepPredicates.StepLive(drownedFind, "sw-scrap"),
                   "StepLive names exactly the current step");
        Check.That(!StepPredicates.StepLive(new List<QuestTrack> { Track("craft", Step("sw-scrap", ChallengeKind.CollectItem, "x"), blocked: true) }, "sw-scrap"),
                   "and not a blocked one");
        Check.That(!StepPredicates.StepLive(null, "sw-scrap") && !StepPredicates.StepLive(drownedFind, null), "nor from nothing");

        // --- Degenerate input ---------------------------------------------------------------
        Check.That(!StepPredicates.DeerHunt(null), "a null track list is not a hunt");
        Check.That(!StepPredicates.DeerHunt(new List<QuestTrack>()), "nor is an empty one");
        Check.That(!StepPredicates.DeerHunt(new List<QuestTrack> { Track("hunt", null) }),
                   "nor is an exhausted track");
    }
}
