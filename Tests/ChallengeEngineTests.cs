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
