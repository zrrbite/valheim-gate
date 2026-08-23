using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The act table itself lives in RunService, which is game-coupled and outside this harness. What
/// IS testable here is the shape an act must have and the invariants its tracks must satisfy — so
/// the tests below define those against a stand-in table with the same structure as the real one,
/// and the real table is checked against the same rules by ValidateActs at run start.
/// </summary>
static class ActDefinitionTests
{
    /// <summary>Mirrors the real table's shape: five acts, each with a hunt track ending on its boss.</summary>
    static List<ActDefinition> Sample() => new List<ActDefinition>
    {
        Act("act1", "I",   "The Meadows",      "defeated_eikthyr",   "mq-eikthyr",  "Eikthyr"),
        Act("act2", "II",  "The Black Forest", "defeated_gdking",    "bf-elder",    "gd_king"),
        Act("act3", "III", "The Swamp",        "defeated_bonemass",  "sw-bonemass", "Bonemass"),
        Act("act4", "IV",  "The Mountains",    "defeated_dragon",    "mt-moder",    "Dragon"),
        Act("act5", "V",   "The Plains",       "defeated_goblinking","pl-yagluth",  "GoblinKing"),
    };

    static ActDefinition Act(string id, string numeral, string title, string key, string bossStepId, string bossPrefab) =>
        new ActDefinition
        {
            Id = id, Numeral = numeral, Title = title, BossDefeatKey = key,
            Tracks = new List<QuestTrack>
            {
                new QuestTrack
                {
                    Id = "hunt", Label = "HUNT",
                    Chain = new List<ChallengeDefinition>
                    {
                        new ChallengeDefinition { Id = id + "-kill", MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = "Boar", Target = 3, Display = "a hunt" },
                        new ChallengeDefinition { Id = bossStepId, MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = bossPrefab, Target = 1, Display = "Defeat the boss" },
                    },
                },
                new QuestTrack
                {
                    Id = "craft", Label = "CRAFT",
                    Chain = new List<ChallengeDefinition>
                    {
                        new ChallengeDefinition { Id = id + "-build", MainQuest = true, Kind = ChallengeKind.BuildPiece, Param = id + "Thing", Target = 1, Display = "a build" },
                    },
                },
            },
        };

    public static void Run()
    {
        var acts = Sample();

        Check.That(acts.Count == 5, "the saga has five acts, one per boss");

        // The banner is what the HUD heads the quest section with and what a transition announces.
        Check.That(acts[1].Banner == "ACT II — THE BLACK FOREST", "the banner reads ACT <numeral> — <TITLE>");
        Check.That(acts[1].Label == "Act II — The Black Forest", "the label is the prose form");

        // Every act's HUNT track must END on its boss. That is what makes "the hunt track ran out"
        // and "the act is over" the same event. A CRAFT track carries no such obligation — finishing
        // it early is allowed, and so is never finishing it before the boss falls.
        foreach (var act in acts)
        {
            var hunt = act.Tracks.FirstOrDefault(t => t.Id == "hunt");
            Check.That(hunt != null && hunt.Chain.Count > 0, $"{act.Id} has a hunt track");

            var last = hunt.Chain.LastOrDefault();
            Check.That(last != null && last.Kind == ChallengeKind.KillPrefab, $"{act.Id}'s hunt track ends on a kill");
            Check.That(last.Target == 1, $"{act.Id} ends on a single kill");
        }

        // Step ids must be unique across every TRACK of every act, not merely within one. RestoreTrack
        // resolves a saved position by id against one track's chain, so an id appearing twice anywhere
        // lets a resume seat the wrong step — and since a step completes the moment Progress >= Target,
        // that can fire an unearned completion on the first tick.
        var allIds = acts.SelectMany(a => a.AllSteps).Select(c => c.Id).ToList();
        Check.That(allIds.Distinct().Count() == allIds.Count, "step ids are unique across every act and track");

        // Act ids and boss keys likewise: the act index is derived from how many of these keys the
        // world has set, so a duplicate key would make two acts indistinguishable.
        Check.That(acts.Select(a => a.Id).Distinct().Count() == acts.Count, "act ids are unique");
        Check.That(acts.Select(a => a.BossDefeatKey).Distinct().Count() == acts.Count, "boss keys are unique");

        // A build category may appear in ONE act only: the built-piece latch runs for the whole run,
        // so a category an earlier act satisfied auto-completes a later act's step for free.
        var categories = acts.SelectMany(a => a.AllSteps)
            .Where(c => c.Kind == ChallengeKind.BuildPiece)
            .Select(c => c.Param)
            .ToList();
        Check.That(categories.Distinct().Count() == categories.Count, "no build category is used by two acts");

        // Every step is flagged MainQuest. The engine ignores the flag — a track is identified by the
        // list it was handed — but the host reads it to tell "a questline advanced, grant its item"
        // from "a random task finished, offer a boon".
        Check.That(acts.SelectMany(a => a.AllSteps).All(c => c.MainQuest), "every track step is flagged MainQuest");

        // AllSteps must reach every track, or validation would silently skip one.
        Check.That(acts[0].AllSteps.Count() == 3, "AllSteps spans every track of an act");

        // A fresh ActDefinition must be safe to construct and inspect without tracks being set.
        var bare = new ActDefinition();
        Check.That(bare.Tracks != null && bare.Tracks.Count == 0, "an act's track list defaults to empty, not null");
        Check.That(!bare.AllSteps.Any(), "an act with no tracks has no steps");
    }

