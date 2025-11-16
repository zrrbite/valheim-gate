using System;
using System.Linq;
using UnityEngine;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Implementation of teleportation service.
    /// Handles all player teleportation functionality.
    /// </summary>
    public class TeleportService : ITeleportService
    {
        private readonly IGameAPI _game;
        private readonly IConfiguration _config;

        public TeleportService(IGameAPI game, IConfiguration config)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void TeleportToMapCursor()
        {
            if (!_game.IsLocalPlayerValid)
            {
                _game.ShowMessage("Player not valid", MessageType.TopLeft);
                return;
            }

            if (!_game.IsMapOpen)
            {
                _game.ShowMessage("Map must be open", MessageType.TopLeft);
                return;
            }

            Vector3 position = _game.GetMapCursorWorldPosition();
            if (position == Vector3.zero)
            {
                _game.ShowMessage("Invalid map position", MessageType.TopLeft);
                return;
            }

            TeleportTo(position);
            _game.ShowMessage($"Teleported to {position}", MessageType.TopLeft);
        }

        public void TeleportToSpawn()
        {
            if (!_game.IsLocalPlayerValid)
            {
                _game.ShowMessage("Player not valid", MessageType.TopLeft);
                return;
            }

            Vector3 spawnPoint = GetSpawnPoint();
            TeleportTo(spawnPoint);
            _game.ShowMessage("Teleported home", MessageType.TopLeft);
        }

        public void TeleportNearbyPlayersToMapCursor(float radius = 50f)
        {
            if (!_game.IsLocalPlayerValid)
            {
                _game.ShowMessage("Player not valid", MessageType.TopLeft);
                return;
            }

            if (!_game.IsMapOpen)
            {
                _game.ShowMessage("Map must be open", MessageType.TopLeft);
                return;
            }

            Vector3 targetPosition = _game.GetMapCursorWorldPosition();
            if (targetPosition == Vector3.zero)
            {
                _game.ShowMessage("Invalid map position", MessageType.TopLeft);
                return;
            }

            int count = _game.TeleportPlayersInRange(
                _game.LocalPlayer.transform.position,
                radius,
                targetPosition
            );

            _game.ShowMessage($"Mass teleport: {count} players", MessageType.TopLeft);
        }

        public void TeleportToSafePin()
        {
            if (!_game.IsLocalPlayerValid)
            {
                _game.ShowMessage("Player not valid", MessageType.TopLeft);
                return;
            }

            // Access minimap pins (m_pins is public static after patcher modification)
            var pins = Minimap.m_pins;
            if (pins == null)
            {
                _game.ShowMessage("No pins available", MessageType.TopLeft);
                return;
            }

            // Find pin marked as "safe"
            foreach (var pin in pins)
            {
                if (pin.m_name.Equals("safe", StringComparison.OrdinalIgnoreCase))
                {
                    // Add some randomness to avoid spawning inside objects
                    Vector3 offset = UnityEngine.Random.insideUnitSphere * 5f;
                    offset.y = 0; // Keep on same vertical level
                    Vector3 destination = pin.m_pos + offset;

                    TeleportTo(destination);
                    _game.ShowMessage("Teleported to safe spot", MessageType.TopLeft);
                    return;
                }
            }

            _game.ShowMessage("No safe pin found", MessageType.TopLeft);
        }

        public void TeleportTo(Vector3 position, Quaternion? rotation = null)
        {
            if (!_game.IsLocalPlayerValid)
            {
                return;
            }

            Quaternion targetRotation = rotation ?? _game.LocalPlayer.transform.rotation;

            // Use Valheim's built-in TeleportTo method
            _game.LocalPlayer.TeleportTo(position, targetRotation, distantTeleport: true);
        }

        /// <summary>
        /// Get the player's spawn point (custom spawn or home point).
        /// Based on TeleportUtils.GetSpawnPoint() from the original code.
        /// </summary>
        private Vector3 GetSpawnPoint()
        {
            var profile = Game.instance.GetPlayerProfile();

            if (profile.HaveCustomSpawnPoint())
            {
                return profile.GetCustomSpawnPoint();
            }

            return profile.GetHomePoint();
        }
    }
}
