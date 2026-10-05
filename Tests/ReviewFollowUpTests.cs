using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// What the Acts V-VII review asked for after the fact: the FORGE page keeping the anvil's secrets,
/// Thor's bow credited by the bow rather than by its lightning, and the run-start self-check.
/// </summary>
static class ReviewFollowUpTests
{
    public static void Run()
    {
        ForgeRevealTests();
        StormBowCreditTests();
        SelfCheckTests();
    }

    static void ForgeRevealTests()
    {
        Func<string, bool> told = id => id == "mq-bow";

        Check.That(!ForgeReveal.ShowAnvilCard("Saga_Ironbound", new[] { "Saga_Ironbound" }, null, told),
                   "a shield's repair is no recipe - it never gets a card, which is how the Ironbound leaked into Act I");
        Check.That(!ForgeReveal.ShowAnvilCard("Saga_Stormward", new[] { "Saga_Stormward" }, "mq-bow", told),
                   "a repair stays off the page even when a teaching step is named and reached");
        Check.That(ForgeReveal.IsRepair("Saga_Stormward", new[] { "Saga_Stormward" }) &&
                   !ForgeReveal.IsRepair("Saga_Stormward", new[] { "Wood", "Resin" }),
                   "a repair is exactly the result alone in the box");
        Check.That(ForgeReveal.ShowAnvilCard("Saga_ThorsBow", new[] { "Wood", "Flint" }, "mq-bow", told),
                   "a shape is shown once the step that teaches it has opened");
        Check.That(!ForgeReveal.ShowAnvilCard("Saga_Stormward", new[] { "Wood", "TrollHide" }, "mq-shield", told),
                   "a shape whose step has not opened stays secret");
        Check.That(!ForgeReveal.ShowAnvilCard("Saga_Something", new[] { "Wood" }, null, told),
                   "a shape nobody teaches is never shown - a new conversion defaults to secret, not to a spoiler");
    }

    static void StormBowCreditTests()
    {
        Check.That(StormBowCredit.Credits(true, true, 22f, float.PositiveInfinity),
                   "lightning from the player's bow is Thor's, as before");
        Check.That(StormBowCredit.Credits(true, true, 0f, 1.5f),
                   "a fire or frost arrow counts when a storm arrow left the string moments ago");
        Check.That(!StormBowCredit.Credits(true, true, 0f, float.PositiveInfinity),
                   "a plain bow's kill with no storm arrow in flight does not count");
        Check.That(!StormBowCredit.Credits(true, true, 0f, StormBowCredit.ArrowSeconds + 0.1f),
                   "a storm arrow long gone does not credit a later kill");
        Check.That(!StormBowCredit.Credits(false, true, 22f, 0f),
                   "someone else's kill is never the player's bow");
        Check.That(!StormBowCredit.Credits(true, false, 0f, 0f),
                   "a kill by a melee weapon is not the bow's, even just after a shot");
    }

    static void SelfCheckTests()
    {
        var known = new HashSet<string> { "SunkenCrypt4", "Charred_Archer", "StoneHenge3" };
        var check = new SagaSelfCheck();

        string crypt = check.Pick("The drowned one's crypt", new[] { "SunkenCrypt4" }, known.Contains, "he never appears", stalls: true);
        string body = check.Pick("The charred one's body", new[] { "Charred_Melee", "Charred_Archer" }, known.Contains, "cannot stand", stalls: true);
        string ring = check.Pick("The harvester's ring", new[] { "StoneHenge1", "StoneHenge2" }, known.Contains,
                                 "stands on plains ground near the player", stalls: false);
        string site = check.Pick("The keeper's chamber", new[] { "Crypt2" }, known.Contains, "he never appears", stalls: true);

        Check.That(crypt == "SunkenCrypt4" && body == "Charred_Archer" && ring == null && site == null,
                   "Pick returns the first name that exists, or null");
        Check.That(check.Lines[0].Verdict == SelfCheckVerdict.Ok, "the guess itself resolving is OK");
        Check.That(check.Lines[1].Verdict == SelfCheckVerdict.Fallback && check.Lines[1].Detail.Contains("Charred_Melee") &&
                   check.Lines[1].Detail.Contains("Charred_Archer"),
                   "a later candidate is a FALLBACK naming both the guess and what was used");
        Check.That(check.Lines[2].Verdict == SelfCheckVerdict.Fallback && check.Lines[2].Detail.Contains("plains ground"),
                   "no name at all, with a way around it, is a FALLBACK saying what happens instead");
        Check.That(check.Lines[3].Verdict == SelfCheckVerdict.Missing && check.Lines[3].Detail.Contains("never appears"),
                   "no name and no way around it is MISSING, with the consequence");

        check.Ok("Quest prices", "all 14 are items");
        var lines = check.Format("1.0.16-run.2026-10-05n").ToList();
        Check.That(lines.All(l => l.StartsWith(SagaSelfCheck.Tag)),
                   "every line carries the tag, so one grep finds the whole block");
        Check.That(lines[0].Contains("2 OK") && lines[0].Contains("2 FALLBACK") && lines[0].Contains("1 MISSING") &&
                   lines[0].Contains("1.0.16-run.2026-10-05n"),
                   "the header counts each verdict and names the build");
        Check.That(lines.Count == 6, "a header and one line per check");
        Check.That(lines.FindIndex(l => l.Contains("MISSING") && l.Contains("keeper")) == 1,
                   "MISSING lines come first under the header, then FALLBACK, then OK");

        var many = new SagaSelfCheck();
        many.Pick("Somewhere", Enumerable.Range(1, 9).Select(i => "Site" + i).ToArray(), n => false, "stalls", stalls: true);
        Check.That(many.Lines[0].Detail.Contains("Site1") && many.Lines[0].Detail.Contains("6 more") &&
                   !many.Lines[0].Detail.Contains("Site9"),
                   "a long candidate list is shortened rather than printed whole");
        Check.That(new SagaSelfCheck().Pick("Nothing to try", new string[0], n => true, "x", stalls: true) == null,
                   "no candidates resolves to nothing rather than throwing");

        var any = new SagaSelfCheck();
        int rings = any.AnyOf("Stone rings", new[] { "StoneHenge1", "StoneHenge2", "StoneHenge3" }, known.Contains,
                              "stands on plains ground", stalls: false);
        any.AnyOf("Dvergr sites", new[] { "Mistlands_Harbour1" }, known.Contains, "stands in the mist", stalls: false);
        Check.That(rings == 1 && any.Lines[0].Verdict == SelfCheckVerdict.Ok && any.Lines[0].Detail.Contains("1 of 3") &&
                   any.Lines[0].Detail.Contains("StoneHenge3"),
                   "AnyOf is OK when any name exists - a later ring is as good as the first - and says how many");
        Check.That(any.Lines[1].Verdict == SelfCheckVerdict.Fallback && any.Lines[1].Detail.Contains("in the mist"),
                   "AnyOf with none found and a way around it is a FALLBACK");

        var all = new SagaSelfCheck();
        all.AllOf("Quest prices", 14, new string[0], "refused");
        all.AllOf("Quest creatures", 30, new[] { "Fenrirr" }, "their kill quests never progress");
        Check.That(all.Lines[0].Verdict == SelfCheckVerdict.Ok && all.Lines[0].Detail.Contains("all 14") &&
                   all.Lines[1].Verdict == SelfCheckVerdict.Missing && all.Lines[1].Detail.Contains("Fenrirr"),
                   "AllOf is OK with the total, or MISSING naming each bad one");
    }
}
