namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Service for spawning game objects and prefabs.
    /// Handles prefab selection, spawning at player or cursor location.
    /// </summary>
    public interface ISpawnService
    {
        /// <summary>
        /// Currently selected prefab name from the prefab list.
        /// </summary>
        string CurrentPrefab { get; }

        /// <summary>
        /// Previous prefab in the list.
        /// </summary>
        string PrevPrefab { get; }

        /// <summary>
        /// Next prefab in the list.
        /// </summary>
        string NextPrefab { get; }

        /// <summary>
        /// Cycle to the next prefab in the available prefab list.
        /// </summary>
        void CyclePrefab();

        /// <summary>
        /// Spawn the currently selected prefab at the mouse cursor world position.
        /// Requires god mode.
        /// </summary>
        void SpawnSelectedPrefab();

        /// <summary>
        /// Spawn a specific prefab by name at the mouse cursor world position.
        /// Uses raycast to determine spawn position.
        /// Requires god mode.
        /// </summary>
        /// <param name="prefabName">Name of the prefab to spawn.</param>
        void SpawnPrefabAtCursor(string prefabName);

        /// <summary>
        /// Spawn a specific prefab by name in front of the player.
        /// Spawns 5m in front of player, with ground adjustment via raycast.
        /// Requires god mode.
        /// </summary>
        /// <param name="prefabName">Name of the prefab to spawn.</param>
        void SpawnPrefabInFrontOfPlayer(string prefabName);

        /// <summary>
        /// Repair all structures within radius.
        /// </summary>
        /// <param name="radius">Radius to search for structures (default: 20m).</param>
        void RepairStructuresAoE(float radius = 20f);

        /// <summary>
        /// Stagger all non-tamed enemies within radius.
        /// </summary>
        /// <param name="radius">Radius to search for enemies (default: 10m).</param>
        void StaggerAoE(float radius = 10f);
    }
}
