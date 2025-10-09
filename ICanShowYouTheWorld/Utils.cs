using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;
using Object = UnityEngine.Object;

namespace ICanShowYouTheWorld
{

    public static class CleanupUtils
    {
        public static float TrashRadius = 1f;

        // Things we never delete (prefab or display name substring match; case-insensitive)
        static readonly string[] KeepNames = {
        "Wishbone", "Yagluth", "DragonEgg", "BeltStrength", "$item_beltstrength"
    };

        static string PrefabOf(GameObject go)
        {
            if (!go) return "";
            string n = go.name;
            int i = n.IndexOf('(');              // strip " (Clone)"
            return i > 0 ? n.Substring(0, i) : n;
        }

        static bool IsKept(string prefab, string display)
        {
            for (int i = 0; i < KeepNames.Length; i++)
            {
                string k = KeepNames[i];
                if (!string.IsNullOrEmpty(prefab) && prefab.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (!string.IsNullOrEmpty(display) && display.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // Destroys ground ItemDrop(s) in radius. Set dryRun=true to preview.
        public static int DestroyNearbyDrops(float radius = -1f, bool dryRun = false)
        {
            var p = Player.m_localPlayer; if (!p) return 0;
            if (radius <= 0f) radius = TrashRadius;

            Vector3 center = p.transform.position;

            // Grab colliders near feet (include triggers so item volumes are caught)
            Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);

            int removed = 0, seen = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i]; if (!col) continue;
                var drop = col.GetComponentInParent<ItemDrop>(); // robust even if collider is on child
                if (!drop) continue;

                // ignore anything currently equipped (paranoia guard)
                if (drop.m_itemData != null && drop.m_itemData.m_equipped) continue;

                string prefab = PrefabOf(drop.gameObject);
                string disp = drop.m_itemData != null && drop.m_itemData.m_shared != null
                              ? drop.m_itemData.m_shared.m_name
                              : prefab;

                if (IsKept(prefab, disp)) continue;

                seen++;

                if (dryRun)
                {
                    Console.instance?.Print($"[Cleanup] would remove: {disp} ({prefab})");
                    continue;
                }

                var znv = drop.GetComponent<ZNetView>();
                if (znv != null && znv.IsValid())
                    ZNetScene.instance.Destroy(znv.gameObject); // network-safe
                else
                    UnityEngine.Object.Destroy(drop.gameObject);

                removed++;
            }

            var msg = dryRun
                ? $"Preview: {seen} drop(s) matched within {radius:0.0}m"
                : $"Cleared {removed} drop(s) in {radius:0.0}m";
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, msg);
            Console.instance?.Print("[Cleanup] " + msg);
            return removed;
        }
    }

    // Utility helpers for teleporting and input conversion
    static class TeleportUtils
    {
        public static Vector3 GetSpawnPoint()
        {
            var profile = Game.instance.GetPlayerProfile();
            return profile.HaveCustomSpawnPoint() ? profile.GetCustomSpawnPoint() : profile.GetHomePoint();
        }
    }

    static class Utils
    {
        public static Vector3 MouseToWorldPoint()
        {
            var mp = Input.mousePosition;
            // convert via Minimap; simplified here
            return Camera.main.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 10f));
        }

        // Converts screen/mouse position to world via the minimap logic
        public static Vector3 ScreenToWorldPoint(Vector3 mousePos)
        {
            Vector2 screenPoint = mousePos;
            RectTransform rectTransform = Minimap.instance.m_mapImageLarge.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out var localPoint))
            {
                Vector2 vector = Rect.PointToNormalized(rectTransform.rect, localPoint);
                Rect uvRect = Minimap.instance.m_mapImageLarge.uvRect;
                float mx = uvRect.xMin + vector.x * uvRect.width;
                float my = uvRect.yMin + vector.y * uvRect.height;
                return MapPointToWorld(mx, my);
            }
            return Vector3.zero;
        }

        private static Vector3 MapPointToWorld(float mx, float my)
        {
            int half = Minimap.instance.m_textureSize / 2;
            mx = (mx * Minimap.instance.m_textureSize - half) * Minimap.instance.m_pixelSize;
            my = (my * Minimap.instance.m_textureSize - half) * Minimap.instance.m_pixelSize;
            return new Vector3(mx, 0f, my);
        }

        // Distance in XZ plane between two points
        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }

    static class PlayerUtility
    {
        public static IEnumerable<Player> GetNearbyPlayers(float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, radius, list);
            foreach (var c in list)
                if (c.IsPlayer()) yield return c as Player;
        }
    }

    // Periodic task infrastructure
    class PeriodicTask
    {
        public int Interval;
        public Action Action;
        public int LastFrame;
    }

    static class PeriodicManager
    {
        private static readonly List<PeriodicTask> tasks = new List<PeriodicTask>();
        public static void Register(int interval, Action action)
        {
            tasks.Add(new PeriodicTask { Interval = interval, Action = action, LastFrame = 0 });
        }
        public static void HandlePeriodic()
        {
            int now = Time.frameCount;
            foreach (var t in tasks)
            {
                if (now - t.LastFrame >= t.Interval)
                {
                    t.Action();
                    t.LastFrame = now;
                }
            }
        }
    }
}