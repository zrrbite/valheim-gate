using UnityEngine;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Service for teleportation functionality.
    /// Provides methods to teleport the player to various locations.
    /// </summary>
    public interface ITeleportService
    {
        /// <summary>
        /// Teleport to the map cursor position (where mouse is on the minimap).
        /// </summary>
        void TeleportToMapCursor();

        /// <summary>
        /// Teleport to the player's spawn point (bind location).
        /// Uses either custom spawn point or home point.
        /// </summary>
        void TeleportToSpawn();

        /// <summary>
        /// Teleport all nearby players to the map cursor position.
        /// </summary>
        /// <param name="radius">Search radius for nearby players.</param>
        void TeleportNearbyPlayersToMapCursor(float radius = 50f);

        /// <summary>
        /// Teleport to a map pin marked as "safe".
        /// </summary>
        void TeleportToSafePin();

        /// <summary>
        /// Teleport the local player to a specific position.
        /// </summary>
        /// <param name="position">Target world position.</param>
        /// <param name="rotation">Target rotation (optional).</param>
        void TeleportTo(Vector3 position, Quaternion? rotation = null);
    }
}
