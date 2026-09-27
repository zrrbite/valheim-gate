using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The ladder that paces a way's kit, and the validator that keeps the class table and the boon
/// pool agreeing. Both fail silently in play when wrong — a rung that grants nothing looks exactly
/// like a rung not yet due — so they are pinned here.
/// </summary>
static class ClassLadderTests
{
    /// <summary>
    /// A synthetic pool mirroring the real one's class tagging: the nine migrated boons, the four
    /// new ones, and a couple of general boons that must not trouble the validator.
    /// </summary>
    static List<BoonDefinition> Pool() => new List<BoonDefinition>
    {
        new BoonDefinition { Id = "fleet" },
        new BoonDefinition { Id = "wind", CooldownSeconds = 120f },
        new BoonDefinition { Id = "hunter", ClassId = "hunter", IsPassive = true },
        new BoonDefinition { Id = "shepherd", ClassId = "hunter", IsPassive = true },
        new BoonDefinition { Id = "brother", ClassId = "hunter" },
        new BoonDefinition { Id = "menagerie", ClassId = "hunter" },
        new BoonDefinition { Id = "unseen", ClassId = "hunter" },
        new BoonDefinition { Id = "hearthlight", ClassId = "volva", IsPassive = true },
        new BoonDefinition { Id = "shaman", ClassId = "volva" },
        new BoonDefinition { Id = "bonecaller", ClassId = "volva" },
        new BoonDefinition { Id = "wrath", ClassId = "volva" },
        new BoonDefinition { Id = "warrior", ClassId = "berserker", IsPassive = true },
        new BoonDefinition { Id = "rend", ClassId = "berserker" },
        new BoonDefinition { Id = "rage", ClassId = "berserker" },
        new BoonDefinition { Id = "warcry", ClassId = "berserker" },
    };

    public static void Run()
    {
        var hunter = ClassLadder.Find("hunter");
        Check.That(hunter != null && hunter.Title == "Eydís", "the Hunter is found, and is Eydís's way");

        var none = new string[0];
        Check.That(ClassLadder.Due(hunter, 0, none).SequenceEqual(new[] { "hunter", "shepherd", "brother" }),
            "at the choice: the passives and rung 1, passives first");
        Check.That(ClassLadder.Due(hunter, 1, none).SequenceEqual(new[] { "hunter", "shepherd", "brother", "menagerie" }),
            "one god down adds rung 2");
        Check.That(ClassLadder.Due(hunter, 2, none).Count() == 4, "two is still only rung 2");
        Check.That(ClassLadder.Due(hunter, 3, none).Last() == "unseen" && ClassLadder.Due(hunter, 3, none).Count() == 5,
            "three down adds rung 3");

        var learned = new[] { "hunter", "shepherd", "brother" };
        Check.That(!ClassLadder.Due(hunter, 0, learned).Any(), "nothing is due twice");
        Check.That(ClassLadder.Due(hunter, 1, learned).SequenceEqual(new[] { "menagerie" }),
            "only the unlearned rung is due once Eikthyr falls");

        Check.That(ClassLadder.NextThreshold(hunter, 0, learned) == 1, "next threshold after the choice is one boss");
        Check.That(ClassLadder.NextThreshold(hunter, 1, learned) == 3, "then three");
        Check.That(ClassLadder.NextThreshold(hunter, 3, learned) == null, "and then the ladder is spent");

        Check.That(ClassLadder.Find("skald") == null, "an unknown way is not found");
        Check.That(!ClassLadder.Due(null, 5, none).Any(), "and owes nothing");
        Check.That(ClassLadder.NextThreshold(null, 0, none) == null, "and has no next threshold");
        Check.That(ClassLadder.Find(null) == null, "a null id finds nothing rather than throwing");

        var catalog = ClassLadder.Catalog();
        Check.That(catalog.Count == 3, "v1 has three ways");
        Check.That(catalog.Select(c => c.Id).Distinct().Count() == catalog.Count, "way ids are distinct");
        Check.That(catalog.All(c => c.Rungs.Length == ClassLadder.Thresholds.Length),
            "every way has one rung per threshold");

        var problems = ClassLadder.Validate(catalog, Pool()).ToList();
        foreach (var p in problems) Console.WriteLine("       " + p);
        Check.That(problems.Count == 0, "the catalog agrees with a pool tagged like the real one");

        var missing = Pool().Where(b => b.Id != "wrath").ToList();
        Check.That(ClassLadder.Validate(catalog, missing).Any(p => p.Contains("wrath")),
            "a rung id missing from the pool is caught");

        var stray = Pool();
        stray.Add(new BoonDefinition { Id = "seidr", ClassId = "volva" });
        Check.That(ClassLadder.Validate(catalog, stray).Any(p => p.Contains("seidr")),
            "a class-tagged boon its class never teaches is caught");

        var wrongTag = Pool();
        wrongTag.First(b => b.Id == "rend").ClassId = "hunter";
        Check.That(ClassLadder.Validate(catalog, wrongTag).Any(p => p.Contains("rend")),
            "a boon tagged for the wrong way is caught");

        var shared = ClassLadder.Catalog().ToList();
        shared[2].Rungs[0] = new[] { "brother" };
        Check.That(ClassLadder.Validate(shared, Pool()).Any(p => p.Contains("claimed by both")),
            "an id two ways both teach is caught");
    }
}
