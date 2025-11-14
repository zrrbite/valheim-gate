using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.GameAPI
{
    /// <summary>
    /// Concrete implementation of IGameAPI that wraps the Valheim game API.
    /// All direct Valheim API calls should go through this class.
    /// </summary>
    public class ValheimGameAPI : IGameAPI
    {
        // ===== Player Access =====

        public Player LocalPlayer => Player.m_localPlayer;

        public bool IsLocalPlayerValid => Player.m_localPlayer != null && !Player.m_localPlayer.IsDead();

        // ===== Character Queries =====

        public IEnumerable<Character> GetCharactersInRange(Vector3 position, float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(position, radius, list);
            return list;
        }

        public IEnumerable<Character> GetAllCharacters()
        {
            return Character.GetAllCharacters();
        }

        // ===== World & Position =====

        public Vector3 GetMapCursorWorldPosition()
        {
            if (!IsMapOpen || Minimap.instance == null)
            {
                return Vector3.zero;
            }

            try
            {
                Vector3 mousePos = Input.mousePosition;
                return ScreenToWorldPoint(mousePos);
            }
            catch (Exception)
            {
                return Vector3.zero;
            }
        }

        public bool IsMapOpen
        {
            get
            {
                return Minimap.instance != null &&
                       Minimap.instance.m_mapImageLarge != null &&
                       Minimap.instance.m_mapImageLarge.gameObject.activeSelf;
            }
        }

        public Vector3 GetSafeSpawnPosition(Vector3 offset)
        {
            if (!IsLocalPlayerValid)
            {
                return Vector3.zero;
            }

            Vector3 position = LocalPlayer.transform.position + offset;

            // Try to find ground below
            if (FindGroundPosition(position + Vector3.up * 10f, 20f, out Vector3 groundPos))
            {
                position = groundPos + Vector3.up * 0.5f; // Slightly above ground
            }

            return position;
        }

        // ===== Teleportation =====

        public void TeleportPlayer(Vector3 position)
        {
            if (!IsLocalPlayerValid)
            {
                return;
            }

            LocalPlayer.transform.position = position;
        }

        public int TeleportPlayersInRange(Vector3 fromPosition, float radius, Vector3 toPosition)
        {
            var characters = GetCharactersInRange(fromPosition, radius);
            int count = 0;

            foreach (var character in characters)
            {
                if (character.IsPlayer())
                {
                    character.transform.position = toPosition;
                    count++;
                }
            }

            return count;
        }

        // ===== Messaging =====

        public void ShowMessage(string text, MessageType type = MessageType.Center)
        {
            if (!IsLocalPlayerValid)
            {
                return;
            }

            LocalPlayer.Message((MessageHud.MessageType)type, text);
        }

        public void PrintToConsole(string text)
        {
            Console.instance?.Print(text);
        }

        // ===== Prefab & Spawning =====

        public GameObject SpawnPrefab(string prefabName, Vector3 position, Quaternion? rotation = null)
        {
            if (!IsZNetSceneReady)
            {
                Debug.LogWarning($"[ValheimGameAPI] Cannot spawn prefab '{prefabName}': ZNetScene not ready");
                return null;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[ValheimGameAPI] Prefab '{prefabName}' not found");
                return null;
            }

            Quaternion rot = rotation ?? Quaternion.identity;
            return UnityEngine.Object.Instantiate(prefab, position, rot);
        }

        public bool PrefabExists(string prefabName)
        {
            if (!IsZNetSceneReady)
            {
                return false;
            }

            return ZNetScene.instance.GetPrefab(prefabName) != null;
        }

        public IEnumerable<string> GetAllPrefabNames()
        {
            if (!IsZNetSceneReady)
            {
                return Enumerable.Empty<string>();
            }

            // Note: This would require reflection or direct access to ZNetScene's prefab collection
            // For now, return empty. This can be implemented when needed.
            Debug.LogWarning("[ValheimGameAPI] GetAllPrefabNames not fully implemented - requires prefab enumeration");
            return Enumerable.Empty<string>();
        }

        // ===== Game State =====

        public bool IsGameActive
        {
            get
            {
                return Game.instance != null &&
                       !Game.instance.IsShuttingDown() &&
                       IsLocalPlayerValid;
            }
        }

        public bool IsZNetSceneReady
        {
            get
            {
                return ZNetScene.instance != null;
            }
        }

        public float GetGameTime()
        {
            if (EnvMan.instance == null)
            {
                return 0f;
            }

            return EnvMan.instance.GetDayFraction();
        }

        // ===== Environment & Physics =====

        public bool FindGroundPosition(Vector3 position, float maxDistance, out Vector3 hitPoint)
        {
            if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, maxDistance))
            {
                hitPoint = hit.point;
                return true;
            }

            hitPoint = Vector3.zero;
            return false;
        }

        public bool IsPositionInWater(Vector3 position)
        {
            if (ZoneSystem.instance == null)
            {
                return false;
            }

            float waterLevel = ZoneSystem.instance.m_waterLevel;
            return position.y < waterLevel;
        }

        // ===== Combat & Damage =====

        public void DamageCharacter(Character character, float damage, HitData.DamageTypes damageType)
        {
            if (character == null || character.IsDead())
            {
                return;
            }

            HitData hitData = new HitData();
            hitData.m_damage = damageType;
            hitData.m_damage.m_damage = damage;
            hitData.m_point = character.GetCenterPoint();

            character.Damage(hitData);
        }

        public void HealCharacter(Character character, float amount)
        {
            if (character == null || character.IsDead())
            {
                return;
            }

            character.Heal(amount);
        }

        public void KillCharacter(Character character)
        {
            if (character == null || character.IsDead())
            {
                return;
            }

            HitData hitData = new HitData();
            hitData.m_damage.m_damage = character.GetMaxHealth() * 10f;
            character.Damage(hitData);
        }

        // ===== Skills & Stats =====

        public void SetPlayerRunSpeed(float speed)
        {
            if (!IsLocalPlayerValid)
            {
                return;
            }

            LocalPlayer.m_runSpeed = speed;
        }

        public float GetPlayerRunSpeed()
        {
            if (!IsLocalPlayerValid)
            {
                return 0f;
            }

            return LocalPlayer.m_runSpeed;
        }

        public void ResetForsakenPowerCooldown()
        {
            if (!IsLocalPlayerValid)
            {
                return;
            }

            LocalPlayer.m_guardianPowerCooldown = 0f;
        }

        // ===== Private Helper Methods =====

        /// <summary>
        /// Convert screen position to world position via minimap.
        /// Based on the existing Utils.ScreenToWorldPoint implementation.
        /// </summary>
        private Vector3 ScreenToWorldPoint(Vector3 mousePos)
        {
            if (Minimap.instance == null || Minimap.instance.m_mapImageLarge == null)
            {
                return Vector3.zero;
            }

            Vector2 screenPoint = mousePos;
            RectTransform rectTransform = Minimap.instance.m_mapImageLarge.transform as RectTransform;

            if (rectTransform == null)
            {
                return Vector3.zero;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, null, out var localPoint))
            {
                Vector2 vector = Rect.PointToNormalized(rectTransform.rect, localPoint);
                Rect uvRect = Minimap.instance.m_mapImageLarge.uvRect;

                float mx = uvRect.xMin + vector.x * uvRect.width;
                float my = uvRect.yMin + vector.y * uvRect.height;

                return MapPointToWorld(mx, my);
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Convert map UV coordinates to world position.
        /// Based on the existing Utils.MapPointToWorld implementation.
        /// </summary>
        private Vector3 MapPointToWorld(float mx, float my)
        {
            if (Minimap.instance == null)
            {
                return Vector3.zero;
            }

            int half = Minimap.instance.m_textureSize / 2;
            float worldX = (mx * Minimap.instance.m_textureSize - half) * Minimap.instance.m_pixelSize;
            float worldZ = (my * Minimap.instance.m_textureSize - half) * Minimap.instance.m_pixelSize;

            return new Vector3(worldX, 0f, worldZ);
        }
    }
}
