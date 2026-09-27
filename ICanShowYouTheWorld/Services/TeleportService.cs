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

            TeleportTo(position, distant: true);   // GM: unchanged, always the distant wait
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
            TeleportTo(spawnPoint, distant: true); // GM: unchanged, always the distant wait
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

                    TeleportTo(destination, distant: true);   // GM: unchanged
                    _game.ShowMessage("Teleported to safe spot", MessageType.TopLeft);
                    return;
                }
            }

            _game.ShowMessage("No safe pin found", MessageType.TopLeft);
        }

        /// <summary>
        /// Beyond this many metres a teleport is DISTANT in the game's sense; within it, near.
        /// </summary>
        /// <remarks>
        /// Player.UpdateTeleport (1.0.16 IL) holds a distant teleport on the spinning screen for at
        /// least 8 s, and up to 15 s when ZoneSystem.FindFloor misses at the target, because a
        /// distant hop has zones to load first. A near hop ends as soon as ZNetScene.IsAreaReady
        /// says the target is loaded, and shows no spin at all (ShowTeleportAnimation is
        /// m_distantTeleport). The mod used to pass true for everything, so Homeward from the edge
        /// of the base spun for eight seconds with the player already home (owner, 2026-09-27).
        ///
        /// 200 m is comfortably inside the loaded area around the player, so a hop within it lands
        /// in zones that are already there; IsAreaReady still guards it either way. The price of
        /// guessing wrong is small but real: a near hop whose FindFloor misses is sent BACK with
        /// "$msg_portal_blocked", where a distant one would be put on the solid height instead.
        /// </remarks>
        public const float DistantBeyondMeters = 200f;

        public void TeleportTo(Vector3 position, Quaternion? rotation = null, bool? distant = null)
        {
            if (!_game.IsLocalPlayerValid)
            {
                return;
            }

            var player = _game.LocalPlayer;
            Quaternion targetRotation = rotation ?? player.transform.rotation;
            bool isDistant = distant ??
                (Vector3.Distance(player.transform.position, position) > DistantBeyondMeters);

            player.TeleportTo(position, targetRotation, distantTeleport: isDistant);
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
