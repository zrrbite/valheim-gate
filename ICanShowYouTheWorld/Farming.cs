using System;
using UnityEngine;
using System.Collections.Generic;

namespace ICanShowYouTheWorld
{
    using UnityEngine;
    using System.Collections.Generic;

    public static class PlantingTools
    {
        // Simple knobs
        public static int GridRows = 4;
        public static int GridCols = 4;
        public static float ExtraMargin = 0.25f;  // added to 2*growRadius
        public static float MaxSlopeDeg = 22f;    // skip steep ground
        public static float RayUp = 20f;    // raycast up
        public static float RayDown = 60f;    // raycast down

        // Cycle a few common saplings/crops (edit to taste)
        public static readonly string[] SeedPrefabs = {
        // Crops (need cultivated soil to be Healthy)
         "sapling_magecap", "sapling_jotunpuffs", "sapling_carrot","sapling_turnip","sapling_onion","sapling_barley","sapling_flax",

    };
        public static int SeedIndex = 0;

        public static void CycleSeed(int delta)
        {
            if (SeedPrefabs.Length == 0) return;
            SeedIndex = (SeedIndex + delta + SeedPrefabs.Length) % SeedPrefabs.Length;
            Show("Seed: " + SeedPrefabs[SeedIndex]);
        }

        public static void PlantSelectedGrid()
        {
            if (SeedPrefabs.Length == 0) return;
            PlantGrid(SeedPrefabs[SeedIndex], GridRows, GridCols, -1f);
        }

        /// Plants a rows×cols grid using prefabName.
        /// spacingOverride < 0 = auto from Plant.m_growRadius (Healthy spacing).
        public static int PlantGrid(string prefabName, int rows, int cols, float spacingOverride)
        {
            var zs = ZNetScene.instance;
            var pl = Player.m_localPlayer;
            if (zs == null || pl == null) { Show("Not ready"); return 0; }

            var prefab = zs.GetPrefab(prefabName);
            if (prefab == null) { Show("Missing prefab: " + prefabName); return 0; }

            // spacing from prefab’s Plant (Healthy distance)
            float spacing = spacingOverride;
            var plant = prefab.GetComponent<Plant>();
            if (spacing <= 0f)
            {
                if (plant != null)
                    spacing = Mathf.Max(0.8f, plant.m_growRadius * 2f + ExtraMargin);
                else
                    spacing = GuessSpacing(prefabName); // fallback
            }

            // center on mouse-ground (fallback: in front of player)
            Vector3 center;
            if (!TryMouseGround(out center))
            {
                Vector3 probe = pl.transform.position + pl.transform.forward * 3f + Vector3.up * RayUp;
                RaycastHit hit2;
                if (Physics.Raycast(probe, Vector3.down, out hit2, RayUp + RayDown))
                    center = hit2.point;
                else
                    center = pl.transform.position;
            }

            // orient grid to the player facing
            Vector3 fwd = pl.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            float cosMaxSlope = Mathf.Cos(MaxSlopeDeg * Mathf.Deg2Rad);
            int planted = 0;

            // center offsets
            float ox = -(cols - 1) * 0.5f;
            float oy = -(rows - 1) * 0.5f;

            // inside PlantGrid(...)
            float lastY = center.y;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 offset = (right * (ox + c) + fwd * (oy + r)) * spacing;
                    Vector3 xz = center + offset;

                    Vector3 hitPos, hitNormal;
                    if (!TryGroundAt(xz, RayUp, RayDown, out hitPos, out hitNormal))
                        continue;

                    // slope gate
                    if (Vector3.Dot(hitNormal, Vector3.up) < cosMaxSlope)
                        continue;

                    // clamp big spikes (tree/rock lips)
                    float y = ClampStep(lastY, hitPos.y, 1.25f);
                    lastY = y;

                    Vector3 pos = new Vector3(hitPos.x, y + 0.02f, hitPos.z);

                    // (optional) avoid overlaps
                    // if (Physics.CheckSphere(pos, spacing * 0.45f)) continue;

                    var go = Object.Instantiate(prefab, pos, Quaternion.identity);

                    // (optional) align to ground a bit so visuals look snug
                    // go.transform.up = Vector3.Slerp(go.transform.up, hitNormal, 0.6f);

                    planted++;
                }
            }

            Show("Planted " + planted + " × " + prefabName + " @ ~" + spacing.ToString("0.0") + "m");
            return planted;
        }

        // --- helpers ---
        static float GuessSpacing(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("magecap") || n.Contains("jotun")) return 1.4f; // Mistlands shrooms
            if (n.Contains("carrot") || n.Contains("turnip") || n.Contains("onion")) return 1.5f;
            if (n.Contains("barley") || n.Contains("flax")) return 1.5f;
            if (n.Contains("sapling")) return 4.0f; // trees
            return 2.0f;
        }

        static bool TryMouseGround(out Vector3 point)
        {
            var cam = Camera.main;
            if (!cam) { point = Vector3.zero; return false; }
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 500f)) { point = hit.point; return true; }
            point = Vector3.zero; return false;
        }

        static void Show(string s)
        {
            if (Player.m_localPlayer)
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, s);
            if (Console.instance) Console.instance.Print(s);
        }

        // --- Ground sampling that prefers terrain and never returns “air” ---
        // ray: cast from (xz + up) downward; choose the LOWEST non-Rigidbody hit.
        // fallback: Heightmap.GetHeight when physics fails (e.g., no collider).
        static bool TryGroundAt(Vector3 xz, float up, float down, out Vector3 point, out Vector3 normal)
        {
            Vector3 origin = xz + Vector3.up * up;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, up + down, ~0, QueryTriggerInteraction.Ignore);

            int best = -1;
            float minY = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                // Ignore dynamic things (trees/creatures often have rigidbodies)
                if (h.collider && h.collider.attachedRigidbody != null) continue;

                if (h.point.y < minY)
                {
                    minY = h.point.y;
                    best = i;
                }
            }

            if (best >= 0)
            {
                point = hits[best].point;
                normal = hits[best].normal;
                return true;
            }

            // Fallback: terrain height (never “air”)
            float y;
            if (Heightmap.GetHeight(new Vector3(xz.x, 0f, xz.z), out y))
            {
                point = new Vector3(xz.x, y, xz.z);
                normal = Vector3.up;
                return true;
            }

            point = xz;
            normal = Vector3.up;
            return false;
        }

        // Optional: reject huge vertical steps (e.g., from a tree branch) and clamp to last good Y.
        static float ClampStep(float lastY, float newY, float maxStep = 1.25f)
        {
            return Mathf.Abs(newY - lastY) > maxStep ? lastY : newY;
        }
    }
}
