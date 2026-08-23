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
        BiomeFilterTests();
        RepeatableTests();
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
        CompositeTests();
        MainQuestTests();
        BuildPieceTests();
    }

    /// <summary>
    /// BuildPiece: the "you have built one of these" measure. It is param-scoped like CollectItem
    /// and StatDelta — the host reports a CATEGORY ("Fire", "Bed", "Chest", "Door") that names a
    /// compiled Valheim type rather than an asset name — and it latches, because the host reports
    /// only what it can currently see near the player and a player walks away from their house.
    ///
    /// RequiresBuilt is the same vocabulary pointed the other way: a gate on whether a definition
    /// may be DEALT at all. The engine itself knows nothing about it; it rides ExternalFilter,
    /// which is what these cases exercise.
    /// </summary>
    static void BuildPieceTests()
    {
        Check.That(new ChallengeDefinition().RequiresBuilt == null, "RequiresBuilt defaults to null");

        var pool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="fire",  Kind=ChallengeKind.BuildPiece, Param="Fire",  Target=1, Display="Build a fire" },
            new ChallengeDefinition { Id="bed",   Kind=ChallengeKind.BuildPiece, Param="Bed",   Target=1, Display="Build a bed" },
            new ChallengeDefinition { Id="chest", Kind=ChallengeKind.BuildPiece, Param="Chest", Target=1, Display="Build a chest" },
        };

        // Param-scoping: a fire must not satisfy a bed. This is the whole reason BuildPiece joins
        // CollectItem/StatDelta in CreditMeasure's param test rather than defaulting to the
        // world-wide behaviour altitude and no-armor use.
        var e = new ChallengeEngine(pool, new Random(7), 120f);
        e.Tick(0.1f);
        var bed = e.Active.First(a => a.Def.Id == "bed");
        e.ReportMeasure(ChallengeKind.BuildPiece, "Fire", 1f);
        Check.That(bed.Progress == 0f, "a fire report leaves a bed challenge alone");
        e.ReportMeasure(ChallengeKind.BuildPiece, "Bed", 1f);
        Check.That(bed.Progress == 1f, "a bed report credits the bed challenge");
        Check.That(bed.Done, "one piece is enough to finish a BuildPiece challenge");

        // A different KIND carrying the same param must not credit it either.
        var e2 = new ChallengeEngine(pool.Where(d => d.Id == "fire").ToList(), new Random(8), 120f);
        e2.Tick(0.1f);
        e2.ReportMeasure(ChallengeKind.CollectItem, "Fire", 1f);
        Check.That(e2.Active[0].Progress == 0f, "a CollectItem report never credits a BuildPiece challenge");

        // Counting: a BuildPiece objective may ask for a QUANTITY, which is what makes "plant ten
        // seeds" a crop rather than a gesture. The host reports how many it can see; the engine
        // just measures.
        var counted = new ChallengeEngine(
            new List<ChallengeDefinition>
            {
                new ChallengeDefinition { Id="crop", Kind=ChallengeKind.BuildPiece, Param="Plant", Target=10, Display="Plant a crop" },
            },
            new Random(24), 120f);
        counted.Tick(0.1f);
        counted.ReportMeasure(ChallengeKind.BuildPiece, "Plant", 4f);
        Check.That(counted.Active[0].Progress == 4f && !counted.Active[0].Done, "a partial count is partial progress");
        counted.ReportMeasure(ChallengeKind.BuildPiece, "Plant", 10f);
        Check.That(counted.Active[0].Done, "reaching the count finishes it");
        // Harvesting the field drops the live count; max-semantics must not take the step back.
        counted.ReportMeasure(ChallengeKind.BuildPiece, "Plant", 1f);
        Check.That(counted.Active[0].Progress == 10f, "harvesting a finished crop does not un-earn it");

        // Latching: the host stops seeing the piece (reports 0) once the player walks away, and
        // ReportMeasure's max-semantics must hold the completed progress rather than undo it.
        var e3 = new ChallengeEngine(pool.Where(d => d.Id == "chest").ToList(), new Random(9), 120f);
        e3.Tick(0.1f);
        e3.ReportMeasure(ChallengeKind.BuildPiece, "Chest", 1f);
        e3.ReportMeasure(ChallengeKind.BuildPiece, "Chest", 0f);
        Check.That(e3.Active[0].Progress == 1f, "walking away from the piece does not un-earn it");

        // A BuildPiece step works in the reserved questline slot on the same terms.
        var q = new ChallengeEngine(Pool(), new Random(10), 120f);
        q.SetMainChain(new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="mq-fire", MainQuest=true, Kind=ChallengeKind.BuildPiece, Param="Fire", Target=1, Display="Build a fire" },
            new ChallengeDefinition { Id="mq-bed",  MainQuest=true, Kind=ChallengeKind.BuildPiece, Param="Bed",  Target=1, Display="Build a bed" },
        });
        q.ReportMeasure(ChallengeKind.BuildPiece, "Bed", 1f);
        q.Tick(0.1f);
        Check.That(q.CurrentMainQuest.Def.Id == "mq-fire", "a bed does not advance past the fire step");
        q.ReportMeasure(ChallengeKind.BuildPiece, "Fire", 1f);
        q.Tick(0.1f);
        Check.That(q.CurrentMainQuest.Def.Id == "mq-bed", "the fire advances the chain");
        // The bed report from before the step existed must not carry over — each step is a fresh
        // ActiveChallenge, so the chain cannot be fast-forwarded by reporting ahead of it.
        Check.That(q.CurrentMainQuest.Progress == 0f, "the next step starts at zero, not at an earlier report");

        // RequiresBuilt gating, as the host wires it: a definition naming a category the run has
        // not built yet is undrawable, and becomes drawable once the latch set gains it.
        var built = new HashSet<string>();
        var gatePool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="s-doors", Kind=ChallengeKind.StatDelta, Param="DoorsOpened", Target=8, RequiresBuilt="Door", Display="Open 8 doors" },
            new ChallengeDefinition { Id="s-jump",  Kind=ChallengeKind.StatDelta, Param="Jumps",       Target=15, Display="Jump 15 times" },
        };
        var g = new ChallengeEngine(gatePool, new Random(11), 120f);
        g.ExternalFilter = d => string.IsNullOrEmpty(d.RequiresBuilt) || built.Contains(d.RequiresBuilt);
        g.Tick(0.1f);
        Check.That(g.Active.All(a => a.Def.Id != "s-doors"), "a door task is undrawable before a door is built");

        built.Add("Door");
        var g2 = new ChallengeEngine(gatePool, new Random(12), 120f);
        g2.ExternalFilter = d => string.IsNullOrEmpty(d.RequiresBuilt) || built.Contains(d.RequiresBuilt);
        g2.Tick(0.1f);
        Check.That(g2.Active.Any(a => a.Def.Id == "s-doors"), "the door task is drawable once a door exists");

        BuildPieceCompositeTests();
        ReachBiomeTests();
        QuestTrackTests();
        DiscoverLocationTests();
    }

    /// <summary>
    /// DiscoverLocation: "you have found this place", param-scoped and latching, like every other
    /// measure whose host reports only what it can currently see.
    ///
    /// It makes an act's finale earned rather than handed over — the boss step used to simply
    /// appear, wherever the altar happened to be.
    /// </summary>
    static void DiscoverLocationTests()
    {
        Check.That(new ChallengeDefinition().Hint == null, "Hint defaults to null — most steps need none");

        var chain = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id = "find", MainQuest = true, Kind = ChallengeKind.DiscoverLocation, Param = "Eikthyrnir", Target = 1, Display = "Find Eikthyr's altar" },
            new ChallengeDefinition { Id = "kill", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Eikthyr", Target = 1, Display = "Defeat Eikthyr" },
        };

        var e = new ChallengeEngine(Pool(), new Random(51), 120f);
        e.SetMainChain(chain);

        // Param-scoped: finding a different altar is not finding this one.
        e.ReportMeasure(ChallengeKind.DiscoverLocation, "GDKing", 1f);
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest.Def.Id == "find", "a different location does not advance the chain");

        e.ReportMeasure(ChallengeKind.DiscoverLocation, "Eikthyrnir", 1f);
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest.Def.Id == "kill", "finding the named location advances to the boss");

        // Latching: the host reports only what is currently in range, so walking away must not
        // un-find a place already found.
        var e2 = new ChallengeEngine(Pool(), new Random(52), 120f);
        e2.SetMainChain(new List<ChallengeDefinition> { chain[0] });
        e2.ReportMeasure(ChallengeKind.DiscoverLocation, "Eikthyrnir", 1f);
        e2.ReportMeasure(ChallengeKind.DiscoverLocation, "Eikthyrnir", 0f);
        Check.That(e2.CurrentMainQuest.Progress == 1f, "walking away does not un-find a location");

        // Wrong kind, right param.
        var e3 = new ChallengeEngine(Pool(), new Random(53), 120f);
        e3.SetMainChain(new List<ChallengeDefinition> { chain[0] });
        e3.ReportMeasure(ChallengeKind.ReachBiome, "Eikthyrnir", 1f);
        Check.That(e3.CurrentMainQuest.Progress == 0f, "a biome report never credits a discovery step");
    }

    /// <summary>
    /// Two questlines side by side — a HUNT track and a CRAFT track — each advancing independently.
    ///
    /// The point of the split is that the player chooses which thread to pull, and since every step
    /// pays heat, that choice IS the difficulty dial: pursue both and you are stronger but hotter,
    /// rush the boss and you are safer with a lower score.
    /// </summary>
    static void QuestTrackTests()
    {
        var engine = new ChallengeEngine(Pool(), new Random(41), 120f);
        engine.SetTracks(SampleTracks());

        Check.That(engine.Tracks.Count == 2, "both tracks are installed");
        Check.That(engine.Tracks[0].Id == "hunt" && engine.Tracks[1].Id == "craft", "tracks keep their order");
        Check.That(engine.Tracks[0].Current.Def.Id == "h-1", "each track seats its own first step");
        Check.That(engine.Tracks[1].Current.Def.Id == "c-1", "the second track seats independently");

        // Independence: progress on one track must not touch the other.
        engine.ReportKill("Boar");
        engine.Tick(0.1f);
        Check.That(engine.Tracks[0].Current.Def.Id == "h-2", "a kill advances the hunt track");
        Check.That(engine.Tracks[1].Current.Def.Id == "c-1", "the craft track is untouched by a kill");

        engine.ReportMeasure(ChallengeKind.BuildPiece, "Fire", 1f);
        engine.Tick(0.1f);
        Check.That(engine.Tracks[1].Current.Def.Id == "c-2", "a build advances the craft track");
        Check.That(engine.Tracks[0].Current.Def.Id == "h-2", "the hunt track is untouched by a build");

        // Neither track is ever drawn into a random slot, nor filtered by tier or the external gate.
        var gated = new ChallengeEngine(Pool(), new Random(42), 120f) { MaxTier = -1 };
        gated.ExternalFilter = _ => false;
        gated.SetTracks(SampleTracks());
        gated.Tick(0.1f);
        Check.That(gated.Tracks.All(t => t.Current != null), "tracks ignore MaxTier and ExternalFilter");
        Check.That(gated.Active.All(a => !a.Def.MainQuest), "no track step leaks into the random slots");

        // Slot addressing: track i is addressed as -1-i, so track 0 keeps the historical -1.
        Check.That(ChallengeEngine.TrackSlot(0) == ChallengeEngine.MainQuestSlot, "track 0 is the historical main-quest slot");
        Check.That(ChallengeEngine.TrackSlot(1) == -2, "track 1 is the next slot down");

        var slotted = new ChallengeEngine(Pool(), new Random(43), 120f);
        slotted.SetTracks(SampleTracks());
        slotted.ReportSlotMeasure(ChallengeEngine.TrackSlot(1), 1f);
        Check.That(slotted.Tracks[1].Current.Progress == 1f, "a slot-addressed report credits its own track");
        Check.That(slotted.Tracks[0].Current.Progress == 0f, "and only its own track");

        // Completion fires per track, carrying the finished definition.
        var completed = new List<string>();
        var events = new ChallengeEngine(Pool(), new Random(44), 120f);
        events.SetTracks(SampleTracks());
        events.Completed += d => completed.Add(d.Id);
        events.ReportKill("Boar");
        events.ReportMeasure(ChallengeKind.BuildPiece, "Fire", 1f);
        events.Tick(0.1f);
        Check.That(completed.Contains("h-1") && completed.Contains("c-1"), "each track raises its own completion");

        // An exhausted track goes quiet without disturbing the other.
        var drained = new ChallengeEngine(Pool(), new Random(45), 120f);
        drained.SetTracks(SampleTracks());
        drained.ReportMeasure(ChallengeKind.BuildPiece, "Fire", 1f);
        drained.Tick(0.1f);
        drained.ReportMeasure(ChallengeKind.BuildPiece, "Bed", 1f);
        drained.Tick(0.1f);
        Check.That(drained.Tracks[1].Current == null, "an exhausted track has no current step");
        Check.That(drained.Tracks[0].Current != null, "the other track carries on");
        drained.Tick(0.1f);
        Check.That(drained.Tracks[1].Current == null, "an exhausted track stays quiet");

        // Restore seats each track independently, by id, replaying no rewards.
        var resumed = new ChallengeEngine(Pool(), new Random(46), 120f);
        var resumedEvents = new List<string>();
        resumed.SetTracks(SampleTracks());
        resumed.Completed += d => resumedEvents.Add(d.Id);
        resumed.RestoreTrack("hunt", 1, 2f, "h-2");
        resumed.RestoreTrack("craft", 1, 0f, "c-2");
        Check.That(resumed.Tracks[0].Current.Def.Id == "h-2" && resumed.Tracks[0].Current.Progress == 2f,
            "a restore seats the saved step with its progress");
        Check.That(resumed.Tracks[1].Current.Def.Id == "c-2", "each track restores independently");
        Check.That(resumedEvents.Count == 0, "a restore replays no completions");

        // An unknown track id is ignored rather than throwing — a save from a build with different
        // track names must not stop a resume.
        resumed.RestoreTrack("nonsense", 0, 0f, null);
        Check.That(resumed.Tracks.Count == 2, "an unknown track id changes nothing");

        // The single-chain API still works: it installs one track, which is what keeps every
        // pre-track test in this file meaningful.
        var legacy = new ChallengeEngine(Pool(), new Random(47), 120f);
        legacy.SetMainChain(MainChain());
        Check.That(legacy.Tracks.Count == 1, "SetMainChain installs exactly one track");
        Check.That(legacy.CurrentMainQuest != null && legacy.CurrentMainQuest.Def.Id == "mq-1",
            "CurrentMainQuest still reads the first track");
    }

    /// <summary>A hunt track and a craft track, each two steps, sharing no ids.</summary>
    static List<QuestTrack> SampleTracks() => new List<QuestTrack>
    {
        new QuestTrack
        {
            Id = "hunt", Label = "HUNT",
            Chain = new List<ChallengeDefinition>
            {
                new ChallengeDefinition { Id="h-1", MainQuest=true, Kind=ChallengeKind.KillPrefab, Param="Boar", Target=1, Display="Kill a boar" },
                new ChallengeDefinition { Id="h-2", MainQuest=true, Kind=ChallengeKind.KillPrefab, Param="Eikthyr", Target=1, Display="Defeat Eikthyr" },
            },
        },
        new QuestTrack
        {
            Id = "craft", Label = "CRAFT",
            Chain = new List<ChallengeDefinition>
            {
                new ChallengeDefinition { Id="c-1", MainQuest=true, Kind=ChallengeKind.BuildPiece, Param="Fire", Target=1, Display="Build a fire" },
                new ChallengeDefinition { Id="c-2", MainQuest=true, Kind=ChallengeKind.BuildPiece, Param="Bed", Target=1, Display="Build a bed" },
            },
        },
    };

    /// <summary>
    /// ReachBiome: "you have stood in this biome during this run". Param names a Heightmap.Biome
    /// member; the host resolves it against the visited-biome bitmask it already keeps.
    ///
    /// It exists so a questline can ask for a DESTINATION rather than a means of travel. An act
    /// that demanded a boat would hard-stall on a world where the biome is walkable, and the chain
    /// has no skip; "reach the Swamp" is true however you got there.
    /// </summary>
    static void ReachBiomeTests()
    {
        var chain = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id = "a3-arrive", MainQuest = true, Kind = ChallengeKind.ReachBiome, Param = "Swamp", Target = 1, Display = "Reach the Swamp" },
            new ChallengeDefinition { Id = "a3-boss",   MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Bonemass", Target = 1, Display = "Defeat Bonemass" },
        };

        var e = new ChallengeEngine(Pool(), new Random(31), 120f);
        e.SetMainChain(chain);

        // Param-scoped: arriving somewhere else is not arriving here.
        e.ReportMeasure(ChallengeKind.ReachBiome, "Mountain", 1f);
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest.Def.Id == "a3-arrive", "a different biome does not advance the chain");

        e.ReportMeasure(ChallengeKind.ReachBiome, "Swamp", 1f);
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest.Def.Id == "a3-boss", "reaching the named biome advances the chain");

        // The host derives this from a bitmask it only ever ORs into, so a report can never go
        // backwards — but the engine should not depend on that, and max-semantics means it doesn't.
        var e2 = new ChallengeEngine(Pool(), new Random(32), 120f);
        e2.SetMainChain(new List<ChallengeDefinition> { chain[0] });
        e2.ReportMeasure(ChallengeKind.ReachBiome, "Swamp", 1f);
        e2.ReportMeasure(ChallengeKind.ReachBiome, "Swamp", 0f);
        Check.That(e2.CurrentMainQuest.Progress == 1f, "leaving the biome does not un-earn arriving in it");

        // Wrong kind, right param: must not credit.
        var e3 = new ChallengeEngine(Pool(), new Random(33), 120f);
        e3.SetMainChain(new List<ChallengeDefinition> { chain[0] });
        e3.ReportMeasure(ChallengeKind.CollectItem, "Swamp", 1f);
        Check.That(e3.CurrentMainQuest.Progress == 0f, "a CollectItem report never credits a ReachBiome step");
    }

    /// <summary>
    /// BuildPiece as a composite SUB — "build a cooking station and a chest, and hold 20 wood" in
    /// one slot. It qualifies on the rule composites actually require (see ChallengeDefinition.Subs):
    /// an absolute-quantity measure with no per-sub Baseline. StatDelta remains excluded.
    /// </summary>
    static void BuildPieceCompositeTests()
    {
        var def = new ChallengeDefinition
        {
            Id = "cq-homestead", Target = 1, HeatReward = 3, Display = "Homestead",
            Subs = new List<SubObjective>
            {
                new SubObjective { Kind = ChallengeKind.BuildPiece, Param = "Cooking", Target = 1, Label = "Build a cooking station" },
                new SubObjective { Kind = ChallengeKind.BuildPiece, Param = "Chest",   Target = 1, Label = "Build a chest" },
                new SubObjective { Kind = ChallengeKind.CollectFood, Param = "",       Target = 5, Label = "Hold 5 food" },
            }
        };

        var e = new ChallengeEngine(new List<ChallengeDefinition> { def }, new Random(21), 120f);
        e.Tick(0.1f);
        var a = e.Active[0];
        Check.That(a.SubProgress != null && a.SubProgress.Count == 3, "a BuildPiece composite allocates its sub progress");

        // Param-scoping across subs: a cooking station must not credit the chest sub.
        e.ReportMeasure(ChallengeKind.BuildPiece, "Cooking", 1f);
        Check.That(a.SubProgress[0] == 1f, "the cooking sub is credited");
        Check.That(a.SubProgress[1] == 0f, "a cooking report leaves the chest sub alone");
        Check.That(!a.Done, "a composite with two subs outstanding is not done");

        e.ReportMeasure(ChallengeKind.BuildPiece, "Chest", 1f);
        e.ReportMeasure(ChallengeKind.CollectFood, "", 5f);
        Check.That(a.Done, "every sub met finishes the composite");

        // Latching, as for a simple BuildPiece challenge: the host stops seeing the piece and
        // reports 0, which must not walk a finished sub backwards.
        var e2 = new ChallengeEngine(new List<ChallengeDefinition> { def }, new Random(22), 120f);
        e2.Tick(0.1f);
        e2.ReportMeasure(ChallengeKind.BuildPiece, "Cooking", 1f);
        e2.ReportMeasure(ChallengeKind.BuildPiece, "Cooking", 0f);
        Check.That(e2.Active[0].SubProgress[0] == 1f, "a sub does not un-earn when the report drops to zero");

        // A kill report must not touch BuildPiece subs, and vice versa.
        var e3 = new ChallengeEngine(new List<ChallengeDefinition> { def }, new Random(23), 120f);
        e3.Tick(0.1f);
        e3.ReportKill("Cooking");
        Check.That(e3.Active[0].SubProgress[0] == 0f, "a kill report never credits a BuildPiece sub");
    }

    /// <summary>The v1-shaped main chain: a stat-delta step, then two kill steps.</summary>
    static List<ChallengeDefinition> MainChain() => new List<ChallengeDefinition>
    {
        new ChallengeDefinition { Id="mq-1", MainQuest=true, Kind=ChallengeKind.StatDelta,  Param="CraftsOrUpgrades", Target=1, Display="Craft an axe",  RewardText="Bow + 40 arrows" },
        new ChallengeDefinition { Id="mq-2", MainQuest=true, Kind=ChallengeKind.KillPrefab, Param="Deer",             Target=3, Display="Kill 3 Deer",   RewardText="Leather armor" },
        new ChallengeDefinition { Id="mq-3", MainQuest=true, Kind=ChallengeKind.KillPrefab, Param="Eikthyr",          Target=1, Display="Defeat Eikthyr", RewardText="Antler pickaxe" },
    };

    /// <summary>
    /// The main-quest chain: one step at a time in a reserved slot beside the three random ones,
    /// dealt in order, advanced by completion, immune to the tier ceiling / external filter /
    /// reroll, and round-tripping through RestoreMainQuest without replaying its rewards.
    /// </summary>
    static void MainQuestTests()
    {
        Check.That(!new ChallengeDefinition().MainQuest, "MainQuest defaults to false");
        Check.That(new ChallengeDefinition().RewardText == null, "RewardText defaults to null");

        var e = new ChallengeEngine(Pool(), new Random(211), 120f);
        Check.That(e.CurrentMainQuest == null, "no chain set: no main quest");

        e.SetMainChain(MainChain());
        Check.That(e.CurrentMainQuest != null && e.CurrentMainQuest.Def.Id == "mq-1",
            "the chain deals its first step immediately");
        Check.That(e.MainQuestIndex == 0, "a fresh chain sits at index 0");
        Check.That(e.CurrentMainQuest.Def.RewardText == "Bow + 40 arrows", "the step carries its reward copy");

        // The reserved slot is genuinely extra: three random slots still fill beside it.
        e.Tick(0.1f);
        Check.That(e.Active.Count == 3, "random slots still fill to 3 alongside the chain");
        Check.That(e.Active.All(a => !a.Def.MainQuest), "no chain step leaked into the random slots");
        Check.That(e.CurrentMainQuest.Def.Id == "mq-1", "the chain step survives a tick untouched");

        // Step 1 is a StatDelta, addressed by the reserved slot index (it has its own baseline).
        var completed = new List<string>();
        e.Completed += d => completed.Add(d.Id);
        e.ReportSlotMeasure(ChallengeEngine.MainQuestSlot, 1f);
        Check.That(e.CurrentMainQuest.Progress == 1f, "ReportSlotMeasure credits the reserved slot");
        e.Tick(0.1f);
        Check.That(completed.Count == 1 && completed[0] == "mq-1", "completing a step fires Completed with its def");
        Check.That(e.MainQuestIndex == 1 && e.CurrentMainQuest.Def.Id == "mq-2", "completion advances to the next step");
        Check.That(e.Active.Count == 3, "a chain completion does not vacate a random slot");

        // Step 2 is a KillPrefab and takes progress from the ordinary kill report.
        e.ReportKill("Boar");
        Check.That(e.CurrentMainQuest.Progress == 0f, "a non-matching kill leaves the chain step alone");
        e.ReportKill("Deer");
        e.ReportKill("Deer");
        Check.That(e.CurrentMainQuest.Progress == 2f, "ReportKill credits a KillPrefab chain step");
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest.Def.Id == "mq-2", "a part-done step does not advance");
        e.ReportKill("Deer");
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest.Def.Id == "mq-3", "the third kill advances the chain");

        // Exhaustion.
        e.ReportKill("Eikthyr");
        e.Tick(0.1f);
        Check.That(e.CurrentMainQuest == null, "an exhausted chain has no current step");
        Check.That(e.MainQuestIndex == 3, "the index ends past the last step");
        Check.That(completed.SequenceEqual(new[] { "mq-1", "mq-2", "mq-3" }), "every step fired once, in order");
        e.Tick(200f);
        Check.That(e.CurrentMainQuest == null && completed.Count == 3, "an exhausted chain stays quiet");

        // Gating and rerolls cannot touch the chain.
        var gated = new ChallengeEngine(Pool(), new Random(223), 120f);
        gated.MaxTier = 0;
        gated.ExternalFilter = _ => false;   // nothing at all is drawable
        var highTier = MainChain();
        foreach (var d in highTier) d.Tier = 4;
        gated.SetMainChain(highTier);
        gated.Tick(0.1f);
        Check.That(gated.Active.Count == 0, "the filter really does block every random draw");
        Check.That(gated.CurrentMainQuest != null && gated.CurrentMainQuest.Def.Id == "mq-1",
            "the chain ignores MaxTier and ExternalFilter");

        var rr = new ChallengeEngine(Pool(), new Random(227), 120f);
        rr.SetMainChain(MainChain());
        rr.Tick(0.1f);
        Check.That(rr.Reroll(0), "a random slot still rerolls with a chain installed");
        Check.That(rr.CurrentMainQuest.Def.Id == "mq-1", "rerolling a random slot leaves the chain step in place");
        Check.That(rr.Active.All(a => !a.Def.MainQuest), "a reroll can never draw a chain step");
        rr.Reroll(ChallengeEngine.MainQuestSlot);
        Check.That(rr.CurrentMainQuest.Def.Id == "mq-1", "the reserved slot index is not rerollable");

        // RestoreMainQuest round-trips a position + part-progress and replays no rewards.
        var resumed = new ChallengeEngine(Pool(), new Random(229), 120f);
        var resumedCompletions = new List<string>();
        resumed.Completed += d => resumedCompletions.Add(d.Id);
        resumed.SetMainChain(MainChain());
        resumed.RestoreMainQuest(1, 2f, "mq-2");
        Check.That(resumed.MainQuestIndex == 1 && resumed.CurrentMainQuest.Def.Id == "mq-2",
            "RestoreMainQuest seats the saved step");
        Check.That(resumed.CurrentMainQuest.Progress == 2f, "RestoreMainQuest keeps the saved progress");

        // The id wins over the index, so a chain that GREW or was REORDERED between builds still
        // resumes the player on the step they were actually working on.
        var moved = new ChallengeEngine(Pool(), new Random(241), 120f);
        moved.SetMainChain(new List<ChallengeDefinition>
        {
            MainChain()[0],
            new ChallengeDefinition { Id="mq-new", MainQuest=true, Kind=ChallengeKind.StatDelta, Param="Builds", Target=1, Display="inserted step" },
            MainChain()[1],
            MainChain()[2],
        });
        moved.RestoreMainQuest(1, 2f, "mq-2");
        Check.That(moved.MainQuestIndex == 2 && moved.CurrentMainQuest.Def.Id == "mq-2",
            "a saved id follows its step to a new position in a reordered chain");
        Check.That(moved.CurrentMainQuest.Progress == 2f, "progress follows the step, not the index");

        // A save with no id predates the field, so its index means nothing here: seat the step but
        // drop the progress, rather than credit an objective the player never worked on. (A saved
        // progress of 2 against an inserted target-1 step would otherwise complete instantly.)
        var legacy = new ChallengeEngine(Pool(), new Random(251), 120f);
        var legacyCompletions = new List<string>();
        legacy.Completed += d => legacyCompletions.Add(d.Id);
        legacy.SetMainChain(new List<ChallengeDefinition>
        {
            MainChain()[0],
            new ChallengeDefinition { Id="mq-new", MainQuest=true, Kind=ChallengeKind.StatDelta, Param="Builds", Target=1, Display="inserted step" },
            MainChain()[1],
            MainChain()[2],
        });
        legacy.RestoreMainQuest(1, 2f, null);
        Check.That(legacy.CurrentMainQuest.Progress == 0f, "a save with no step id restores at zero progress");
        legacy.Tick(0.1f);
        Check.That(legacyCompletions.Count == 0, "a legacy restore cannot fire an unearned completion");

        // An id that no longer exists at all falls back the same way.
        var gone = new ChallengeEngine(Pool(), new Random(257), 120f);
        gone.SetMainChain(MainChain());
        gone.RestoreMainQuest(1, 2f, "mq-deleted");
        Check.That(gone.CurrentMainQuest.Def.Id == "mq-2" && gone.CurrentMainQuest.Progress == 0f,
            "a step id that no longer exists falls back to the index with zero progress");
        resumed.Tick(0.1f);
        Check.That(resumedCompletions.Count == 0, "restoring past earlier steps fires no completions");
        resumed.ReportKill("Deer");
        resumed.Tick(0.1f);
        Check.That(resumedCompletions.SequenceEqual(new[] { "mq-2" }), "the restored step still completes normally");

        // Out-of-range and negative indices are the exhausted/start states, not a throw.
        var edge = new ChallengeEngine(Pool(), new Random(233), 120f);
        edge.SetMainChain(MainChain());
        edge.RestoreMainQuest(99, 0f);
        Check.That(edge.CurrentMainQuest == null, "an index past the chain restores as exhausted");
        edge.RestoreMainQuest(-4, 0f);
        Check.That(edge.MainQuestIndex == 0 && edge.CurrentMainQuest.Def.Id == "mq-1",
            "a negative index clamps to the first step");
        edge.MainQuestIndex = 2;
        Check.That(edge.CurrentMainQuest.Def.Id == "mq-3" && edge.CurrentMainQuest.Progress == 0f,
            "the MainQuestIndex setter re-seats the step with zero progress");

        // A null/empty chain is simply "no questline".
        var none = new ChallengeEngine(Pool(), new Random(239), 120f);
        none.SetMainChain(null);
        none.Tick(0.1f);
        Check.That(none.CurrentMainQuest == null && none.Active.Count == 3,
            "a null chain leaves an otherwise ordinary engine");
        none.ReportKill("Deer");
        none.ReportSlotMeasure(ChallengeEngine.MainQuestSlot, 5f);
        Check.That(none.CurrentMainQuest == null, "reporting into an absent chain is a no-op");
    }

    /// <summary>A composite (two subs: a KillPrefab and a CollectItem) plus a plain, non-composite definition.</summary>
    static List<ChallengeDefinition> CompositePool() => new List<ChallengeDefinition>
    {
        new ChallengeDefinition
        {
            Id = "cx-a", Kind = ChallengeKind.KillPrefab, Param = "unused", Target = 99, HeatReward = 1, Display = "cx-a",
            Subs = new List<SubObjective>
            {
                new SubObjective { Kind = ChallengeKind.KillPrefab,  Param = "Boar",       Target = 2, Label = "Kill 2 Boar" },
                new SubObjective { Kind = ChallengeKind.CollectItem, Param = "$item_wood", Target = 5, Label = "Hold 5 Wood" },
            }
        },
    };

    /// <summary>
    /// Composite (multi-objective) challenges: Done requires every sub, ReportKill/ReportMeasure
    /// credit only matching subs (capped at each sub's own target), sub progress round-trips
    /// through RestoreActive, and a plain single-objective challenge is untouched by any of it.
    /// </summary>
    static void CompositeTests()
    {
        var e = new ChallengeEngine(CompositePool(), new Random(71), 120f);
        e.RestoreActive(new[] { new KeyValuePair<string, float>("cx-a", 0f) });
        Check.That(e.Active.Count == 1, "composite restores into its slot");

        var a = e.Active[0];
        Check.That(a.SubProgress != null && a.SubProgress.Count == 2 && a.SubProgress.All(v => v == 0f),
            "a restored composite with no saved sub-progress starts every sub at zero");
        Check.That(!a.Done, "a fresh composite is not done");

        // ReportKill credits only the matching sub, incrementing and capping at ITS target.
        e.ReportKill("Wolf");
        Check.That(a.SubProgress[0] == 0f, "a non-matching kill does not touch a composite sub");
        e.ReportKill("Boar");
        Check.That(a.SubProgress[0] == 1f, "a matching kill credits its sub");
        e.ReportKill("Boar");
        e.ReportKill("Boar"); // one past the sub's target
        Check.That(a.SubProgress[0] == 2f, "a KillPrefab sub caps at its own target");
        Check.That(!a.Done, "one sub complete is not the whole composite");

        // CollectItem sub matches by Param, with the usual max-semantics.
        e.ReportMeasure(ChallengeKind.CollectItem, "$item_stone", 99f);
        Check.That(a.SubProgress[1] == 0f, "a CollectItem report for a different Param does not touch the sub");
        e.ReportMeasure(ChallengeKind.CollectItem, "$item_wood", 3f);
        Check.That(a.SubProgress[1] == 3f, "a matching CollectItem report credits the sub");
        e.ReportMeasure(ChallengeKind.CollectItem, "$item_wood", 1f);
        Check.That(a.SubProgress[1] == 3f, "a CollectItem sub's progress never regresses");
        e.ReportMeasure(ChallengeKind.CollectItem, "$item_wood", 5f);
        Check.That(a.SubProgress[1] == 5f, "reaching the sub's target completes it");

        Check.That(a.Done, "composite is done once every sub reaches its own target");

        // A plain, non-composite challenge is untouched by any of the composite plumbing above.
        var simplePool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id = "simple", Kind = ChallengeKind.KillPrefab, Param = "Boar", Target = 2, HeatReward = 1, Display = "Kill 2 Boar" },
        };
        var s = new ChallengeEngine(simplePool, new Random(73), 120f);
        s.Tick(0.1f);
        Check.That(s.Active[0].SubProgress == null, "a simple challenge's SubProgress is null");
        s.ReportKill("Boar");
        s.ReportKill("Boar");
        Check.That(s.Active[0].Progress == 2f && s.Active[0].Done,
            "a simple challenge's own Progress/Done work exactly as before");

        CompositeRoundTripTests();
    }

    /// <summary>Sub progress survives RestoreActive's third argument the same way baselines do — SAVED-sequence indexed, malformed input never throws.</summary>
    static void CompositeRoundTripTests()
    {
        var e = new ChallengeEngine(CompositePool(), new Random(79), 120f);
        e.RestoreActive(
            new[] { new KeyValuePair<string, float>("cx-a", 0f) },
            new List<float> { float.NaN },
            new List<List<float>> { new List<float> { 2f, 4f } });

        Check.That(e.Active.Count == 1, "restore with sub-progress fills the slot");
        Check.That(e.Active[0].SubProgress[0] == 2f && e.Active[0].SubProgress[1] == 4f,
            "sub progress restores alongside the slot");

        // Round trip: what a save would write back out is what came in.
        var savedSubs = new List<List<float>> { e.Active[0].SubProgress.ToList() };
        var e2 = new ChallengeEngine(CompositePool(), new Random(79), 120f);
        e2.RestoreActive(
            new[] { new KeyValuePair<string, float>("cx-a", 0f) }, null, savedSubs);
        Check.That(e2.Active[0].SubProgress[0] == 2f && e2.Active[0].SubProgress[1] == 4f,
            "sub progress survives a save/restore round trip");

        // A short saved list pads the uncovered tail with zero rather than throwing.
        var shortList = new ChallengeEngine(CompositePool(), new Random(83), 120f);
        shortList.RestoreActive(
            new[] { new KeyValuePair<string, float>("cx-a", 0f) },
            null,
            new List<List<float>> { new List<float> { 7f } });
        Check.That(shortList.Active[0].SubProgress[0] == 7f && shortList.Active[0].SubProgress[1] == 0f,
            "a short saved sub-progress list leaves the uncovered tail at zero");

        // No sub-progress list at all (pre-composite save) restarts every sub at zero.
        var legacy = new ChallengeEngine(CompositePool(), new Random(89), 120f);
        legacy.RestoreActive(new[] { new KeyValuePair<string, float>("cx-a", 5f) });
        Check.That(legacy.Active[0].SubProgress.All(v => v == 0f),
            "restoring with no sub-progress list restarts every composite sub at zero");

        // A dropped entry (unknown id) must not shift the sub-progress index of what follows it.
        var skewed = new ChallengeEngine(CompositePool(), new Random(97), 120f);
        skewed.RestoreActive(
            new[]
            {
                new KeyValuePair<string, float>("nonsense", 0f),
                new KeyValuePair<string, float>("cx-a", 0f),
            },
            null,
            new List<List<float>> { new List<float> { 999f }, new List<float> { 1f, 2f } });
        Check.That(skewed.Active.Count == 1 && skewed.Active[0].Def.Id == "cx-a", "the unknown id is dropped");
        Check.That(skewed.Active[0].SubProgress[0] == 1f && skewed.Active[0].SubProgress[1] == 2f,
            "sub-progress index follows the saved sequence past a dropped entry");
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

    static void BiomeFilterTests()
    {
        // Two quests: one anywhere (Biomes=0), one gated to biome bit 8.
        var pool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="bf-any",   Kind=ChallengeKind.ReachAltitude, Param="", Target=10, HeatReward=1, Display="any", Biomes=0 },
            new ChallengeDefinition { Id="bf-gated", Kind=ChallengeKind.ReachAltitude, Param="", Target=10, HeatReward=1, Display="gated", Biomes=8 },
        };
        int visited = 1; // only biome bit 1 seen
        var e = new ChallengeEngine(pool, new System.Random(5), 1f);
        e.ExternalFilter = d => d.Biomes == 0 || (d.Biomes & visited) != 0;
        e.Tick(0.1f);
        Check.That(e.Active.Count == 1 && e.Active[0].Def.Id == "bf-any", "gated quest not dealt before its biome is visited");

        visited |= 8; // player reaches the gated biome
        e.Tick(2f);   // past refill cooldown-less initial fill: force draws
        e.Tick(2f);
        bool gatedNow = false;
        foreach (var a in e.Active) if (a.Def.Id == "bf-gated") gatedNow = true;
        Check.That(gatedNow, "gated quest dealt once its biome is visited");
    }

    /// <summary>
    /// The standing task: seated by Opener, never vacated, pays out every time it is filled.
    /// </summary>
    static void RepeatableTests()
    {
        var pool = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id="standing", Opener=true, Repeatable=true, Kind=ChallengeKind.StatDelta, Param="RavenTalk", Target=5, HeatReward=1, Display="Heed Hugin 5 times" },
            new ChallengeDefinition { Id="filler1", Kind=ChallengeKind.ReachAltitude, Param="", Target=10, HeatReward=1, Display="filler1" },
            new ChallengeDefinition { Id="filler2", Kind=ChallengeKind.BuildHeight, Param="", Target=10, HeatReward=1, Display="filler2" },
        };
        var e = new ChallengeEngine(pool, new System.Random(7), refillCooldownSeconds: 120f);
        int paid = 0;
        e.Completed += d => { if (d.Id == "standing") paid++; };

        e.Tick(0.1f);
        int slot = e.Active.ToList().FindIndex(a => a.Def.Id == "standing");
        Check.That(slot >= 0, "repeatable opener is seated on a fresh engine");

        e.ReportSlotMeasure(slot, 5f);
        e.Tick(0.1f);
        var standing = e.Active.FirstOrDefault(a => a.Def.Id == "standing");
        Check.That(paid == 1, "repeatable pays out when filled");
        Check.That(standing != null, "repeatable keeps its slot after paying out");
        Check.That(standing.Progress == 0f, "repeatable restarts at zero");
        Check.That(float.IsNaN(standing.Baseline),
            "repeatable re-arms its stat baseline, so the next round measures from here");

        // Second fill in the same slot: proves it is a faucet, not a one-off.
        slot = e.Active.ToList().FindIndex(a => a.Def.Id == "standing");
        e.ReportSlotMeasure(slot, 5f);
        e.Tick(0.1f);
        Check.That(paid == 2, "repeatable pays out again on the next fill");

        // It must never be drawn a second time, and never rerolled away.
        Check.That(e.Active.Count(a => a.Def.Id == "standing") == 1, "only ever one copy of the standing task");
        Check.That(!e.Reroll(e.Active.ToList().FindIndex(a => a.Def.Id == "standing")),
            "the standing task refuses to be rerolled away");
    }
}
