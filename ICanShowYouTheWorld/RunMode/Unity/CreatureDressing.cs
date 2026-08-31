using System;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Makes a creature LOOK like the thing the saga says it is, without shipping a single asset.
    ///
    /// The mode's named creatures — the Herald, the Gatherer, the couriers — have until now been
    /// ordinary spawns with a star, a name and sometimes a bigger scale. That is thin for things
    /// the story treats as characters, and the owner asked the obvious question: can we make our
    /// own assets for them?
    ///
    /// You can go a long way before the answer has to be yes, and this is that way. It is not
    /// guesswork: Valheim recolours its own starred creatures through LevelEffects, and that class
    /// is compiled, so its method can be read. It shifts FOUR shader properties —
    ///
    ///     _Hue   _Saturation   _Value   _EmissionColor
    ///
    /// — on the creature's renderers. Those four names are lifted straight out of
    /// LevelEffects.SetupLevelVisualization in this build's IL rather than remembered from a wiki,
    /// which matters: a shader property that does not exist fails silently, exactly like a wrong
    /// prefab name, and this mode has paid for that lesson more than once.
    ///
    /// The one thing that must NOT be copied from LevelEffects is how it writes them. It edits
    /// sharedMaterials through a static per-prefab cache, which is correct for "every two-star
    /// greydwarf looks like this" and catastrophic here: writing a shared material would repaint
    /// every greydwarf in the world gold. So this touches <c>renderer.materials</c>, which Unity
    /// instantiates per renderer — this creature only.
    ///
    /// Where a real asset pipeline would still be needed: a different SILHOUETTE. Everything here
    /// changes colour, size and light, so a Gatherer is an unmistakable greydwarf rather than a
    /// new creature. Swapping the mesh needs an AssetBundle built in Unity 6000.0.x, shipped
    /// beside the DLL and loaded at runtime, with the game's own shaders re-bound by name — a real
    /// piece of work, and a decision rather than a detail.
    /// </summary>
    internal static class CreatureDressing
    {
        // Shader property ids, resolved once. Names verified against LevelEffects in this build.
        private static readonly int HueId = Shader.PropertyToID("_Hue");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ValueId = Shader.PropertyToID("_Value");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// How a named creature should look. Every field is optional — null means "leave it".
        ///
        /// Hue/Saturation/Value are OFFSETS, the way LevelEffects uses them: 0 is untouched.
        /// </summary>
        internal class Look
        {
            public float? Hue;
            public float? Saturation;
            public float? Value;

            /// <summary>Emissive colour. This is what makes a thing look lit from inside.</summary>
            public Color? Emission;

            /// <summary>
            /// Emissive colour for the EYES alone, when the creature keeps them on their own
            /// material. Null leaves them with whatever <see cref="Emission"/> gives the body.
            ///
            /// Worth its own field because the eyes are the one part a player reads at a distance,
            /// in the dark, before anything else resolves. A probe of Greydwarf_Elite showed them
            /// as a separate "eye_red" material on the Standard shader — separate from the body's
            /// two Custom/Creature renderers — so they can burn a different colour than the hide
            /// for nothing but this field. Deer and greyling carry the same arrangement.
            ///
            /// Matched on the material NAME containing "eye", which is asset data and therefore
            /// the one thing here that can silently stop matching. It degrades honestly: no match
            /// means the eyes simply take the body treatment, which is what they did before this
            /// existed. <see cref="Apply"/> says so in the log rather than leaving it a mystery.
            /// </summary>
            public Color? EyeEmission;

            /// <summary>Multiplied into the existing scale, so a star's own growth is kept.</summary>
            public float ScaleMultiplier = 1f;

            /// <summary>A real point light on the creature, or 0 for none.</summary>
            public float LightRange;
            public Color LightColor = Color.white;
            public float LightIntensity = 1.5f;
        }

        /// <summary>
        /// Applies a look. Never throws: a creature that is the wrong colour is a cosmetic
        /// disappointment, and one that threw on spawn is a broken act.
        /// </summary>
        public static void Apply(GameObject creature, Look look)
        {
            if (creature == null || look == null) return;

            try
            {
                if (Math.Abs(look.ScaleMultiplier - 1f) > 0.001f)
                    creature.transform.localScale *= look.ScaleMultiplier;

                Recolour(creature, look);
                AddGlow(creature, look);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Creature dressing failed: " + ex.Message);
            }
        }

        private static void Recolour(GameObject creature, Look look)
        {
            if (look.Hue == null && look.Saturation == null && look.Value == null &&
                look.Emission == null && look.EyeEmission == null)
                return;

            var renderers = creature.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null) return;

            // Only to report an EyeEmission that asked for eyes this creature does not have.
            bool wantedEyes = look.EyeEmission != null;
            bool foundEyes = false;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                // Particle systems and trails carry their own materials and read none of these
                // properties; touching them would instantiate a material per effect for nothing.
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                    renderer is LineRenderer) continue;

                // .materials, NEVER .sharedMaterials — see the class summary. This is the line
                // that keeps the change on one creature instead of on the species.
                var materials = renderer.materials;
                if (materials == null) continue;

                foreach (var material in materials)
                {
                    if (material == null) continue;

                    bool isEye = IsEyeMaterial(material);
                    if (isEye) foundEyes = true;

                    // Every write is guarded: these four exist on Valheim's creature shader and
                    // not on, say, an eye or a cape using something else. Setting a property a
                    // shader does not have is a silent no-op in Unity, which is exactly the kind
                    // of silence this mode refuses to build on.
                    if (look.Hue != null && material.HasProperty(HueId))
                        material.SetFloat(HueId, look.Hue.Value);

                    if (look.Saturation != null && material.HasProperty(SaturationId))
                        material.SetFloat(SaturationId, look.Saturation.Value);

                    if (look.Value != null && material.HasProperty(ValueId))
                        material.SetFloat(ValueId, look.Value.Value);

                    // The eyes win where both are set: a look that names them has said something
                    // more specific than the one covering the whole hide.
                    Color? emission = isEye && look.EyeEmission != null ? look.EyeEmission : look.Emission;

                    if (emission != null && material.HasProperty(EmissionId))
                    {
                        material.SetColor(EmissionId, emission.Value);

                        // Standard-shader materials ignore _EmissionColor unless the keyword is
                        // on. Valheim's creature shader does not need it; harmless where it is
                        // not read, and the difference between glowing and not where it is. The
                        // eyes are Standard on every creature probed, so this is the line that
                        // makes EyeEmission visible at all.
                        try { material.EnableKeyword("_EMISSION"); } catch { }
                    }
                }
            }

            if (wantedEyes && !foundEyes)
            {
                Debug.Log("[ICanShowYouTheWorld] " + creature.name + " has no eye material — its " +
                          "EyeEmission was ignored and the body's emission used throughout. Cosmetic only.");
            }
        }

        /// <summary>
        /// Whether a material is a creature's eyes, by name.
        ///
        /// Valheim names them plainly — "eye_red" on greydwarves and their kin — and there is no
        /// component or shader that distinguishes an eye, so the name is the only handle available.
        /// A miss costs the eye treatment and nothing else; see <see cref="Look.EyeEmission"/>.
        /// </summary>
        private static bool IsEyeMaterial(Material material) =>
            material.name != null &&
            material.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// A real point light on the creature, so it lights the ground and the trees around it.
        ///
        /// This is the half that sells a light-carrier at distance, and it is the reason the
        /// couriers get one: the story says they are visibly laden and hurrying through the dark,
        /// and "a starred greydwarf forty metres away in a night forest" was not visible at all.
        /// An emissive skin glows only where you can already see the creature; a light announces
        /// it through the trees.
        /// </summary>
        private static void AddGlow(GameObject creature, Look look)
        {
            if (look.LightRange <= 0f) return;

            var holder = new GameObject("icsytw_glow");
            holder.transform.SetParent(creature.transform, worldPositionStays: false);
            holder.transform.localPosition = Vector3.up;

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = look.LightColor;
            light.range = look.LightRange;
            light.intensity = look.LightIntensity;

            // No shadows on purpose. A shadow-casting point light on a moving creature is one of
            // the most expensive things you can add to a scene, and it buys nothing here — the
            // job is "you can see it coming", not "it is lit correctly".
            light.shadows = LightShadows.None;
        }

        // --- The saga's named things, in one place so the fiction and the look stay together ---

        /// <summary>
        /// The oldest splinter, glutted on what the forest took. Deadwood grey-green, and lit from
        /// inside in PROPORTION to the lights it ate — the arrival line reads the ledger out loud,
        /// and now so does the creature.
        ///
        /// The eyes go their own way, and they are the part that arrives first. A probe of
        /// Greydwarf_Elite put them on a separate "eye_red" material, so they can be pushed well
        /// past the hide without washing the whole creature out: they start as banked embers on a
        /// Gatherer that has eaten nothing and end near white-gold on a full one. It comes at you
        /// through trees at night, and this is what you see before the silhouette resolves.
        ///
        /// For scale, Valheim's own two-star brute is a 1.2x greydwarf with a hue shift of -0.11
        /// and no emission at all. Everything here is deliberately an order of magnitude louder
        /// than that, because this one is a character rather than a difficulty tier.
        /// </summary>
        public static Look Gatherer(int lightsEaten)
        {
            float fed = Mathf.Clamp01(lightsEaten / 8f);

            return new Look
            {
                Hue = 0.08f,
                Saturation = -0.25f,
                Value = -0.2f + fed * 0.25f,
                Emission = Color.Lerp(new Color(0.10f, 0.09f, 0.04f), new Color(1.00f, 0.86f, 0.35f), fed),
                EyeEmission = Color.Lerp(new Color(0.85f, 0.35f, 0.05f), new Color(1.00f, 0.95f, 0.70f), fed),
                LightRange = 6f + fed * 10f,
                LightColor = new Color(1f, 0.85f, 0.45f),
                LightIntensity = 1f + fed * 1.5f,
            };
        }

        /// <summary>
        /// A greydwarf given a burden and a title. The brand IS the cargo, so the cargo is what
        /// shows: a carried light, bright enough to find through trees at night.
        ///
        /// Toned down from the first version, which read as a floodlight (owner: "the couriers are
        /// REALLY bright"). RANGE is what makes a light findable through trees and INTENSITY is
        /// what makes it harsh, so the two moved in opposite directions: range barely dropped,
        /// intensity fell by two thirds. The thing should read as something carrying a lamp
        /// through the woods, not as the lamp.
        /// </summary>
        public static Look Courier() => new Look
        {
            Saturation = -0.15f,
            Value = 0.05f,
            Emission = new Color(0.42f, 0.34f, 0.13f),
            LightRange = 10f,
            LightColor = new Color(1f, 0.88f, 0.50f),
            LightIntensity = 0.7f,
        };

        /// <summary>
        /// The herd's guardian: pale, and carrying more original light than any deer alive.
        /// Brighter and cooler than the forest's things, because it is the opposite of them.
        /// </summary>
        public static Look Herald() => new Look
        {
            Saturation = -0.35f,
            Value = 0.3f,
            Emission = new Color(0.65f, 0.78f, 1.00f),
            LightRange = 10f,
            LightColor = new Color(0.72f, 0.84f, 1f),
            LightIntensity = 1.6f,
        };
    }
}