    /// <summary>
    /// SeatingFor: unfinished work from an earlier act follows the run forward.
    ///
    /// An act ends when its boss dies, and a player can summon a boss whenever they like — so no
    /// questline gate can stop an act from ending early. Carrying the work forward can.
    /// </summary>
    public static void SeatingTests()
    {
        var acts = Sample();

        // Act I gets a hearth; no other act has one. That is the whole reason it can carry —
        // "hunt" exists in every act, so a leftover would collide with the new act's own.
        acts[0].Tracks.Add(new QuestTrack
        {
            Id = "hearth", Label = "HEARTH",
            Chain = new List<ChallengeDefinition>
            {
                new ChallengeDefinition { Id = "cozy1", MainQuest = true, Kind = ChallengeKind.PlayerState, Param = "Comfort", Target = 5, Display = "Comfort" },
                new ChallengeDefinition { Id = "cozy2", MainQuest = true, Kind = ChallengeKind.PlayerState, Param = "FishHeld", Target = 3, Display = "Fish" },
            },
        });

        // Run start: nothing is live, so everything the acts offer is seated and the save decides.
        var atStart = ActDefinition.SeatingFor(acts, 0, new List<QuestTrack>());
        Check.That(atStart.Any(t => t.Id == "hearth"), "Act I seats its own hearth");

        var laterAtStart = ActDefinition.SeatingFor(acts, 1, new List<QuestTrack>());
        Check.That(laterAtStart.Any(t => t.Id == "hearth"),
                   "a resume into Act II seats the hearth so the save can restore it");

        // Looked up by id rather than by position: how many tracks Sample() gives an act is not
        // this test's business.
        var hearthChain = acts[0].Tracks.First(t => t.Id == "hearth").Chain;
        var huntChain = acts[0].Tracks.First(t => t.Id == "hunt").Chain;

        // Act change with the hearth still unfinished: it carries.
        var live = new List<QuestTrack>
        {
            new QuestTrack { Id = "hunt", Label = "HUNT", Chain = huntChain },
            new QuestTrack { Id = "hearth", Label = "HEARTH", Chain = hearthChain,
                             Current = new ActiveChallenge { Def = hearthChain[1] } },
        };
        var carried = ActDefinition.SeatingFor(acts, 1, live);
        Check.That(carried.Any(t => t.Id == "hearth"), "an unfinished hearth carries into Act II");
        Check.That(carried.Count(t => t.Id == "hunt") == 1, "and Act II's own hunt track is not duplicated");
        Check.That(carried[0].Id == "hunt", "the act's own tracks still come first");

        // Finished before the boss fell: it does not follow.
        var done = new List<QuestTrack>
        {
            new QuestTrack { Id = "hunt", Label = "HUNT", Chain = huntChain },
            new QuestTrack { Id = "hearth", Label = "HEARTH", Chain = hearthChain, Current = null },
        };
        Check.That(!ActDefinition.SeatingFor(acts, 1, done).Any(t => t.Id == "hearth"),
                   "a finished hearth does not follow the run around");

        // Already dropped stays dropped, rather than reappearing at the next act change.
        var dropped = new List<QuestTrack>
        {
            new QuestTrack { Id = "hunt", Label = "HUNT", Chain = acts[1].Tracks.First(t => t.Id == "hunt").Chain },
        };
        Check.That(!ActDefinition.SeatingFor(acts, 2, dropped).Any(t => t.Id == "hearth"),
                   "a hearth already dropped does not come back");
    }
}
