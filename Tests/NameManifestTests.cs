using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.RunMode;

static class NameManifestTests
{
    public static void Run()
    {
        var empty = NameManifest.Collect(null);
        Check.That(empty.CreaturePrefabs.Count == 0 && empty.ItemNames.Count == 0 &&
                   empty.PieceCategories.Count == 0, "a null definition list yields an empty manifest");

        var defs = new List<ChallengeDefinition>
        {
            new ChallengeDefinition { Id = "k-boar", Kind = ChallengeKind.KillPrefab, Param = "Boar", Target = 3 },
            new ChallengeDefinition { Id = "c-wood", Kind = ChallengeKind.CollectItem, Param = "$item_wood", Target = 25 },
            new ChallengeDefinition { Id = "b-fire", Kind = ChallengeKind.BuildPiece, Param = "Fire", Target = 1 },
            new ChallengeDefinition { Id = "r-swamp", Kind = ChallengeKind.ReachBiome, Param = "Swamp", Target = 1 },

            // Bucketed by kind, not by shape: altitude and stat-delta params are not asset names
            // and must not end up anywhere in the manifest.
            new ChallengeDefinition { Id = "s-alt",  Kind = ChallengeKind.ReachAltitude, Param = "", Target = 150 },
            new ChallengeDefinition { Id = "s-jump", Kind = ChallengeKind.StatDelta, Param = "Jumps", Target = 15 },

            // Subs contribute on the same terms as a top-level Kind/Param.
            new ChallengeDefinition
            {
                Id = "cq-camp", Target = 1,
                Subs = new List<SubObjective>
                {
                    new SubObjective { Kind = ChallengeKind.KillPrefab,  Param = "Greyling",   Target = 2 },
                    new SubObjective { Kind = ChallengeKind.CollectItem, Param = "$item_stone", Target = 10 },
                    new SubObjective { Kind = ChallengeKind.BuildPiece,  Param = "Cooking",     Target = 1 },
                    new SubObjective { Kind = ChallengeKind.CollectFood, Param = "",            Target = 5 },
                }
            },

            // A gate, not an objective — collected because a typo here makes the task undrawable
            // forever, which is quieter than a dead objective.
            new ChallengeDefinition { Id = "s-doors", Kind = ChallengeKind.StatDelta, Param = "DoorsOpened", Target = 8, RequiresBuilt = "Door" },

            // Duplicates collapse; nulls and blanks are ignored rather than thrown on.
            new ChallengeDefinition { Id = "k-boar2", Kind = ChallengeKind.KillPrefab, Param = "Boar", Target = 5 },
            new ChallengeDefinition { Id = "k-blank", Kind = ChallengeKind.KillPrefab, Param = "", Target = 1 },
            new ChallengeDefinition { Id = "k-null",  Kind = ChallengeKind.KillPrefab, Param = null, Target = 1 },
            null,
        };

        var m = NameManifest.Collect(defs);

        Check.That(m.CreaturePrefabs.SequenceEqual(new[] { "Boar", "Greyling" }),
            "creature names come from KillPrefab challenges and subs, deduped, in pool order");
        Check.That(m.ItemNames.SequenceEqual(new[] { "$item_wood", "$item_stone" }),
            "item names come from CollectItem challenges and subs");
        Check.That(m.PieceCategories.SequenceEqual(new[] { "Fire", "Cooking", "Door" }),
            "piece categories come from BuildPiece challenges, subs, and RequiresBuilt gates");

        Check.That(!m.CreaturePrefabs.Contains("Jumps") && !m.ItemNames.Contains("Jumps"),
            "a StatDelta param is not an asset name and is never collected");
        Check.That(!m.ItemNames.Contains(""), "CollectFood carries no param and contributes nothing");
        Check.That(m.Biomes.SequenceEqual(new[] { "Swamp" }), "biome names come from ReachBiome challenges");
        Check.That(!m.PieceCategories.Contains("Swamp"), "a biome is not a build category");
    }
}
