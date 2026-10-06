using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Everything the game itself provides, as prefab names: what creatures drop, what recipes craft, what
    /// rocks, trees, pickables, chests and loot spawners yield, what smelters, fermenters, cooking stations
    /// and the Obliterator turn things into, what hives and sap collectors make, what traders sell, and every
    /// fish. Read once at run start for <see cref="ItemSources"/>. Every field here was checked in 1.0.17's
    /// IL (2026-10-06); all are public.
    /// </summary>
    internal static class ItemSourceScan
    {
        public static HashSet<string> Produced(ZNetScene scene, ObjectDB odb)
        {
            var produced = new HashSet<string>();

            if (odb != null)
            {
                if (odb.m_recipes != null)
                    foreach (var recipe in odb.m_recipes)
                        if (recipe != null && recipe.m_enabled && recipe.m_item != null) produced.Add(recipe.m_item.gameObject.name);

                if (odb.m_items != null)
                    foreach (var item in odb.m_items)
                        if (item != null && item.GetComponent<Fish>() != null) produced.Add(item.name);
            }

            if (scene?.m_prefabs == null) return produced;

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                foreach (var c in prefab.GetComponentsInChildren<CharacterDrop>(true))
                    if (c.m_drops != null)
                        foreach (var d in c.m_drops) Add(produced, d?.m_prefab);

                foreach (var c in prefab.GetComponentsInChildren<DropOnDestroyed>(true)) AddTable(produced, c.m_dropWhenDestroyed);
                foreach (var c in prefab.GetComponentsInChildren<MineRock>(true)) AddTable(produced, c.m_dropItems);
                foreach (var c in prefab.GetComponentsInChildren<MineRock5>(true)) AddTable(produced, c.m_dropItems);
                foreach (var c in prefab.GetComponentsInChildren<TreeBase>(true)) AddTable(produced, c.m_dropWhenDestroyed);
                foreach (var c in prefab.GetComponentsInChildren<TreeLog>(true)) AddTable(produced, c.m_dropWhenDestroyed);
                foreach (var c in prefab.GetComponentsInChildren<Container>(true)) AddTable(produced, c.m_defaultItems);
                foreach (var c in prefab.GetComponentsInChildren<LootSpawner>(true)) AddTable(produced, c.m_items);

                foreach (var c in prefab.GetComponentsInChildren<Pickable>(true))
                {
                    Add(produced, c.m_itemPrefab);
                    AddTable(produced, c.m_extraDrops);
                }

                foreach (var c in prefab.GetComponentsInChildren<PickableItem>(true))
                    if (c.m_itemPrefab != null) produced.Add(c.m_itemPrefab.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<Smelter>(true))
                    if (c.m_conversion != null)
                        foreach (var x in c.m_conversion) if (x?.m_to != null) produced.Add(x.m_to.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<Fermenter>(true))
                    if (c.m_conversion != null)
                        foreach (var x in c.m_conversion) if (x?.m_to != null) produced.Add(x.m_to.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<CookingStation>(true))
                    if (c.m_conversion != null)
                        foreach (var x in c.m_conversion) if (x?.m_to != null) produced.Add(x.m_to.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<Incinerator>(true))
                    if (c.m_conversions != null)
                        foreach (var x in c.m_conversions) if (x?.m_result != null) produced.Add(x.m_result.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<Beehive>(true))
                    if (c.m_honeyItem != null) produced.Add(c.m_honeyItem.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<SapCollector>(true))
                    if (c.m_spawnItem != null) produced.Add(c.m_spawnItem.gameObject.name);

                foreach (var c in prefab.GetComponentsInChildren<Trader>(true))
                    if (c.m_items != null)
                        foreach (var t in c.m_items) if (t?.m_prefab != null) produced.Add(t.m_prefab.gameObject.name);
            }

            return produced;
        }

        /// <summary>A name to the prefabs it can mean: a "$item_" token names every item sharing it, anything else names itself.</summary>
        public static Dictionary<string, List<string>> TokenTable(ObjectDB odb)
        {
            var table = new Dictionary<string, List<string>>();
            if (odb?.m_items == null) return table;

            foreach (var item in odb.m_items)
            {
                var token = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name : null;
                if (string.IsNullOrEmpty(token)) continue;
                if (!table.TryGetValue(token, out var names)) table[token] = names = new List<string>();
                names.Add(item.name);
            }

            return table;
        }

        private static void AddTable(HashSet<string> produced, DropTable table)
        {
            if (table?.m_drops == null) return;
            foreach (var d in table.m_drops) Add(produced, d.m_item);
        }

        private static void Add(HashSet<string> produced, GameObject item)
        {
            if (item != null) produced.Add(item.name);
        }
    }
}
