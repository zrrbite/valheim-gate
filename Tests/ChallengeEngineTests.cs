using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

static class ChallengeEngineTests
{
    static List<ChallengeDefinition> Pool() => new List<ChallengeDefinition>
    {
        new ChallengeDefinition { Id="k-grey", Kind=ChallengeKind.KillPrefab, Param="Greydwarf", Target=5, HeatReward=2, Display="Kill 5 Greydwarves" },
        new ChallengeDefinition { Id="alt",    Kind=ChallengeKind.ReachAltitude, Param="", Target=150, HeatReward=3, Display="Reach 150m altitude" },
        new ChallengeDefinition { Id="build",  Kind=ChallengeKind.BuildHeight, Param="", Target=10, HeatReward=2, Display="Build 10m high" },
        new ChallengeDefinition { Id="wood",   Kind=ChallengeKind.CollectItem, Param="Wood", Target=50, HeatReward=1, Display="Hold 50 wood" },
        new ChallengeDefinition { Id="naked",  Kind=ChallengeKind.NoArmorMinutes, Param="", Target=5, HeatReward=3, Display="No armor for 5 min" },
    };

    public static void Run()
    {
        var e = new ChallengeEngine(Pool(), new Random(42), refillCooldownSeconds: 120f);
        e.Tick(0.1f);
        Check.That(e.Active.Count == 3, "fills to 3 active");
        Check.That(e.Active.Select(a => a.Def.Id).Distinct().Count() == 3, "actives are distinct");

        // Complete a kill challenge if one is active; otherwise complete via measure.
        ChallengeDefinition completed = null;
        e.Completed += d => completed = d;
        foreach (var a in e.Active.ToList())
        {
            if (a.Def.Kind == ChallengeKind.KillPrefab)
                for (int i = 0; i < (int)a.Def.Target; i++) e.ReportKill(a.Def.Param);
            else
                e.ReportMeasure(a.Def.Kind, a.Def.Param, a.Def.Target);
        }
        e.Tick(0.1f);
        Check.That(completed != null, "completion event fired");
        Check.That(e.Active.Count < 3, "completed slot is empty until cooldown");

        e.Tick(119f);
        int before = e.Active.Count;
        e.Tick(2f);
        Check.That(e.Active.Count == 3 && before < 3, "refills after cooldown");

        // Reroll swaps for a different definition
        var oldId = e.Active[0].Def.Id;
        Check.That(e.Reroll(0), "reroll succeeds with pool alternatives");
        Check.That(e.Active[0].Def.Id != oldId, "reroll changed the challenge");
        Check.That(e.Active.Select(a => a.Def.Id).Distinct().Count() == 3, "still distinct after reroll");

        // Kill reports only touch matching prefab
        var e2 = new ChallengeEngine(Pool().Where(d => d.Id == "k-grey").ToList(), new Random(1), 120f);
        e2.Tick(0.1f);
        e2.ReportKill("Troll");
        Check.That(e2.Active[0].Progress == 0f, "non-matching kill ignored");
        e2.ReportKill("Greydwarf");
        Check.That(e2.Active[0].Progress == 1f, "matching kill counts");

        // Per-slot cooldown: each completed slot refills independently
        var e3 = new ChallengeEngine(Pool(), new Random(99), 120f);
        e3.Tick(0.1f);
        Check.That(e3.Active.Count == 3, "starts with 3");

        // Complete first challenge (by kind, to avoid hardcoding which one is which)
        var first = e3.Active[0];
        if (first.Def.Kind == ChallengeKind.KillPrefab)
            for (int i = 0; i < (int)first.Def.Target; i++) e3.ReportKill(first.Def.Param);
        else
            e3.ReportMeasure(first.Def.Kind, first.Def.Param, first.Def.Target);

        e3.Tick(0.1f);
        Check.That(e3.Active.Count == 2, "first completes, slot vacates");

        e3.Tick(60f);
        Check.That(e3.Active.Count == 2, "after 60s total, first slot still pending");

        // Complete second challenge
        var second = e3.Active[0];
        if (second.Def.Kind == ChallengeKind.KillPrefab)
            for (int i = 0; i < (int)second.Def.Target; i++) e3.ReportKill(second.Def.Param);
        else
            e3.ReportMeasure(second.Def.Kind, second.Def.Param, second.Def.Target);

        e3.Tick(0.1f);
        Check.That(e3.Active.Count == 1, "second completes, now only third remains");

        e3.Tick(61f);
        Check.That(e3.Active.Count == 2, "first slot refilled at ~120s mark, second still pending at ~181s mark");

        e3.Tick(60f);
        Check.That(e3.Active.Count == 3, "second slot refilled after its own ~120s cooldown");

        RestoreActiveTests();
        TierTests();
        CollectFoodTests();
        StatDeltaTests();
        BaselineRoundTripTests();
        OpenerTests();
    }

