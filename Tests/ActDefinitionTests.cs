using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// The act table itself lives in RunService, which is game-coupled and outside this harness. What
/// IS testable here is the shape an act must have and the invariants a chain must satisfy — so the
/// tests below define those against a stand-in table with the same structure as the real one, and
/// the real table is checked against the same rules by the name validator at run start.
/// </summary>
static class ActDefinitionTests
{
    /// <summary>Mirrors the real table's shape: five acts, each ending in its own boss.</summary>
    static List<ActDefinition> Sample() => new List<ActDefinition>
    {
        Act("act1", "I",   "The Meadows",      "defeated_eikthyr",   "mq-eikthyr",  "Eikthyr"),
        Act("act2", "II",  "The Black Forest", "defeated_gdking",    "bf-elder",    "gd_king"),
        Act("act3", "III", "The Swamp",        "defeated_bonemass",  "sw-bonemass", "Bonemass"),
        Act("act4", "IV",  "The Mountains",    "defeated_dragon",    "mt-moder",    "Dragon"),
        Act("act5", "V",   "The Plains",       "defeated_goblinking","pl-yagluth",  "GoblinKing"),
    };

    static ActDefinition Act(string id, string numeral, string title, string key, string stepId, string bossPrefab) =>
        new ActDefinition
        {
            Id = id, Numeral = numeral, Title = title, BossDefeatKey = key,
            Chain = new List<ChallengeDefinition>
            {
                new ChallengeDefinition { Id = id + "-step", MainQuest = true, Kind = ChallengeKind.StatDelta, Param = "Builds", Target = 1, Display = "a step" },
                new ChallengeDefinition { Id = stepId, MainQuest = true, Kind = ChallengeKind.KillPrefab, Param = bossPrefab, Target = 1, Display = "Defeat the boss" },
            },
        };

    public static void Run()
    {
        var acts = Sample();

        Check.That(acts.Count == 5, "the saga has five acts, one per boss");

        // The banner is what the HUD heads the quest section with and what a transition announces.
        Check.That(acts[1].Banner == "ACT II — THE BLACK FOREST", "the banner reads ACT <numeral> — <TITLE>");
        Check.That(acts[1].Label == "Act II — The Black Forest", "the label is the prose form");

        // Every act must END on its boss. This is what makes "the chain ran out" and "the act is
        // over" the same event — an act whose last step is anything else would leave the questline
        // exhausted while the act it belongs to is still running, which is the dead end acts exist
        // to remove.
        foreach (var act in acts)
        {
            var last = act.Chain.LastOrDefault();
            Check.That(last != null, $"{act.Id} has a chain");
            Check.That(last.Kind == ChallengeKind.KillPrefab, $"{act.Id} ends on a kill");
            Check.That(last.Target == 1, $"{act.Id} ends on a single kill");
        }

        // Step ids must be unique across the WHOLE saga, not merely within an act. RestoreMainQuest
        // resolves a saved position by id against the current act's chain, so an id appearing in two
        // acts would let a resume seat the wrong act's step — and since a step completes the moment
        // Progress >= Target, that can fire an unearned completion on the first tick.
        var allIds = acts.SelectMany(a => a.Chain).Select(c => c.Id).ToList();
        Check.That(allIds.Distinct().Count() == allIds.Count, "step ids are unique across every act");

        // Act ids and boss keys likewise: the act index is derived from how many of these keys the
        // world has set, so a duplicate key would make two acts indistinguishable.
        Check.That(acts.Select(a => a.Id).Distinct().Count() == acts.Count, "act ids are unique");
        Check.That(acts.Select(a => a.BossDefeatKey).Distinct().Count() == acts.Count, "boss keys are unique");

        // Every chain step is flagged MainQuest. The engine ignores the flag — the chain is
        // identified by the list it was handed — but the host reads it to tell "the questline
        // advanced, grant its item" from "a random task finished, offer a boon".
        Check.That(acts.SelectMany(a => a.Chain).All(c => c.MainQuest), "every chain step is flagged MainQuest");

        // A fresh ActDefinition must be safe to construct and inspect without a chain being set.
        Check.That(new ActDefinition().Chain != null, "an act's chain defaults to empty, not null");
    }
}
