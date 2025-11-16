using System;
using System.Collections.Generic;
using UnityEngine;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Implementation of spawn service.
    /// Handles prefab spawning at player or cursor location, structure repair, and AoE stagger.
    /// </summary>
    public class SpawnService : ISpawnService
    {
        private readonly IGameAPI _game;
        private readonly IConfiguration _config;
        private readonly ICombatService _combat;

        // Prefab list
        private readonly string[] _spawnPrefabs = {
            "skip",
            "skip",
            // --- Pets ------------
            "Hen",
            "Asksvin",
            "Boar",
            "Boar_piggy",
            "Wolf",
            "Wolf_cub",
            // --- Spawners -----------
            "Spawner_Charred",
            "Spawner_Charred_Archer",
            "Spawner_Twitcher",
            "Spawner_Volture",
            // --- AOEs ------
            "Fader_MeteorSmash_AOE",
        };

        private int _prefabIndex = 0;

        public SpawnService(IGameAPI game, IConfiguration config, ICombatService combat)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public string CurrentPrefab => _spawnPrefabs[_prefabIndex];

        public string PrevPrefab =>
            (_spawnPrefabs != null && _spawnPrefabs.Length > 0)
                ? _spawnPrefabs[(_prefabIndex - 1 + _spawnPrefabs.Length) % _spawnPrefabs.Length]
                : "";

        public string NextPrefab =>
            (_spawnPrefabs != null && _spawnPrefabs.Length > 0)
                ? _spawnPrefabs[(_prefabIndex + 1 + _spawnPrefabs.Length) % _spawnPrefabs.Length]
                : "";

        public void CyclePrefab()
        {
            _prefabIndex = (_prefabIndex + 1) % _spawnPrefabs.Length;
            _game.ShowMessage($"Selected prefab: {CurrentPrefab}", MessageType.TopLeft);
        }

        public void SpawnSelectedPrefab()
        {
            SpawnPrefabAtCursor(CurrentPrefab);
        }

        public void SpawnPrefabAtCursor(string prefabName)
        {
            if (!_combat.RequireGodMode("Spawn Prefab")) return;
            if (!_game.IsLocalPlayerValid) return;

            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                _game.ShowMessage($"Missing prefab: {prefabName}", MessageType.TopLeft);
                return;
            }

            Vector3 spawnPos;
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Cast a ray from the cursor into the world
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    // Spawn right at the hit point
                    spawnPos = hit.point;
                }
                else
                {
                    // Fallback: 5m in front
                    var player = _game.LocalPlayer;
                    Vector3 fwd = player.transform.forward;
                    spawnPos = player.transform.position + fwd * 5f + Vector3.up;
                }
            }
            else
            {
                // No camera? Fallback to front
                var player = _game.LocalPlayer;
                Vector3 fwd = player.transform.forward;
                spawnPos = player.transform.position + fwd * 5f + Vector3.up;
            }

            // Nudge up so it isn't clipping the ground
            spawnPos.y += 0.5f;

            UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            _game.ShowMessage($"Spawned {prefabName} at {spawnPos:0.0}", MessageType.TopLeft);
        }

        public void SpawnPrefabInFrontOfPlayer(string prefabName)
        {
            if (!_combat.RequireGodMode("Spawn Prefab")) return;
            if (!_game.IsLocalPlayerValid) return;

            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                _game.ShowMessage($"Missing prefab: {prefabName}", MessageType.TopLeft);
                return;
            }

            var player = _game.LocalPlayer;
            Vector3 forward = player.transform.forward;
            Vector3 spawnPos = player.transform.position + forward * 5f + Vector3.up;

            // Adjust to ground level
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 10f))
                spawnPos.y = hit.point.y + 0.5f;

            UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            _game.ShowMessage($"Spawned {prefabName} at {spawnPos:0.0}", MessageType.TopLeft);
        }

        public void RepairStructuresAoE(float radius = 20f)
        {
            if (!_game.IsLocalPlayerValid) return;

            var center = _game.LocalPlayer.transform.position;
            int repaired = 0;

            foreach (var piece in UnityEngine.Object.FindObjectsOfType<Piece>())
            {
                if (Vector3.Distance(piece.transform.position, center) <= radius)
                {
                    var znv = piece.GetComponent<ZNetView>();
                    znv.InvokeRPC("RPC_Repair");
                    repaired++;
                }
            }

            _game.ShowMessage($"Repaired {repaired} structures within {radius:0}m", MessageType.TopLeft);
        }

        public void StaggerAoE(float radius = 10f)
        {
            if (!_game.IsLocalPlayerValid) return;

            var center = _game.LocalPlayer.transform.position;
            var list = new List<Character>();
            Character.GetCharactersInRange(center, radius, list);

            int count = 0;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;
                c.Stagger(Vector3.forward);
                count++;
            }

            _game.LocalPlayer.Message(
                MessageHud.MessageType.TopLeft,
                $"Staggered {count} foes within {radius:0}m"
            );
        }
    }
}
