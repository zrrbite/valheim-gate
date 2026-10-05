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
        new BoonDefinition { Id = "elemental", ClassId = "hunter" },
        new BoonDefinition { Id = "unseen", ClassId = "hunter" },
        new BoonDefinition { Id = "hearthlight", ClassId = "volva", IsPassive = true },
        new BoonDefinition { Id = "shaman", ClassId = "volva" },
        new BoonDefinition { Id = "bonecaller", ClassId = "volva" },
        new BoonDefinition { Id = "wrath", ClassId = "volva" },
        new BoonDefinition { Id = "warrior", ClassId = "berserker", IsPassive = true },
        new BoonDefinition { Id = "rend", ClassId = "berserker" },
        new BoonDefinition { Id = "rage", ClassId = "berserker" },
        new BoonDefinition { Id = "warcry", ClassId = "berserker" },

        // The four of 2026-09-28.
        new BoonDefinition { Id = "hirdman", ClassId = "huskarl", IsPassive = true },
        new BoonDefinition { Id = "bash", ClassId = "huskarl" },
        new BoonDefinition { Id = "bulwark", ClassId = "huskarl" },
        new BoonDefinition { Id = "laststand", ClassId = "huskarl" },
        new BoonDefinition { Id = "poet", ClassId = "skald", IsPassive = true },
        new BoonDefinition { Id = "march", ClassId = "skald" },
        new BoonDefinition { Id = "warsong", ClassId = "skald" },
        new BoonDefinition { Id = "bragi", ClassId = "skald" },
        new BoonDefinition { Id = "seafarer", ClassId = "saefari", IsPassive = true },
        new BoonDefinition { Id = "tide", ClassId = "saefari" },
        new BoonDefinition { Id = "fairwind", ClassId = "saefari" },
        new BoonDefinition { Id = "sealegs", ClassId = "saefari" },
        new BoonDefinition { Id = "craftsman", ClassId = "smidr", IsPassive = true },
        new BoonDefinition { Id = "fieldforge", ClassId = "smidr" },
        new BoonDefinition { Id = "mastersminute", ClassId = "smidr" },
        new BoonDefinition { Id = "reinforce", ClassId = "smidr" },
    };

    public static void Run()
    {
        var hunter = ClassLadder.Find("hunter");
        Check.That(hunter != null && hunter.Title == "Eydís", "the Hunter is found, and is Eydís's way");

        var none = new string[0];
        Check.That(ClassLadder.Due(hunter, 0, none).SequenceEqual(new[] { "hunter", "shepherd", "brother" }),
            "at the choice: the passives and rung 1, passives first");
        Check.That(ClassLadder.Due(hunter, 1, none).SequenceEqual(new[] { "hunter", "shepherd", "brother", "menagerie", "elemental" }),
            "one god down adds rung 2: Menagerie and Elemental Arrows, in that order");
        Check.That(ClassLadder.Due(hunter, 2, none).Last() == "unseen" && ClassLadder.Due(hunter, 2, none).Count() == 6,
            "two down adds rung 3");
        Check.That(ClassLadder.Due(hunter, 3, none).Count() == 6, "three down adds nothing more");

        var learned = new[] { "hunter", "shepherd", "brother" };
        Check.That(!ClassLadder.Due(hunter, 0, learned).Any(), "nothing is due twice");
        Check.That(ClassLadder.Due(hunter, 1, learned).SequenceEqual(new[] { "menagerie", "elemental" }),
            "only the unlearned rung is due once Eikthyr falls - both of its boons");
        Check.That(ClassLadder.Due(hunter, 1, learned.Concat(new[] { "menagerie" })).SequenceEqual(new[] { "elemental" }),
            "a rung half learned owes only its other half");

        Check.That(ClassLadder.NextThreshold(hunter, 0, learned) == 1, "next threshold after the choice is one boss");
        Check.That(ClassLadder.NextThreshold(hunter, 1, learned) == 2, "then two");
        Check.That(ClassLadder.NextThreshold(hunter, 2, learned) == null, "and then the ladder is spent");

        Check.That(ClassLadder.Find("jarl") == null, "an unknown way is not found");
        Check.That(!ClassLadder.Due(null, 5, none).Any(), "and owes nothing");
        Check.That(ClassLadder.NextThreshold(null, 0, none) == null, "and has no next threshold");
        Check.That(ClassLadder.Find(null) == null, "a null id finds nothing rather than throwing");

        var catalog = ClassLadder.Catalog();
        Check.That(catalog.Count == 7, "the thane names seven ways");
        Check.That(catalog.Select(c => c.Id).Distinct().Count() == catalog.Count, "way ids are distinct");
        Check.That(catalog.Select(c => c.Title).Distinct().Count() == catalog.Count, "and seven different graves");
        Check.That(catalog.Select(c => c.Id).SequenceEqual(new[] { "hunter", "volva", "berserker", "huskarl", "skald", "saefari", "smidr" }),
            "in card order: the first three, then Halvard, Ormr, Ragna, Dvalinn");
        Check.That(catalog.All(c => !string.IsNullOrEmpty(c.Description) && c.PassiveBoonIds.Length > 0),
            "every way has a card and a passive");

        var huskarl = ClassLadder.Find("huskarl");
        Check.That(huskarl != null && huskarl.Title == "Halvard" && huskarl.Display == "Húskarl", "the Húskarl is Halvard's way");
        Check.That(ClassLadder.Due(huskarl, 0, none).SequenceEqual(new[] { "hirdman", "bash" }),
            "the Húskarl starts with Hirdman and Shield Bash");
        Check.That(ClassLadder.Due(huskarl, 3, none).SequenceEqual(new[] { "hirdman", "bash", "bulwark", "laststand" }),
            "and the wall and the last stand come with the gods");
        Check.That(ClassLadder.Find("skald").Title == "Ormr" && ClassLadder.Find("saefari").Title == "Ragna" &&
                   ClassLadder.Find("smidr").Title == "Dvalinn", "Ormr, Ragna and Dvalinn keep their graves");
        Check.That(ClassLadder.Find("saefari").Display == "Sæfari" && ClassLadder.Find("smidr").Display == "Smiðr",
            "and their names keep their letters");
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

        // What a way hands over at the graves. Only the Berserker gives something to hold, and
        // exactly one pair: TakeUpWay grants these once, so a count here is a count in the pack.
        var berserker = ClassLadder.Find("berserker");
        Check.That(berserker.GrantItems.Length == 1 &&
                   berserker.GrantItems[0].prefab == "Saga_UlfrsAxes" && berserker.GrantItems[0].count == 1,
            "the Berserker is handed one pair of Ulfr's axes");
        Check.That(ClassLadder.Find("hunter").GrantItems.Length == 0 && ClassLadder.Find("volva").GrantItems.Length == 0,
            "the Hunter and the Völva are handed nothing");
        Check.That(hunter.Description.EndsWith(" Later your arrows learn fire and frost."),
            "the Hunter's card says her arrows learn fire and frost");
        Check.That(hunter.Description.Contains(" You move quietly. Later your arrows"),
            "the Hunter's card says she moves quietly, before the arrows");

        var noElemental = Pool().Where(b => b.Id != "elemental").ToList();
        Check.That(ClassLadder.Validate(catalog, noElemental).Any(p => p.Contains("elemental")),
            "Elemental Arrows missing from the pool is caught");
        Check.That(berserker.Description.EndsWith(" Ulfr’s axes are yours the moment you take his name."),
            "and the card says so");
        Check.That(new ClassDefinition().GrantItems != null, "a way built without gifts has an empty list, not null");

        // The four new ways each hand something over, and their cards say so.
        Check.That(huskarl.GrantItems.SequenceEqual(new[] { ("ShieldWood", 1), ("SpearFlint", 1) }),
            "the Húskarl is handed a wooden shield and a flint spear");
        Check.That(ClassLadder.Find("skald").GrantItems.SequenceEqual(new[] { ("MeadHealthMinor", 3) }),
            "the Skald is handed three minor healing meads");
        Check.That(ClassLadder.Find("saefari").GrantItems.SequenceEqual(new[] { ("SpearChitin", 1) }),
            "the Sæfari is handed the abyssal harpoon");
        Check.That(ClassLadder.Find("smidr").GrantItems.SequenceEqual(new[] { ("Hoe", 1), ("Cultivator", 1) }),
            "the Smiðr is handed a hoe and a cultivator");
        Check.That(new[] { "huskarl", "skald", "saefari", "smidr" }
                .All(id => ClassLadder.Find(id).Description.EndsWith(" the moment you take his name.") ||
                           ClassLadder.Find(id).Description.EndsWith(" the moment you take her name.")),
            "and each card says the gift is yours at the choice");

        // What the HUD says a rung waits on - read from the thresholds, so moving one keeps it true.
        Check.That(ClassLadder.AfterLine(ClassLadder.Thresholds[1]) == "after Eikthyr", "rung 2 waits on Eikthyr");
        Check.That(ClassLadder.AfterLine(ClassLadder.Thresholds[2]) == "after the Elder", "rung 3 waits on the Elder");
        Check.That(ClassLadder.AfterLine(2) == "after the Elder" && ClassLadder.AfterLine(5) == "after Yagluth",
            "the table covers the gods between");
        Check.That(ClassLadder.AfterLine(9) == "after 9 gods", "a count past the table falls back to a number");

        // The way's skill in the Skills window mirrors the ladder: 0 / 33 / 66 / 100.
        Check.That(ClassLadder.MirrorLevel(null, new[] { "brother" }) == 0, "no way held: the mirror reads 0");
        Check.That(ClassLadder.MirrorLevel(hunter, none) == 0, "a way with nothing learned reads 0");
        Check.That(ClassLadder.MirrorLevel(hunter, new[] { "hunter", "shepherd" }) == 0,
            "passives alone do not move the mirror");
        Check.That(ClassLadder.MirrorLevel(hunter, new[] { "hunter", "shepherd", "brother" }) == 33,
            "rung 1 learned reads 33");
        Check.That(ClassLadder.MirrorLevel(hunter, new[] { "brother", "menagerie" }) == 33,
            "a rung is learned only when all its boons are held (Menagerie without Elemental Arrows)");
        Check.That(ClassLadder.MirrorLevel(hunter, new[] { "brother", "menagerie", "elemental" }) == 66,
            "rung 2 learned reads 66, floored rather than rounded");
        Check.That(ClassLadder.MirrorLevel(hunter, new[] { "brother", "menagerie", "elemental", "unseen" }) == 100,
            "the whole way reads 100");
        Check.That(ClassLadder.MirrorLevel(ClassLadder.Find("volva"), new[] { "brother", "menagerie", "elemental", "unseen" }) == 0,
            "another way's boons do not count toward this one");
    }
}