    /// <summary>Three openers in a fixed order, plus enough ordinary defs to fill and refill around them.</summary>
    static List<ChallengeDefinition> OpenerPool() => new List<ChallengeDefinition>
    {
        new ChallengeDefinition { Id="o-wood",  Opener=true, Kind=ChallengeKind.CollectItem, Param="$item_wood",      Target=5, HeatReward=1, Display="Hold 5 Wood" },
        new ChallengeDefinition { Id="o-stone", Opener=true, Kind=ChallengeKind.CollectItem, Param="$item_stone",     Target=3, HeatReward=1, Display="Hold 3 Stone" },
        new ChallengeDefinition { Id="o-craft", Opener=true, Kind=ChallengeKind.StatDelta,   Param="CraftsOrUpgrades", Target=1, HeatReward=1, Display="Craft something — an axe!" },
        new ChallengeDefinition { Id="r-a", Kind=ChallengeKind.ReachAltitude, Param="", Target=90,  HeatReward=1, Display="r-a" },
        new ChallengeDefinition { Id="r-b", Kind=ChallengeKind.CollectFood,   Param="", Target=20,  HeatReward=1, Display="r-b" },
        new ChallengeDefinition { Id="r-c", Kind=ChallengeKind.StatDelta,     Param="Jumps", Target=30, HeatReward=1, Display="r-c" },
        new ChallengeDefinition { Id="r-d", Kind=ChallengeKind.NoArmorMinutes, Param="", Target=5, HeatReward=1, Display="r-d" },
    };

