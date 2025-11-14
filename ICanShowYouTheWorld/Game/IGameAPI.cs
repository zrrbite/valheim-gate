using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.GameAPI
{
    /// <summary>
    /// Abstraction layer for Valheim game API.
    /// This interface isolates the mod from direct Valheim API calls, enabling:
    /// - Unit testing with mock implementations
    /// - Easier adaptation to Valheim API changes
    /// - Clearer dependency boundaries
    /// </summary>
    public interface IGameAPI
    {
        // ===== Player Access =====

        /// <summary>
        /// Get the local player instance.
        /// </summary>
        Player LocalPlayer { get; }

        /// <summary>
        /// Check if the local player is valid (not null and active).
        /// </summary>
        bool IsLocalPlayerValid { get; }

        // ===== Character Queries =====

        /// <summary>
        /// Get all characters within a radius of a position.
        /// </summary>
        /// <param name="position">Center position.</param>
        /// <param name="radius">Search radius in meters.</param>
        /// <returns>List of characters in range.</returns>
        IEnumerable<Character> GetCharactersInRange(Vector3 position, float radius);

        /// <summary>
        /// Get all characters in the world.
        /// </summary>
        /// <returns>List of all characters.</returns>
        IEnumerable<Character> GetAllCharacters();

        // ===== World & Position =====

        /// <summary>
        /// Get the world position of the mouse cursor on the minimap.
        /// Returns Vector3.zero if map is not open or cursor is invalid.
        /// </summary>
        /// <returns>World position or Vector3.zero.</returns>
        Vector3 GetMapCursorWorldPosition();

        /// <summary>
        /// Check if the minimap is currently open/visible.
        /// </summary>
        bool IsMapOpen { get; }

        /// <summary>
        /// Get a safe spawn position near the player (on ground, not in water).
        /// </summary>
        /// <param name="offset">Offset from player position.</param>
        /// <returns>Safe world position.</returns>
        Vector3 GetSafeSpawnPosition(Vector3 offset);

        // ===== Teleportation =====

        /// <summary>
        /// Teleport the local player to a position.
        /// </summary>
        /// <param name="position">Target world position.</param>
        void TeleportPlayer(Vector3 position);

        /// <summary>
        /// Teleport all players within radius to a target position.
        /// </summary>
        /// <param name="fromPosition">Center position to search for players.</param>
        /// <param name="radius">Search radius.</param>
        /// <param name="toPosition">Target teleport position.</param>
        /// <returns>Number of players teleported.</returns>
        int TeleportPlayersInRange(Vector3 fromPosition, float radius, Vector3 toPosition);

        // ===== Messaging =====

        /// <summary>
        /// Show a message to the local player.
        /// </summary>
        /// <param name="text">Message text.</param>
        /// <param name="type">Message type (Center, TopLeft, etc.).</param>
        void ShowMessage(string text, MessageType type = MessageType.Center);

        /// <summary>
        /// Print a message to the console.
        /// </summary>
        /// <param name="text">Message text.</param>
        void PrintToConsole(string text);

        // ===== Prefab & Spawning =====

        /// <summary>
        /// Spawn a prefab at a position.
        /// </summary>
        /// <param name="prefabName">Name of the prefab (e.g., "Greydwarf", "Wood").</param>
        /// <param name="position">World position to spawn at.</param>
        /// <param name="rotation">Rotation (optional).</param>
        /// <returns>The spawned GameObject, or null if failed.</returns>
        GameObject SpawnPrefab(string prefabName, Vector3 position, Quaternion? rotation = null);

        /// <summary>
        /// Check if a prefab exists in the game.
        /// </summary>
        /// <param name="prefabName">Name of the prefab.</param>
        /// <returns>True if prefab exists.</returns>
        bool PrefabExists(string prefabName);

        /// <summary>
        /// Get all available prefab names (for browsing/spawning).
        /// </summary>
        /// <returns>List of prefab names.</returns>
        IEnumerable<string> GetAllPrefabNames();

        // ===== Game State =====

        /// <summary>
        /// Check if the game is currently running (not paused, loading, etc.).
        /// </summary>
        bool IsGameActive { get; }

        /// <summary>
        /// Check if ZNetScene is ready (required for spawning).
        /// </summary>
        bool IsZNetSceneReady { get; }

        /// <summary>
        /// Get the current game time (in-game day/night cycle).
        /// </summary>
        /// <returns>Time value (0-1, where 0.5 is noon).</returns>
        float GetGameTime();

        // ===== Environment & Physics =====

        /// <summary>
        /// Raycast from a position downward to find ground.
        /// </summary>
        /// <param name="position">Start position (usually above ground).</param>
        /// <param name="maxDistance">Maximum raycast distance.</param>
        /// <param name="hitPoint">Ground hit point (out parameter).</param>
        /// <returns>True if ground was found.</returns>
        bool FindGroundPosition(Vector3 position, float maxDistance, out Vector3 hitPoint);

        /// <summary>
        /// Check if a position is in water.
        /// </summary>
        /// <param name="position">World position to check.</param>
        /// <returns>True if position is underwater.</returns>
        bool IsPositionInWater(Vector3 position);

        // ===== Combat & Damage =====

        /// <summary>
        /// Apply damage to a character.
        /// </summary>
        /// <param name="character">Target character.</param>
        /// <param name="damage">Damage amount.</param>
        /// <param name="damageType">Type of damage.</param>
        void DamageCharacter(Character character, float damage, HitData.DamageTypes damageType);

        /// <summary>
        /// Heal a character.
        /// </summary>
        /// <param name="character">Target character.</param>
        /// <param name="amount">Heal amount.</param>
        void HealCharacter(Character character, float amount);

        /// <summary>
        /// Kill a character instantly.
        /// </summary>
        /// <param name="character">Target character.</param>
        void KillCharacter(Character character);

        // ===== Skills & Stats =====

        /// <summary>
        /// Modify player's run speed.
        /// </summary>
        /// <param name="speed">New run speed value.</param>
        void SetPlayerRunSpeed(float speed);

        /// <summary>
        /// Get player's current run speed.
        /// </summary>
        /// <returns>Run speed value.</returns>
        float GetPlayerRunSpeed();

        /// <summary>
        /// Reset player's forsaken power cooldown.
        /// </summary>
        void ResetForsakenPowerCooldown();
    }

    /// <summary>
    /// Message type for player notifications.
    /// Maps to MessageHud.MessageType.
    /// </summary>
    public enum MessageType
    {
        TopLeft = 1,
        Center = 2
    }
}
