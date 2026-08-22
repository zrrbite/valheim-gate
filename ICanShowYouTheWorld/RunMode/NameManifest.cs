using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Every game-asset name a set of challenge definitions depends on, split by which registry
    /// can answer for it.
    ///
    /// This exists because of the mode's oldest and most expensive failure: asset names are Unity
    /// data, invisible to the compiled assembly, and a wrong one does not throw. A wrong CREATURE
    /// name means a kill quest that silently never progresses; a wrong ITEM token means a collect
    /// sub that is dead for the rest of the run. Both look exactly like bad luck, and the only
    /// detector to date has been playing until something felt stuck.
    ///
    /// Collecting the names is pure list-walking, so it lives here and is unit-tested. Resolving
    /// them needs the live game (ZNetScene for creatures, ObjectDB for items) and belongs to the
    /// host — see RunService's validator, which logs what this cannot.
    /// </summary>
    public class NameManifest
    {
        /// <summary>Creature prefab names from KillPrefab challenges and subs — resolved against ZNetScene.</summary>
        public List<string> CreaturePrefabs = new List<string>();

        /// <summary>Item names from CollectItem challenges and subs — resolved against ObjectDB.</summary>
        public List<string> ItemNames = new List<string>();

        /// <summary>
        /// Build-piece categories from BuildPiece challenges and subs. Not an asset name at all —
        /// these resolve to compiled component types host-side, which is the entire reason
        /// BuildPiece was preferred over naming pieces. Collected anyway so the validator can catch
        /// the one mistake still possible here: a definition naming a category the host has no
        /// entry for, which is a typo in our own vocabulary rather than in Valheim's data.
        /// </summary>
        public List<string> PieceCategories = new List<string>();

        /// <summary>
        /// Biome names from ReachBiome challenges — resolved host-side against Heightmap.Biome.
        ///
        /// Worth checking even though it is our own vocabulary rather than Valheim's: a ReachBiome
        /// step is the FIRST step of an act, so a typo there stalls the whole act at its opening
        /// beat, with the player having demonstrably arrived and nothing happening.
        /// </summary>
        public List<string> Biomes = new List<string>();

        /// <summary>
        /// Walks every definition — its own Kind/Param and each of its Subs — and buckets the names
        /// it depends on. Null-safe throughout and de-duplicated; order is the order first seen, so
        /// the validator's output reads in pool order rather than at random.
        /// </summary>
        public static NameManifest Collect(IEnumerable<ChallengeDefinition> definitions)
        {
            var manifest = new NameManifest();
            if (definitions == null) return manifest;

            foreach (var def in definitions.Where(d => d != null))
            {
                manifest.Add(def.Kind, def.Param);

                // RequiresBuilt is a gate rather than an objective, but it is drawn from the same
                // category vocabulary and a typo there silently makes the task undrawable FOREVER —
                // strictly worse than a dead objective, because nothing ever appears to go wrong.
                // Collected BEFORE the Subs guard below: most definitions have no subs, and this
                // sitting after an early-continue is how it went uncollected for all of them.
                manifest.AddTo(manifest.PieceCategories, def.RequiresBuilt);

                if (def.Subs == null) continue;
                foreach (var sub in def.Subs.Where(s => s != null)) manifest.Add(sub.Kind, sub.Param);
            }

            return manifest;
        }

        private void Add(ChallengeKind kind, string param)
        {
            switch (kind)
            {
                case ChallengeKind.KillPrefab: AddTo(CreaturePrefabs, param); break;
                case ChallengeKind.CollectItem: AddTo(ItemNames, param); break;
                case ChallengeKind.BuildPiece: AddTo(PieceCategories, param); break;
                case ChallengeKind.ReachBiome: AddTo(Biomes, param); break;
            }
        }

        private void AddTo(List<string> target, string name)
        {
            if (!string.IsNullOrEmpty(name) && !target.Contains(name)) target.Add(name);
        }
    }
}