    static void OpenerTests()
    {
        Check.That(!new ChallengeDefinition().Opener, "Opener defaults to false");

        // A fresh engine deals exactly the three openers, in pool order, ahead of any random draw.
        var e = new ChallengeEngine(OpenerPool(), new Random(101), 120f);
        e.Tick(0.1f);
        Check.That(e.Active.Count == 3, "fresh engine fills all three slots");
        Check.That(e.Active[0].Def.Id == "o-wood", "opener 1 is dealt first");
        Check.That(e.Active[1].Def.Id == "o-stone", "opener 2 is dealt second");
        Check.That(e.Active[2].Def.Id == "o-craft", "opener 3 is dealt third");

        // Completing one refills from the ORDINARY pool: openers never come round again.
        e.ReportMeasure(ChallengeKind.CollectItem, "$item_wood", 5f);
        e.Tick(0.1f);
        Check.That(e.Active.Count == 2, "completed opener vacates its slot");
        e.Tick(200f);
        Check.That(e.Active.Count == 3, "the slot refills after the cooldown");
        Check.That(e.Active.All(a => !a.Def.Opener || a.Def.Id == "o-stone" || a.Def.Id == "o-craft"),
            "the refill did not redeal a completed opener");
        Check.That(e.Active.Any(a => !a.Def.Opener), "the refill drew an ordinary challenge");

        // Churn hard: no opener may ever be redealt by a random draw.
        var churn = new ChallengeEngine(OpenerPool(), new Random(103), 120f);
        churn.Tick(0.1f);
        var openersSeen = new HashSet<string>(churn.Active.Select(a => a.Def.Id));
        bool noRedeal = true;
        for (int i = 0; i < 40; i++)
        {
            foreach (var a in churn.Active.ToList()) a.Progress = a.Def.Target;
            churn.Tick(200f);
            // Any opener still present must be one from the original deal that hasn't completed
            // yet — and after the first cycle they have all completed, so none may appear at all.
            noRedeal &= churn.Active.All(a => !a.Def.Opener);
        }
        Check.That(openersSeen.Count == 3, "the opening deal was all three openers");
        Check.That(noRedeal, "40 completion cycles never redeal an opener");

        // Rerolling an opener swaps it for an ordinary challenge, and retires that link.
        var rr = new ChallengeEngine(OpenerPool(), new Random(107), 120f);
        rr.Tick(0.1f);
        Check.That(rr.Active[0].Def.Id == "o-wood", "reroll fixture starts on the opening chain");
        Check.That(rr.Reroll(0), "an opener slot can be rerolled");
        Check.That(!rr.Active[0].Def.Opener, "rerolling an opener draws a non-opener");
        Check.That(rr.Active.Select(a => a.Def.Id).Distinct().Count() == 3, "actives stay distinct after an opener reroll");

        // A restored run never replays the opening chain, even when it restores nothing at all.
        var resumed = new ChallengeEngine(OpenerPool(), new Random(109), 120f);
        resumed.RestoreActive(new[] { new KeyValuePair<string, float>("r-a", 12f) });
        resumed.Tick(0.1f);
        Check.That(resumed.Active.Count == 3, "a resumed engine still tops up to three");
        Check.That(resumed.Active[0].Def.Id == "r-a" && resumed.Active[0].Progress == 12f,
            "the restored active keeps its slot and progress");
        Check.That(resumed.Active.All(a => !a.Def.Opener), "a resumed run's refill is random, not the opening chain");

        var emptyResume = new ChallengeEngine(OpenerPool(), new Random(113), 120f);
        emptyResume.RestoreActive(null);
        emptyResume.Tick(0.1f);
        Check.That(emptyResume.Active.Count == 3, "restoring nothing still fills three slots");
        Check.That(emptyResume.Active.All(a => !a.Def.Opener),
            "restoring nothing still retires the opening chain");

        // Openers round-trip through RestoreActive: they resolve by id against the whole pool,
        // so a save taken mid-chain comes back intact.
        var roundTrip = new ChallengeEngine(OpenerPool(), new Random(127), 120f);
        roundTrip.RestoreActive(
            new[]
            {
                new KeyValuePair<string, float>("o-stone", 2f),
                new KeyValuePair<string, float>("o-craft", 0f),
            },
            new List<float> { float.NaN, float.NaN });
        Check.That(roundTrip.Active.Count == 2, "opener actives restore from a save");
        Check.That(roundTrip.Active[0].Def.Id == "o-stone" && roundTrip.Active[0].Progress == 2f,
            "a restored opener keeps its id and progress");
        Check.That(roundTrip.Active[1].Def.Id == "o-craft", "the second restored opener keeps its slot");
        roundTrip.Tick(0.1f);
        Check.That(roundTrip.Active.Count == 3, "a mid-chain resume tops up to three");
        Check.That(roundTrip.Active.Count(a => a.Def.Opener) == 2,
            "the top-up beside restored openers is an ordinary draw");

        // A pool with no openers behaves exactly as it did before the feature existed.
        var plain = new ChallengeEngine(Pool(), new Random(131), 120f);
        plain.Tick(0.1f);
        Check.That(plain.Active.Count == 3, "an opener-free pool still fills three slots");
        Check.That(plain.Active.Select(a => a.Def.Id).Distinct().Count() == 3, "and they are distinct");
    }

    static List<ChallengeDefinition> StatPool() => new List<ChallengeDefinition>
    {
        new ChallengeDefinition { Id="s-chop", Kind=ChallengeKind.StatDelta, Param="TreeChops", Target=15, HeatReward=1, Display="Chop 15 trees" },
        new ChallengeDefinition { Id="s-jump", Kind=ChallengeKind.StatDelta, Param="Jumps",     Target=30, HeatReward=1, Display="Jump 30 times" },
    };

