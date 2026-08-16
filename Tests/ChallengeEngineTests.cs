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
    }
}
