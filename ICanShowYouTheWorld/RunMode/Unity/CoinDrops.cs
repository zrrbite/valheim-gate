using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>Drops coins in the world: one stack of the game's own Coins item (2026-10-08, the fittings' money).</summary>
    internal static class CoinDrops
    {
        private const string CoinsPrefab = "Coins";

        public static void Drop(Vector3 at, int count)
        {
            if (count <= 0 || ObjectDB.instance == null) return;
            var prefab = ObjectDB.instance.GetItemPrefab(CoinsPrefab);
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, at + Vector3.up, Quaternion.identity);
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null) return;
            drop.SetStack(count);
            // The world level, as RunService.DropAtPlayerFeet does: a fitting's price is counted with
            // CountItems, which skips coins below the world's level (final review, 2026-10-08).
            ItemDrop.OnCreateNew(drop);
        }
    }
}
