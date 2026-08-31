using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Reports what a creature is actually MADE of, so the next step in giving the saga's named
    /// things their own look can be chosen from evidence instead of from assumption.
    ///
    /// CreatureDressing currently shifts _Hue/_Saturation/_Value/_EmissionColor, four names lifted
    /// from LevelEffects.SetupLevelVisualization. That much is verified. Everything BEYOND it is
    /// not: whether the creature shader carries an albedo slot we could bind a hand-painted texture
    /// to, what that slot is called, whether the bound texture is readable, and what the rig offers
    /// as attachment points if we graft parts from other prefabs. Each of those is a fact about
    /// Unity data — invisible to the compiled assembly, unavailable to ikdasm, and, when guessed
    /// wrong, SILENTLY ignored. That failure mode has cost this mode more than once, which is the
    /// whole reason this file exists before any art does.
    ///
    /// Two design rules follow from "this is a probe":
    ///
    /// It must not change what it measures. Every read goes through sharedMaterials, never
    /// materials — the latter instantiates a per-renderer copy as a side effect, so a probe written
    /// the convenient way would quietly alter the creature it was sent to describe.
    ///
    /// It must not be able to break a run. Every section is independently guarded; a section that
    /// throws reports its own failure and the rest of the report still lands.
    /// </summary>
    internal static class CreatureProbe
    {
        /// <summary>How far to look for something to probe.</summary>
        private const float SearchRadius = 40f;

        /// <summary>How far off the crosshair a creature may be and still count as "that one".</summary>
        private const float MaxAimAngle = 35f;

        /// <summary>Depth guard for the hierarchy walk. Rigs are deep; corrupt ones are infinite.</summary>
        private const int MaxDepth = 12;

        /// <summary>Line budget for the hierarchy section, which is the only unbounded one.</summary>
        private const int MaxHierarchyLines = 400;

        /// <summary>Past this the log is rolled rather than appended to, so it stays openable.</summary>
        private const long MaxLogBytes = 4L * 1024 * 1024;

        /// <summary>
        /// The creature the player is looking at.
        ///
        /// Deliberately NOT a physics raycast. A raycast needs a layer mask to avoid stopping on
        /// the tree in front of the target, and the mask is one more thing to get silently wrong.
        /// Angle-to-aim over the characters already in range needs no mask, cannot be blocked by
        /// scenery, and is forgiving enough to hit a moving greydwarf — which matters, because the
        /// things worth probing run away.
        /// </summary>
        public static GameObject FindTarget()
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;

            Vector3 origin;
            Vector3 forward;
            AimFrom(player, out origin, out forward);

            var nearby = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, SearchRadius, nearby);

            Character best = null;
            float bestAngle = MaxAimAngle;

            foreach (var c in nearby)
            {
                // ReferenceEquals, not ==: a character destroyed between the range query and here
                // compares equal to null through Unity's overload while still being a usable
                // reference, and the mode has been bitten by the difference before.
                if (ReferenceEquals(c, null) || c == null) continue;
                if (c.IsPlayer()) continue;

                Vector3 delta = c.transform.position + Vector3.up - origin;
                if (delta.sqrMagnitude < 0.01f) continue;

                float angle = Vector3.Angle(forward, delta);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = c;
                }
            }

            return best == null ? null : best.gameObject;
        }

        /// <summary>
        /// Where the player is looking. GameCamera is the truth when it exists, because the
        /// creature under the crosshair is the one under the CAMERA's crosshair; the eye transform
        /// is the fallback for the frames where the camera has not caught up, and the body is the
        /// last resort so this can never return a zero vector.
        /// </summary>
        private static void AimFrom(Player player, out Vector3 origin, out Vector3 forward)
        {
            try
            {
                var camera = GameCamera.instance;
                if (camera != null)
                {
                    origin = camera.transform.position;
                    forward = camera.transform.forward;
                    return;
                }
            }
            catch { }

            var eye = player.m_eye;
            if (eye != null)
            {
                origin = eye.position;
                forward = eye.forward;
                return;
            }

            origin = player.transform.position + Vector3.up;
            forward = player.transform.forward;
        }

        /// <summary>
        /// Writes a full report on <paramref name="creature"/> and returns the path, or null if it
        /// could not be written. Appends, so several creatures can be compared from one session.
        /// </summary>
        public static string WriteReport(GameObject creature)
        {
            if (creature == null) return null;

            string report;
            try
            {
                report = BuildReport(creature);
            }
            catch (Exception ex)
            {
                report = "PROBE FAILED WHILE BUILDING REPORT: " + ex;
            }

            try
            {
                string path = Application.persistentDataPath + "/ICSYTW_probe.txt";

                // Rolled rather than trimmed: a probe log is only ever read whole, and half a
                // report is worse than a fresh file.
                if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                    File.Delete(path);

                File.AppendAllText(path, report);

                // Also to the player log, so a report survives a machine whose config directory
                // is awkward to reach — the Deck, mainly.
                Debug.Log("[ICanShowYouTheWorld] Creature probe written to " + path);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Creature probe could not be written: " + ex.Message);
                return null;
            }
        }

        private static string BuildReport(GameObject creature)
        {
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("================================================================");
            sb.AppendLine("  CREATURE PROBE — " + Describe(creature));
            sb.AppendLine("  mod " + ModVersion.VERSION + ", day " + SafeDay());
            sb.AppendLine("================================================================");

            Section(sb, "1. IDENTITY", () => Identity(sb, creature));
            Section(sb, "2. HIERARCHY  (• renderer, ○ inactive, ☀ light)", () => Hierarchy(sb, creature));

            // Unique MATERIALS are collected while walking the renderers and dumped afterwards.
            //
            // Materials, not shaders, and this is the correction to a probe that lied for its whole
            // life. It used to key on the shader and then read the values out of `new Material(shader)`
            // — a blank material built from the shader — while captioning the table with the name of a
            // real material it never touched. Every table it has ever printed was the shader's
            // DEFAULTS: hence textures reading <none> on a creature that plainly has them, and
            // _Hue/_Saturation/_Value reading 0 on a two-star greydwarf that Valheim demonstrably
            // tinted. A probe that reports defaults as observations is worse than no probe, because
            // it is believed.
            //
            // Keying on the material also fixes a subtler miss. LevelEffects tints only
            // m_mainRender, so a Greydwarf_Elite has two renderers sharing one shader where only
            // one is tinted; per-shader sampling could pick either and never said which.
            var materials = new List<MaterialUse>();
            Section(sb, "3. RENDERERS AND MATERIALS", () => Renderers(sb, creature, materials));
            Section(sb, "4. MATERIAL PROPERTIES  (every slot, read from the creature's own material)",
                    () => MaterialProperties(sb, materials));

            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>Runs one section, so a section that throws costs only itself.</summary>
        private static void Section(StringBuilder sb, string title, Action body)
        {
            sb.AppendLine();
            sb.AppendLine("--- " + title + " ---");
            try { body(); }
            catch (Exception ex) { sb.AppendLine("  SECTION FAILED: " + ex.Message); }
        }

        private static void Identity(StringBuilder sb, GameObject creature)
        {
            sb.AppendLine("  prefab      : " + Describe(creature));
            sb.AppendLine("  position    : " + creature.transform.position);
            sb.AppendLine("  scale       : " + creature.transform.localScale);

            var character = creature.GetComponent<Character>();
            if (character == null)
            {
                sb.AppendLine("  character   : none (not a Character)");
            }
            else
            {
                sb.AppendLine("  level       : " + character.GetLevel() + "  (1 = no stars)");
                sb.AppendLine("  health      : " + character.GetHealth() + " / " + character.GetMaxHealth());
                sb.AppendLine("  tamed       : " + character.IsTamed());
                sb.AppendLine("  name        : " + character.m_name);
            }

            // The reason this line is here: LevelEffects is how the GAME changes a creature's look
            // per star, and alongside the colour shift it swaps whole GameObjects in and out
            // (m_baseEnableObject off, LevelSetup.m_enableObject on). That is a silhouette change
            // using nothing but assets already in the build, and it is the cheapest path to the
            // named things looking different. Whether a given creature ships with such objects is
            // exactly what this reports.
            var effects = creature.GetComponentInChildren<LevelEffects>(includeInactive: true);
            if (effects == null)
            {
                sb.AppendLine("  LevelEffects: none");
            }
            else
            {
                sb.AppendLine("  LevelEffects: present");
                sb.AppendLine("    m_mainRender      : " + NameOf(effects.m_mainRender));
                sb.AppendLine("    m_baseEnableObject: " + NameOf(effects.m_baseEnableObject));

                var setups = effects.m_levelSetups;
                sb.AppendLine("    level setups      : " + (setups == null ? 0 : setups.Count));
                if (setups != null)
                {
                    for (int i = 0; i < setups.Count; i++)
                    {
                        var s = setups[i];
                        if (s == null) continue;
                        sb.AppendLine(string.Format(
                            "      [{0}] scale {1:0.##}  hue {2:0.##}  sat {3:0.##}  val {4:0.##}  enableObject {5}",
                            i, s.m_scale, s.m_hue, s.m_saturation, s.m_value, NameOf(s.m_enableObject)));
                    }
                }
            }

            var nview = creature.GetComponent<ZNetView>();
            sb.AppendLine("  ZNetView    : " + (nview == null
                ? "none"
                : "present, owner=" + nview.IsOwner() + ", valid=" + nview.IsValid()));
        }

        private static void Hierarchy(StringBuilder sb, GameObject creature)
        {
            int lines = 0;
            WalkHierarchy(sb, creature.transform, 0, ref lines);
            if (lines >= MaxHierarchyLines)
                sb.AppendLine("  ... truncated at " + MaxHierarchyLines + " lines");
        }

        private static void WalkHierarchy(StringBuilder sb, Transform t, int depth, ref int lines)
        {
            if (t == null || depth > MaxDepth || lines >= MaxHierarchyLines) return;

            var marks = new StringBuilder();
            if (t.GetComponent<Renderer>() != null) marks.Append('•');
            if (!t.gameObject.activeSelf) marks.Append('○');
            if (t.GetComponent<Light>() != null) marks.Append('☀');

            sb.AppendLine("  " + new string(' ', depth * 2) + t.name +
                          (marks.Length > 0 ? "   " + marks : string.Empty));
            lines++;

            for (int i = 0; i < t.childCount; i++)
                WalkHierarchy(sb, t.GetChild(i), depth + 1, ref lines);
        }

        /// <summary>One material, and the first renderer found wearing it. See <see cref="BuildReport"/>.</summary>
        private struct MaterialUse
        {
            public Material Material;
            public string OwnerPath;
        }

        private static void Renderers(StringBuilder sb, GameObject creature, List<MaterialUse> seen)
        {
            var renderers = creature.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
            {
                sb.AppendLine("  none");
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                sb.AppendLine();
                sb.AppendLine("  " + renderer.GetType().Name + "  \"" + PathTo(creature.transform, renderer.transform) + "\"" +
                              (renderer.enabled ? string.Empty : "  [disabled]"));

                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null)
                {
                    sb.AppendLine("    mesh   : " + NameOf(skinned.sharedMesh) +
                                  ", bones: " + (skinned.bones == null ? 0 : skinned.bones.Length));
                }

                // sharedMaterials, NOT materials. See the class summary: reading through
                // .materials would instantiate copies and change the thing being measured.
                var materials = renderer.sharedMaterials;
                if (materials == null) continue;

                string path = PathTo(creature.transform, renderer.transform);

                foreach (var material in materials)
                {
                    if (material == null) { sb.AppendLine("    material: <null>"); continue; }

                    var shader = material.shader;
                    sb.AppendLine("    material: \"" + material.name + "\"  shader: \"" +
                                  (shader == null ? "<null>" : shader.name) + "\"");

                    // Reference equality, not Equals: two renderers may carry genuinely distinct
                    // materials that share a name (Cube and Cube.001 are both "greydwarf_elite"),
                    // and those are exactly the pair worth telling apart — LevelEffects tints only
                    // one of them.
                    bool known = false;
                    for (int i = 0; i < seen.Count; i++)
                    {
                        if (ReferenceEquals(seen[i].Material, material)) { known = true; break; }
                    }

                    if (!known) seen.Add(new MaterialUse { Material = material, OwnerPath = path });
                }
            }
        }

        private static void MaterialProperties(StringBuilder sb, List<MaterialUse> seen)
        {
            if (seen.Count == 0) { sb.AppendLine("  none"); return; }

            foreach (var use in seen)
            {
                var material = use.Material;
                if (material == null) continue;

                var shader = material.shader;
                if (shader == null) { sb.AppendLine("  material \"" + material.name + "\" has no shader"); continue; }

                sb.AppendLine();
                sb.AppendLine("  material \"" + material.name + "\"   shader \"" + shader.name + "\"");
                sb.AppendLine("    on: " + use.OwnerPath);

                int count;
                try { count = shader.GetPropertyCount(); }
                catch (Exception ex) { sb.AppendLine("    property list unavailable: " + ex.Message); continue; }

                for (int i = 0; i < count; i++)
                {
                    string line;
                    try
                    {
                        string name = shader.GetPropertyName(i);
                        ShaderPropertyType type = shader.GetPropertyType(i);

                        // Read from the creature's OWN material. Nothing is constructed and nothing
                        // is assigned, so this still observes without disturbing — the property that
                        // the old `new Material(shader)` sample was wrongly believed to provide.
                        line = string.Format("    {0,-28} {1,-8} {2}", name, type, ValueOf(material, name, type));
                    }
                    catch (Exception ex)
                    {
                        line = "    <property " + i + " unreadable: " + ex.Message + ">";
                    }
                    sb.AppendLine(line);
                }
            }
        }

        /// <summary>
        /// The current value of one shader slot, formatted for reading rather than for parsing.
        ///
        /// Textures report readability on purpose. A texture with isReadable false cannot be
        /// sampled on the CPU, which decides whether a custom skin can be DERIVED from the game's
        /// own art or has to be painted from nothing — and that is the difference between an
        /// afternoon and a project.
        /// </summary>
        private static string ValueOf(Material material, string name, ShaderPropertyType type)
        {
            if (material == null || !material.HasProperty(name)) return string.Empty;

            try
            {
                switch (type)
                {
                    case ShaderPropertyType.Color:
                        return material.GetColor(name).ToString();

                    case ShaderPropertyType.Vector:
                        return material.GetVector(name).ToString();

                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        return material.GetFloat(name).ToString("0.###");

                    case ShaderPropertyType.Int:
                        return material.GetInt(name).ToString();

                    case ShaderPropertyType.Texture:
                        var texture = material.GetTexture(name);
                        if (texture == null) return "<none>";
                        return string.Format("\"{0}\" {1}x{2} readable={3}",
                                             texture.name, texture.width, texture.height, texture.isReadable);
                }
            }
            catch (Exception ex) { return "<unreadable: " + ex.Message + ">"; }

            return string.Empty;
        }

        // --- small helpers -------------------------------------------------------------------

        /// <summary>
        /// The creature's prefab name, without Unity's "(Clone)" suffix.
        ///
        /// global::Utils is the GAME's helper in assembly_utils — the mod has a static Utils of
        /// its own in this namespace, which otherwise wins name resolution and does not have this
        /// method. Same call LevelEffects makes to key its material cache.
        /// </summary>
        public static string Describe(GameObject go)
        {
            try { return global::Utils.GetPrefabName(go); }
            catch { return go == null ? "<null>" : go.name; }
        }

        private static string NameOf(UnityEngine.Object o) => o == null ? "<none>" : o.name;

        private static string PathTo(Transform root, Transform t)
        {
            var parts = new List<string>();
            while (t != null && t != root)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return parts.Count == 0 ? "<root>" : string.Join("/", parts.ToArray());
        }

        private static string SafeDay()
        {
            try { return EnvMan.instance == null ? "?" : EnvMan.instance.GetDay().ToString(); }
            catch { return "?"; }
        }
    }
}