    /// <summary>
    /// StatDelta is param-scoped like CollectItem: a report about one stat must not move a
    /// challenge measuring another, and it keeps the same max-semantics as every other measure.
    /// </summary>
    static void StatDeltaTests()
    {
        var e = new ChallengeEngine(StatPool(), new Random(31), 120f);
        e.Tick(0.1f);
        Check.That(e.Active.Count == 2, "both stat challenges are drawn");

        var chop = e.Active.First(a => a.Def.Id == "s-chop");
        var jump = e.Active.First(a => a.Def.Id == "s-jump");

        e.ReportMeasure(ChallengeKind.StatDelta, "TreeChops", 7f);
        Check.That(chop.Progress == 7f, "stat report lands on the matching param");
        Check.That(jump.Progress == 0f, "stat report does not touch a different param");

        // Max-semantics, same as every other measure.
        e.ReportMeasure(ChallengeKind.StatDelta, "TreeChops", 3f);
        Check.That(chop.Progress == 7f, "stat progress never regresses");

        // A stat name nothing is measuring is simply ignored.
        e.ReportMeasure(ChallengeKind.StatDelta, "DoorsOpened", 99f);
        Check.That(chop.Progress == 7f && jump.Progress == 0f, "unmatched stat param moves nothing");

        // Cross-kind isolation, both directions: CollectItem's param scoping and StatDelta's
        // must not leak into one another even when the param strings coincide.
        e.ReportMeasure(ChallengeKind.CollectItem, "TreeChops", 50f);
        Check.That(chop.Progress == 7f, "a CollectItem report never satisfies a StatDelta slot");

        e.ReportMeasure(ChallengeKind.StatDelta, "TreeChops", 15f);
        Check.That(chop.Done, "stat challenge completes at target");

        // Fresh slots carry no baseline: that is the caller's job, and NaN is how it knows.
        Check.That(float.IsNaN(jump.Baseline), "a freshly dealt slot has no baseline");
        Check.That(float.IsNaN(new ActiveChallenge().Baseline), "Baseline defaults to NaN");

        SlotMeasureTests();
        BaselineLifecycleTests();
    }

