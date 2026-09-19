using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// One crafting recipe the saga adds to the game for the length of a run.
    ///
    /// Every name here is Unity asset data: a prefab the compiled assembly cannot see and cannot
    /// verify. A wrong one does not throw — the recipe is simply not registered, and the log says
    /// so once. Prefab names, not "$item_" tokens: ObjectDB.GetItemPrefab resolves prefabs, and the
    /// recipe wants the prefab's own ItemDrop.
    /// </summary>
    internal sealed class SagaRecipeDefinition
    {
        /// <summary>Stable id; the registered recipe is named <see cref="SagaRecipes.NamePrefix"/> + this.</summary>
        public string Id;

        /// <summary>Prefab of the item the recipe produces, e.g. "BowFineWood".</summary>
        public string ResultPrefab;

        /// <summary>How many the craft yields.</summary>
        public int Amount = 1;

        /// <summary>
        /// Prefab of the station it is crafted at, e.g. "piece_workbench". The game matches a recipe
        /// to a station by the CraftingStation's m_name, so any prefab carrying the component with
        /// the right name will do; the placed piece's own prefab is the obvious one.
        /// </summary>
        public string StationPrefab;

        /// <summary>Station level required to craft (1 = an unimproved bench).</summary>
        public int MinStationLevel = 1;

        /// <summary>Ingredients as (prefab, amount). Upgrades ask for half of each per quality level.</summary>
        public (string prefab, int amount)[] Resources;

        /// <summary>
        /// Id of a questline step that must be DONE before this recipe exists, or null for
        /// "the whole run". The shade teaches the bow; until it has been paid, the bench does
        /// not know the shape. Judged by the host every poll (StepPredicates.StepDone), so it
        /// is derived from the tracks and survives a resume without being saved.
        /// </summary>
        public string RequiresStepDone;
    }

    /// <summary>
    /// Registers the saga's own recipes with the game's object database while a run is live, and
    /// takes them out again when it ends.
    ///
    /// Recipes are nothing more exotic than entries in <c>ObjectDB.m_recipes</c>, a plain list the
    /// game walks whenever a crafting window opens. Adding a <see cref="Recipe"/> ScriptableObject
    /// to it is the whole trick; the player's "known recipes" list picks it up on its own once
    /// they know every ingredient and the station, exactly as for a vanilla recipe.
    ///
    /// Two things make this a component with state rather than a one-off call:
    ///
    /// The database is REBUILT when a world loads (<c>ObjectDB.CopyOtherDB</c> copies the lists
    /// afresh from the prefab database), which silently discards anything added before. So the
    /// registration is re-checked on the run's 1 Hz poll and re-done whenever the instance has
    /// changed — <see cref="ReferenceEquals"/>, not <c>==</c>, because a destroyed instance compares
    /// equal to null and would otherwise look like "still the same one".
    ///
    /// And the recipes must be RUN-ONLY. Outside the saga the game is vanilla, so
    /// <see cref="Remove"/> strips every entry carrying the prefix when the run ends. The prefix
    /// is the identity: it survives a database rebuild finding an orphaned copy, which a cached
    /// reference would not.
    ///
    /// The ITEMS a recipe produces are a separate concern — see <see cref="SagaItems"/>, which is
    /// NOT run-only, because a saga item outlives the run that made it and must keep loading.
    /// </summary>
    internal sealed class SagaRecipes
    {
        public const string NamePrefix = "Saga_";

        /// <summary>
        /// The saga's recipes. Act I's is the proof: Thor's bow — the saga's own item, cut from the
        /// Finewood bow with lightning added (see SagaItems) — from what the Meadows yield (wood, the
        /// splinters' resin, the herd's hide) and three lights taken back off the forest, at the act's
        /// own top station, the workbench.
        /// Amounts are repeated in the quest step's Hint; change both or the hint lies.
        /// </summary>
        public static readonly SagaRecipeDefinition[] All =
        {
            new SagaRecipeDefinition
            {
                Id = "hunters-bow",
                ResultPrefab = SagaItems.ThorsBowPrefab,
                StationPrefab = "piece_workbench",
                MinStationLevel = 1,
                // The lights are the point of the recipe now (owner: "Thors bow is a bit simple to
                // craft"). Wood, resin and hide are what the Meadows yield to anyone; a rescued
                // light is the only ingredient that has to be WON, and it comes from the hunt track
                // while the bow sits on the craft one — the two tracks finally asking something of
                // each other. See SagaItems.ThorsBowLightCost for why this cannot lock the chain.
                Resources = new[]
                {
                    ("Wood", 10), ("Resin", 10), ("DeerHide", 6),
                    (SagaItems.RescuedLightPrefab, SagaItems.ThorsBowLightCost),
                },
                RequiresStepDone = SagaNames.ShadeBringStepId,
            },
            new SagaRecipeDefinition
            {
                // Act I's last craft. Gated on the TROLL step rather than on an item, because a
                // gate on "have you got troll hide" would simply be invisible - this way the bench
                // learns the shape the moment the Breaker is dealt with, and the hide is what the
                // recipe then asks for.
                //
                // Safe to gate on a LOSABLE step: a failed step still advances its track, so
                // StepDone answers true either way. Miss the troll and the recipe is there and the
                // hide is not, which is the cost being visible rather than the chain being stuck.
                Id = "stormward",
                ResultPrefab = SagaItems.StormwardPrefab,
                StationPrefab = "piece_workbench",
                MinStationLevel = 2,
                Resources = new[]
                {
                    ("Wood", 20), ("Resin", 20), ("TrollHide", 10), ("DeerHide", 10),
                    (SagaItems.RescuedLightPrefab, SagaItems.StormwardLightCost),
                },
                RequiresStepDone = SagaNames.BreakerStepId,
            },
        };

        /// <summary>The database the recipes were last registered on. Compared by reference only.</summary>
        private ObjectDB _registeredOn;

        private readonly HashSet<string> _reported = new HashSet<string>();

        /// <summary>
        /// Makes sure every UNLOCKED recipe is present in the live database and every locked one
        /// is absent. <paramref name="stepDone"/> answers whether a questline step has been
        /// passed; a recipe with no gate is always unlocked. Cheap when nothing changed: one
        /// reference check and a short scan of the recipe list.
        /// </summary>
        public void Ensure(Func<string, bool> stepDone)
        {
            try
            {
                var odb = ObjectDB.instance;
                if (odb == null || odb.m_recipes == null) return;

                bool Unlocked(SagaRecipeDefinition d) =>
                    string.IsNullOrEmpty(d.RequiresStepDone) || (stepDone != null && stepDone(d.RequiresStepDone));

                bool sameDb = ReferenceEquals(odb, _registeredOn);
                if (sameDb && All.All(d => Has(odb, d) == Unlocked(d))) return;

                foreach (var def in All)
                {
                    bool has = Has(odb, def);
                    bool want = Unlocked(def);
                    if (want && !has) Register(odb, def);
                    else if (!want && has) Unregister(odb, def);
                }

                _registeredOn = odb;
            }
            catch (Exception ex)
            {
                ReportOnce("ensure", "[ICanShowYouTheWorld] Saga recipes could not be registered: " + ex.Message);
            }
        }

        /// <summary>Removes every saga recipe from the live database. Safe to call with none registered.</summary>
        public void Remove()
        {
            _registeredOn = null;
            _reported.Clear();

            try
            {
                var odb = ObjectDB.instance;
                if (odb == null || odb.m_recipes == null) return;

                var ours = odb.m_recipes
                    .Where(r => r != null && r.name != null && r.name.StartsWith(NamePrefix, StringComparison.Ordinal))
                    .ToList();

                foreach (var r in ours)
                {
                    odb.m_recipes.Remove(r);
                    UnityEngine.Object.Destroy(r);
                }

                if (ours.Count > 0)
                    Debug.Log($"[ICanShowYouTheWorld] Saga recipes removed: {ours.Count}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Saga recipes could not be removed: " + ex.Message);
            }
        }

        private static bool Has(ObjectDB odb, SagaRecipeDefinition def)
        {
            string wanted = NamePrefix + def.Id;
            return odb.m_recipes.Any(r => r != null && r.name == wanted);
        }

        private static void Unregister(ObjectDB odb, SagaRecipeDefinition def)
        {
            string wanted = NamePrefix + def.Id;
            foreach (var r in odb.m_recipes.Where(r => r != null && r.name == wanted).ToList())
            {
                odb.m_recipes.Remove(r);
                UnityEngine.Object.Destroy(r);
            }
        }

        private void Register(ObjectDB odb, SagaRecipeDefinition def)
        {
            var result = ItemOf(odb, def.ResultPrefab);
            if (result == null)
            {
                ReportOnce(def.Id + "-result",
                    $"[ICanShowYouTheWorld] Saga recipe '{def.Id}': result prefab '{def.ResultPrefab}' is not an item — recipe NOT registered.");
                return;
            }

            CraftingStation station = null;
            if (!string.IsNullOrEmpty(def.StationPrefab))
            {
                var scene = ZNetScene.instance;
                var stationPrefab = scene != null ? scene.GetPrefab(def.StationPrefab) : null;
                station = stationPrefab != null ? stationPrefab.GetComponent<CraftingStation>() : null;
                if (station == null)
                {
                    ReportOnce(def.Id + "-station",
                        $"[ICanShowYouTheWorld] Saga recipe '{def.Id}': station prefab '{def.StationPrefab}' has no CraftingStation — recipe NOT registered.");
                    return;
                }
            }

            var resources = new List<Piece.Requirement>();
            foreach (var (prefab, amount) in def.Resources ?? Array.Empty<(string, int)>())
            {
                var drop = ItemOf(odb, prefab);
                if (drop == null)
                {
                    ReportOnce(def.Id + "-" + prefab,
                        $"[ICanShowYouTheWorld] Saga recipe '{def.Id}': ingredient prefab '{prefab}' is not an item — recipe NOT registered.");
                    return;
                }

                resources.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,
                    // Quality upgrades at the bench ask for half again per level, like most of the
                    // game's own gear; never zero, which the game reads as "free upgrade".
                    m_amountPerLevel = Math.Max(1, amount / 2),
                    m_recover = true,
                });
            }

            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = NamePrefix + def.Id;
            recipe.m_item = result;
            recipe.m_amount = Math.Max(1, def.Amount);
            recipe.m_enabled = true;
            recipe.m_qualityResultAmountMultiplier = 1f;
            recipe.m_craftingStation = station;
            recipe.m_repairStation = station;
            recipe.m_minStationLevel = Math.Max(1, def.MinStationLevel);
            recipe.m_requireOnlyOneIngredient = false;
            recipe.m_resources = resources.ToArray();

            odb.m_recipes.Add(recipe);

            Debug.Log($"[ICanShowYouTheWorld] Saga recipe registered: {recipe.name} -> {def.ResultPrefab} x{recipe.m_amount} " +
                      $"at {def.StationPrefab} lv{recipe.m_minStationLevel} " +
                      $"({string.Join(", ", (def.Resources ?? Array.Empty<(string, int)>()).Select(r => $"{r.prefab} {r.amount}").ToArray())})");
        }

        /// <summary>The prefab's ItemDrop, or null when the name is not an item the database knows.</summary>
        private static ItemDrop ItemOf(ObjectDB odb, string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            var go = odb.GetItemPrefab(prefabName);
            return go != null ? go.GetComponent<ItemDrop>() : null;
        }

        private void ReportOnce(string key, string message)
        {
            if (!_reported.Add(key)) return;
            Debug.LogError(message);
        }
    }
}