    /// <summary>
    /// ReportSlotMeasure addresses ONE slot. Param-scoping isn't fine enough for StatDelta: two
    /// slots can measure the same stat from different baselines, and a param-scoped report would
    /// hand both whichever delta was computed last.
    /// </summary>
    static void SlotMeasureTests()
    {
        // Two actives, same kind AND same param, standing in for the same stat measured from
        // different zero points.
        var pool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="s-a", Kind=ChallengeKind.StatDelta, Param="TreeChops", Target=10, HeatReward=1, Display="s-a" },
            new ChallengeDefinition { Id="s-b", Kind=ChallengeKind.StatDelta, Param="TreeChops", Target=10, HeatReward=1, Display="s-b" },
        };

        var e = new ChallengeEngine(pool, new Random(53), 120f);
        e.Tick(0.1f);
        Check.That(e.Active.Count == 2, "both same-param slots are active");

        // Slot 0 was dealt at baseline 100, slot 1 at baseline 140; the stat now reads 150.
        e.Active[0].Baseline = 100f;
        e.Active[1].Baseline = 140f;

        e.ReportSlotMeasure(0, 150f - e.Active[0].Baseline); // 50
        e.ReportSlotMeasure(1, 150f - e.Active[1].Baseline); // 10

        Check.That(e.Active[0].Progress == 50f, "slot 0 gets its own delta");
        Check.That(e.Active[1].Progress == 10f, "slot 1 is not credited with slot 0's delta");

        // Max-semantics, per slot.
        e.ReportSlotMeasure(1, 4f);
        Check.That(e.Active[1].Progress == 10f, "slot progress never regresses");
        e.ReportSlotMeasure(1, 12f);
        Check.That(e.Active[1].Progress == 12f, "a higher slot report still lands");
        Check.That(e.Active[0].Progress == 50f, "reporting to slot 1 never touches slot 0");

        // Bounds-safe.
        e.ReportSlotMeasure(-1, 999f);
        e.ReportSlotMeasure(2, 999f);
        e.ReportSlotMeasure(int.MaxValue, 999f);
        Check.That(e.Active[0].Progress == 50f && e.Active[1].Progress == 12f,
            "out-of-range slot reports are ignored");

        // The contrast that motivates the method: a param-scoped report hits BOTH slots.
        e.ReportMeasure(ChallengeKind.StatDelta, "TreeChops", 60f);
        Check.That(e.Active[0].Progress == 60f && e.Active[1].Progress == 60f,
            "a param-scoped report leaks across same-param slots, which is why slot reporting exists");
    }

    /// <summary>
    /// A baseline belongs to the SLOT, not the definition: it is taken when the slot is dealt, and
    /// a slot that is replaced starts again from nothing.
    /// </summary>
    static void BaselineLifecycleTests()
    {
        var pool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="s-a", Kind=ChallengeKind.StatDelta, Param="TreeChops", Target=10, HeatReward=1, Display="s-a" },
            new ChallengeDefinition { Id="s-b", Kind=ChallengeKind.StatDelta, Param="Jumps",     Target=10, HeatReward=1, Display="s-b" },
            new ChallengeDefinition { Id="s-c", Kind=ChallengeKind.StatDelta, Param="Mines",     Target=10, HeatReward=1, Display="s-c" },
            new ChallengeDefinition { Id="s-d", Kind=ChallengeKind.StatDelta, Param="Jumps",     Target=10, HeatReward=1, Display="s-d" },
        };

        // Rerolling a slot clears its baseline, so the caller re-takes one at the NEW deal time.
        // Keeping the old slot's zero point would credit the replacement with everything the
        // player did while the discarded challenge was up.
        var e = new ChallengeEngine(pool, new Random(59), 120f);
        e.Tick(0.1f);
        e.Active[0].Baseline = 500f;
        e.Active[0].Progress = 7f;

        Check.That(e.Reroll(0), "the slot rerolls");
        Check.That(float.IsNaN(e.Active[0].Baseline), "a rerolled slot has no baseline");
        Check.That(e.Active[0].Progress == 0f, "a rerolled slot starts from zero progress");

        // A slot dealt LATER in a run is likewise unbaselined at the moment it appears, so the
        // caller baselines it at deal time and not from run start. Without this, a mid-run deal
        // would inherit a zero point from the beginning of the run and arrive part-complete.
        var later = new ChallengeEngine(pool, new Random(61), 120f);
        later.Tick(0.1f);
        foreach (var a in later.Active) a.Baseline = 10f; // as if baselined at run start

        var openSlot = later.Active[0];
        openSlot.Progress = openSlot.Def.Target;
        later.Tick(0.1f);
        Check.That(later.Active.Count == 2, "the completed slot vacates");

        later.Tick(200f);
        Check.That(later.Active.Count == 3, "a replacement is dealt after the cooldown");

        Check.That(later.Active.Count(a => float.IsNaN(a.Baseline)) == 1,
            "exactly the newly dealt slot is unbaselined — a mid-run deal takes no zero point from run start");
        Check.That(later.Active.Count(a => a.Baseline == 10f) == 2,
            "the slots that survived keep the baseline they were dealt with");
    }

    /// <summary>
    /// Baselines survive a save/restore round trip aligned to the SAVED sequence, not to the
    /// slots that survive it — an entry dropped as unknown/duplicate/over-cap still consumes its
    /// index, because saves write the two lists from one active set.
    /// </summary>
    static void BaselineRoundTripTests()
    {
        var e = new ChallengeEngine(StatPool(), new Random(37), 120f);
        e.RestoreActive(
            new[]
            {
                new KeyValuePair<string, float>("s-chop", 4f),
                new KeyValuePair<string, float>("s-jump", 9f),
            },
            new List<float> { 120f, 55f });

        Check.That(e.Active.Count == 2, "restore fills both saved slots");
        Check.That(e.Active[0].Def.Id == "s-chop" && e.Active[0].Baseline == 120f, "baseline restored with its slot");
        Check.That(e.Active[1].Baseline == 55f, "second baseline restored");
        Check.That(e.Active[0].Progress == 4f, "progress still restored alongside the baseline");

        // Round trip: what a save would write back out is what came in.
        var savedIds = e.Active.Select(a => a.Def.Id).ToList();
        var savedBaselines = e.Active.Select(a => a.Baseline).ToList();
        var e2 = new ChallengeEngine(StatPool(), new Random(37), 120f);
        e2.RestoreActive(savedIds.Select(id => new KeyValuePair<string, float>(id, 0f)), savedBaselines);
        Check.That(e2.Active[0].Baseline == 120f && e2.Active[1].Baseline == 55f, "baselines survive a round trip");

        // A dropped entry must not shift the baselines of the entries after it.
        var skewed = new ChallengeEngine(StatPool(), new Random(41), 120f);
        skewed.RestoreActive(
            new[]
            {
                new KeyValuePair<string, float>("nonsense", 0f),  // not in the pool — dropped
                new KeyValuePair<string, float>("s-chop", 1f),
                new KeyValuePair<string, float>("s-chop", 2f),    // duplicate — dropped
                new KeyValuePair<string, float>("s-jump", 3f),
            },
            new List<float> { 999f, 120f, 888f, 55f });
        Check.That(skewed.Active.Count == 2, "dropped entries don't take a slot");
        Check.That(skewed.Active[0].Def.Id == "s-chop" && skewed.Active[0].Baseline == 120f,
            "baseline index follows the saved sequence past a dropped entry");
        Check.That(skewed.Active[1].Def.Id == "s-jump" && skewed.Active[1].Baseline == 55f,
            "baseline index survives a dropped duplicate too");

        // No baselines at all (a pre-baseline save) leaves every slot NaN for the caller to fill.
        var legacy = new ChallengeEngine(StatPool(), new Random(43), 120f);
        legacy.RestoreActive(new[] { new KeyValuePair<string, float>("s-chop", 6f) });
        Check.That(legacy.Active[0].Progress == 6f && float.IsNaN(legacy.Active[0].Baseline),
            "the one-argument restore leaves baselines unset");

        legacy.RestoreActive(new[] { new KeyValuePair<string, float>("s-chop", 6f) }, null);
        Check.That(float.IsNaN(legacy.Active[0].Baseline), "an explicitly null baseline list is the same as none");

        // A short list covers what it can and leaves the tail unset rather than throwing.
        var partial = new ChallengeEngine(StatPool(), new Random(47), 120f);
        partial.RestoreActive(
            new[]
            {
                new KeyValuePair<string, float>("s-chop", 0f),
                new KeyValuePair<string, float>("s-jump", 0f),
            },
            new List<float> { 120f });
        Check.That(partial.Active[0].Baseline == 120f && float.IsNaN(partial.Active[1].Baseline),
            "a short baseline list leaves the uncovered tail unset");

        // Restored baselines must not disturb the top-up path.
        partial.Tick(0.1f);
        Check.That(partial.Active.Count == 2, "pool of 2 stays at 2 after top-up");
        Check.That(partial.Active.Select(a => a.Def.Id).Distinct().Count() == 2, "actives stay distinct after a baseline restore");
    }

    /// <summary>
    /// CollectFood is a category measure: no Param, reported with an empty one, max-semantics
    /// like altitude rather than the per-item scoping CollectItem uses.
    /// </summary>
    static void CollectFoodTests()
    {
        var pool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="c-food", Kind=ChallengeKind.CollectFood, Param="", Target=20, HeatReward=1, Display="Hold 20 food items" },
        };

        var e = new ChallengeEngine(pool, new Random(4), 120f);
        e.Tick(0.1f);
        Check.That(e.Active.Count == 1 && e.Active[0].Def.Kind == ChallengeKind.CollectFood, "food challenge is drawn");

        e.ReportMeasure(ChallengeKind.CollectFood, "", 8f);
        Check.That(e.Active[0].Progress == 8f, "food measure with empty param progresses");

        // Max-semantics: eating some food must not push progress backwards.
        e.ReportMeasure(ChallengeKind.CollectFood, "", 3f);
        Check.That(e.Active[0].Progress == 8f, "food progress never regresses");

        // A named-item report must not satisfy the category challenge, and vice versa.
        e.ReportMeasure(ChallengeKind.CollectItem, "$item_honey", 50f);
        Check.That(e.Active[0].Progress == 8f, "CollectItem reports don't touch a CollectFood slot");

        e.ReportMeasure(ChallengeKind.CollectFood, "", 20f);
        Check.That(e.Active[0].Done, "food challenge completes at target");

        // The empty param is not special-cased — a param-carrying report of the same kind
        // still lands, since only CollectItem is param-scoped.
        var e2 = new ChallengeEngine(pool, new Random(4), 120f);
        e2.Tick(0.1f);
        e2.ReportMeasure(ChallengeKind.CollectFood, "ignored", 12f);
        Check.That(e2.Active[0].Progress == 12f, "CollectFood ignores param entirely");
    }

    static void RestoreActiveTests()
    {
        // Restores ids and progress from a save.
        var e = new ChallengeEngine(Pool(), new Random(3), 120f);
        e.RestoreActive(new[]
        {
            new KeyValuePair<string, float>("alt", 90f),
            new KeyValuePair<string, float>("wood", 25f),
        });
        Check.That(e.Active.Count == 2, "restore fills only the saved slots");
        Check.That(e.Active[0].Def.Id == "alt" && e.Active[0].Progress == 90f, "restored id and progress");
        Check.That(e.Active[1].Def.Id == "wood" && e.Active[1].Progress == 25f, "restored second slot");

        // Tick tops the shortfall back up to 3 without disturbing what was restored.
        e.Tick(0.1f);
        Check.That(e.Active.Count == 3, "restore shortfall tops up on next tick");
        Check.That(e.Active.Any(a => a.Def.Id == "alt" && a.Progress == 90f), "topping up preserved restored progress");
        Check.That(e.Active.Select(a => a.Def.Id).Distinct().Count() == 3, "still distinct after top-up");

        // Ids the engine's own pool doesn't have are ignored — this is what keeps a
        // kill-filtered pool from resurrecting kill challenges on resume.
        var filtered = new ChallengeEngine(
            Pool().Where(d => d.Kind != ChallengeKind.KillPrefab).ToList(), new Random(3), 120f);
        filtered.RestoreActive(new[]
        {
            new KeyValuePair<string, float>("k-grey", 4f),
            new KeyValuePair<string, float>("alt", 10f),
            new KeyValuePair<string, float>("nonsense", 1f),
        });
        Check.That(filtered.Active.Count == 1 && filtered.Active[0].Def.Id == "alt",
            "restore ignores ids outside the engine's own pool");

        // Duplicates dropped, 3-slot cap respected.
        var capped = new ChallengeEngine(Pool(), new Random(3), 120f);
        capped.RestoreActive(new[]
        {
            new KeyValuePair<string, float>("alt", 1f),
            new KeyValuePair<string, float>("alt", 2f),
            new KeyValuePair<string, float>("wood", 3f),
            new KeyValuePair<string, float>("naked", 4f),
            new KeyValuePair<string, float>("build", 5f),
            new KeyValuePair<string, float>("k-grey", 6f),
        });
        Check.That(capped.Active.Count == 3, "restore caps at 3 slots");
        Check.That(capped.Active.Select(a => a.Def.Id).Distinct().Count() == 3, "restore drops duplicate ids");
        Check.That(capped.Active[0].Progress == 1f, "restore keeps the first of a duplicated id");

        // Restoring over an existing set replaces it wholesale.
        var replaced = new ChallengeEngine(Pool(), new Random(5), 120f);
        replaced.Tick(0.1f);
        replaced.RestoreActive(new[] { new KeyValuePair<string, float>("naked", 2f) });
        Check.That(replaced.Active.Count == 1 && replaced.Active[0].Def.Id == "naked",
            "restore replaces the previous active set");

        // Null input clears rather than throwing.
        replaced.RestoreActive(null);
        Check.That(replaced.Active.Count == 0, "restore(null) clears the active set");
    }

    /// <summary>
    /// A pool spanning tiers 0..3: three tier-0/1 definitions (enough to fill all three slots
    /// on their own) and three that sit above them.
    /// </summary>
    static List<ChallengeDefinition> TieredPool() => new List<ChallengeDefinition>
    {
        new ChallengeDefinition { Id="t0-a", Kind=ChallengeKind.CollectItem, Param="Wood",  Target=10, HeatReward=1, Display="t0-a", Tier=0 },
        new ChallengeDefinition { Id="t0-b", Kind=ChallengeKind.CollectItem, Param="Stone", Target=10, HeatReward=1, Display="t0-b", Tier=0 },
        new ChallengeDefinition { Id="t1-a", Kind=ChallengeKind.ReachAltitude, Param="", Target=90, HeatReward=1, Display="t1-a", Tier=1 },
        new ChallengeDefinition { Id="t2-a", Kind=ChallengeKind.KillPrefab, Param="Draugr", Target=8, HeatReward=3, Display="t2-a", Tier=2 },
        new ChallengeDefinition { Id="t2-b", Kind=ChallengeKind.KillPrefab, Param="Wraith", Target=2, HeatReward=3, Display="t2-b", Tier=2 },
        new ChallengeDefinition { Id="t3-a", Kind=ChallengeKind.ReachAltitude, Param="", Target=150, HeatReward=2, Display="t3-a", Tier=3 },
    };

    static void TierTests()
    {
        // Default Tier is 0, and the default MaxTier admits the entire pool — this is what
        // keeps every test above (whose pool never sets Tier) behaving exactly as before.
        Check.That(new ChallengeDefinition().Tier == 0, "Tier defaults to 0");

        var wide = new ChallengeEngine(TieredPool(), new Random(7), 120f);
        Check.That(wide.MaxTier == int.MaxValue, "MaxTier defaults to unlimited");
        for (int i = 0; i < 30; i++)
        {
            // Complete-and-refill churn so the draw sees plenty of different slots.
            wide.Tick(200f);
            foreach (var a in wide.Active.ToList()) a.Progress = a.Def.Target;
        }
        wide.Tick(200f);
        Check.That(wide.Active.Count == 3, "default MaxTier still fills all three slots");

        // --- MaxTier filters draws ---
        var capped = new ChallengeEngine(TieredPool(), new Random(11), 120f) { MaxTier = 1 };
        capped.Tick(0.1f);
        Check.That(capped.Active.Count == 3, "tier-capped pool with 3 eligible defs still fills 3 slots");
        Check.That(capped.Active.All(a => a.Def.Tier <= 1), "draws never exceed MaxTier");

        // Churn: completions and refills must keep respecting the cap.
        bool refillsStayedInTier = true;
        for (int i = 0; i < 20; i++)
        {
            foreach (var a in capped.Active.ToList()) a.Progress = a.Def.Target;
            capped.Tick(200f);
            refillsStayedInTier &= capped.Active.All(a => a.Def.Tier <= 1);
        }
        Check.That(refillsStayedInTier, "refills never exceed MaxTier across 20 completion cycles");

        // A cap tighter than the slot count simply leaves slots empty rather than reaching up.
        var tight = new ChallengeEngine(TieredPool(), new Random(13), 120f) { MaxTier = 0 };
        tight.Tick(0.1f);
        Check.That(tight.Active.Count == 2, "only the two tier-0 defs are drawable at MaxTier 0");
        Check.That(tight.Active.All(a => a.Def.Tier == 0), "tight cap draws tier 0 only");

        // --- Raising MaxTier admits higher tiers on refill ---
        tight.MaxTier = 2;
        tight.Tick(0.1f);
        Check.That(tight.Active.Count == 3, "raising MaxTier tops the empty slot back up");
        Check.That(tight.Active.Any(a => a.Def.Tier > 0), "the newly admitted slot came from a higher tier");
        Check.That(tight.Active.All(a => a.Def.Tier <= 2), "still bounded by the new MaxTier");

        // --- Reroll respects MaxTier ---
        // Only above-tier alternatives remain, so there is nothing legal to swap in.
        var stuck = new ChallengeEngine(TieredPool(), new Random(17), 120f) { MaxTier = 0 };
        stuck.RestoreActive(new[]
        {
            new KeyValuePair<string, float>("t0-a", 0f),
            new KeyValuePair<string, float>("t0-b", 0f),
        });
        Check.That(!stuck.Reroll(0), "reroll fails when every alternative is above MaxTier");
        Check.That(stuck.Active[0].Def.Id == "t0-a", "a failed reroll leaves the slot untouched");

        // With a legal alternative available, reroll swaps but never reaches above the cap.
        var rerolled = new ChallengeEngine(TieredPool(), new Random(19), 120f) { MaxTier = 1 };
        rerolled.RestoreActive(new[]
        {
            new KeyValuePair<string, float>("t0-a", 0f),
            new KeyValuePair<string, float>("t2-a", 0f),
        });
        bool allRerollsLegal = true;
        for (int i = 0; i < 20; i++)
        {
            allRerollsLegal &= rerolled.Reroll(1) && rerolled.Active[1].Def.Tier <= 1;
        }
        Check.That(allRerollsLegal, "20 rerolls all succeed and none draws above MaxTier");

        // --- IsAboveTier ---
        // RestoreActive deliberately does NOT filter by tier: an old save may hold an
        // above-tier active, which stays put and is flagged here instead.
        var restored = new ChallengeEngine(TieredPool(), new Random(23), 120f) { MaxTier = 1 };
        restored.RestoreActive(new[]
        {
            new KeyValuePair<string, float>("t3-a", 5f),
            new KeyValuePair<string, float>("t0-a", 5f),
        });
        Check.That(restored.Active.Count == 2 && restored.Active[0].Def.Id == "t3-a",
            "restore keeps an above-tier active from an older save");
        Check.That(restored.IsAboveTier(0), "IsAboveTier true for a restored above-tier active");
        Check.That(!restored.IsAboveTier(1), "IsAboveTier false for a within-tier active");
        Check.That(!restored.IsAboveTier(-1), "IsAboveTier false below range");
        Check.That(!restored.IsAboveTier(99), "IsAboveTier false above range");

        // Raising the cap over it clears the flag without touching the slot.
        restored.MaxTier = 3;
        Check.That(!restored.IsAboveTier(0), "raising MaxTier clears the above-tier flag");
    }
}
