using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The heart of Act I's story: a deer's light, and the race to keep it.
    ///
    /// Eikthyr's herd is being farmed. Every deer that dies gives up a pale light, and the
    /// greydwarves come to the carcass to take it — which is why they have swarmed every kill
    /// since the act began. Eikthyr is chained and raging in the dark because he cannot stop it.
    /// Kill one of his deer and you are doing what they do, UNLESS you take the light back.
    ///
    /// Mechanically that is a race with a clock rather than a fight over an object. The light
    /// fades on a timer; the greydwarves are simply what stands between you and it. No AI work,
    /// no contested pickup, and the tension is identical: get there, or the forest gets it.
    ///
    /// The failure line matters as much as the success one. "The forest takes it" is the story
    /// being told at the exact moment the player feels the loss.
    /// </summary>
    /// <summary>
    /// Makes a deer's light something you TAKE rather than something you walk near.
    ///
    /// "Let the user actually pick it up to make it more hands on" — and the owner is right for a
    /// mechanical reason too: proximity fired through bushes and behind rocks, so lights were
    /// being collected without ever being seen. An E-press proves the player found it.
    ///
    /// Implements the game's own Interactable/Hoverable, so the standard crosshair prompt does all
    /// the work. Claimed is read by StolenLights.Tick, which owns the consequences.
    /// </summary>
    internal class LightPickup : MonoBehaviour, Interactable, Hoverable
    {
        public bool Claimed { get; private set; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || Claimed) return false;

            Claimed = true;
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverText() => "A deer's light\n[<color=yellow><b>$KEY_Use</b></color>] Take it back";

        public string GetHoverName() => "A deer's light";
    }

    /// <summary>
    /// Carries a light away toward whatever is collecting it.
    ///
    /// The fade timer said "the forest takes it" as an expiry; this says it as MOTION — the light
    /// visibly slips off toward the collector, and taking it back means running it down through
    /// the pack. Same rule, told where the player is looking.
    ///
    /// The wisp is a physics item, so its rigidbody goes kinematic: gravity would drag the drift
    /// into the ground, and a light that burrows is a light nobody can chase.
    /// </summary>
    internal class LightDrift : MonoBehaviour
    {
        public Vector3 Target;
        public float Speed;

        private void Start()
        {
            var body = GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;
        }

        private void Update()
        {
            if (Speed <= 0f) return;

            // HORIZONTAL pursuit with a ground-follow, never a straight 3D line. The homing
            // target can carry a nonsense height (BiomeCompass.Nearest returns y = 0, far below
            // sea level), and a 3D MoveTowards steered the light into the terrain within seconds
            // of rising — "the light disappears immediately" was it burrowing.
            Vector3 flat = Target; flat.y = transform.position.y;
            Vector3 next = Vector3.MoveTowards(transform.position, flat, Speed * Time.deltaTime);

            try
            {
                var zone = ZoneSystem.instance;
                if (zone != null) next.y = zone.GetGroundHeight(next) + 2.2f;
            }
            catch
            {
                // Keep the current height rather than guessing one.
            }

            transform.position = next;
        }
    }

    internal class StolenLights
    {
        private readonly IConfiguration _cfg;

        /// <summary>Tried in order, as in <see cref="SpiritChase"/>: a real spirit if this build has one.</summary>
        private static readonly string[] Candidates = { "Wisp", "Ghost", "Deer" };

        /// <summary>What a questline step matches on. Synthetic — no prefab is called this.</summary>
        public const string TakenEvent = "SpiritTaken";

        private class Light
        {
            public ZDOID Id;
            public Vector3 Where;
            public float FadesAt;
            public LightPickup Pickup;

            /// <summary>When the player was last close enough to have taken it. See Tick.</summary>
            public float NearAt = float.NegativeInfinity;
        }

        private readonly List<Light> _lights = new List<Light>();

        /// <summary>
        /// The scoreboard. Every light is either taken back or taken by the forest, and both halves
        /// are shown — a race you cannot see the other side of is not a race.
        /// </summary>
        public int Taken { get; private set; }
        public int Lost { get; private set; }

        public StolenLights(IConfiguration cfg) => _cfg = cfg;

        public void Reset()
        {
            foreach (var light in _lights.ToList()) Destroy(light);
            _lights.Clear();
            Taken = 0;
            Lost = 0;
        }

        /// <summary>Restores the scoreboard from a save.</summary>
        public void Restore(int taken, int lost)
        {
            Taken = Mathf.Max(0, taken);
            Lost = Mathf.Max(0, lost);
        }

        /// <summary>How many lights are still burning. The HUD counts them down.</summary>
        public int Burning => _lights.Count;

        /// <summary>Seconds left on the most urgent one, or zero.</summary>
        public float Soonest =>
            _lights.Count == 0 ? 0f : Mathf.Max(0f, _lights.Min(l => l.FadesAt) - Time.time);

        /// <summary>
        /// A deer has fallen. Releases its light at the carcass.
        ///
        /// Lifted clear of the ground and offset slightly, so it does not spawn inside the corpse
        /// or the thing that killed it.
        /// </summary>
        public void Release(Vector3 at) => Release(at, 0f, null);

        public void Release(Vector3 at, float fadeSecondsOverride) => Release(at, fadeSecondsOverride, null);

        /// <summary>
        /// As <see cref="Release(Vector3)"/>, with a fade override for lights that are FREED
        /// rather than contested — the Gatherer's hoard should wait to be collected, not start a
        /// second scramble over a corpse the player just fought for.
        /// </summary>
        /// <summary>
        /// <paramref name="driftTo"/>, when set, sends the light HOMING toward the collector —
        /// the Gatherer, the Herald's ground, or the forest itself. Null for lights that wait:
        /// the Gatherer's freed hoard, and any race light with nothing to home on.
        /// </summary>
        public void Release(Vector3 at, float fadeSecondsOverride, Vector3? driftTo)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return;

            GameObject prefab = null;
            string chosen = null;
            foreach (var name in Candidates)
            {
                prefab = scene.GetPrefab(name);
                if (prefab != null) { chosen = name; break; }
            }

            if (prefab == null)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] No spirit prefab — deer give up nothing.");
                return;
            }

            // Head height rather than waist height: at 1.2 up, a light could sit INSIDE a bush
            // and fire its old proximity pickup without ever being seen.
            var inst = UnityEngine.Object.Instantiate(prefab, at + Vector3.up * 2.2f, Quaternion.identity);
            if (inst == null) return;

            // Same size bump as the chase light: these appear at night, mid-fight, with
            // greydwarves arriving. Being findable at a glance is the whole mechanic.
            // A wisp is a small mote where a Ghost is person-sized; the same landmark job
            // needs a very different bump.
            inst.transform.localScale *= chosen == "Wisp" ? 3f : 1.6f;

            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null)
            {
                UnityEngine.Object.Destroy(inst);
                return;
            }

            zdo.Persistent = false;

            var ch = inst.GetComponent<Character>();
            if (ch != null)
            {
                // Tamed so it never fights: this is a thing to reach, and the greydwarves already
                // provide everything hostile the moment needs.
                try { ch.SetTamed(true); } catch { }
                ch.m_name = "A deer's light";

                // The fallback deer must not read as another deer to hunt.
                if (chosen == "Deer") { try { ch.SetLevel(3); } catch { } }
            }

            // The wisp is an ITEM, and items auto-pickup on approach — which is why the E-press
            // "still felt proximity based": the game grabbed the light before the prompt could
            // matter, through our component and past it. Auto-pickup off makes E the only way,
            // served by the game's own item interact; our component is only added where there is
            // no ItemDrop to do that job (Ghost, the fallback deer).
            LightPickup pickup = null;
            var itemDrop = inst.GetComponent<ItemDrop>();
            if (itemDrop != null)
            {
                try { itemDrop.m_autoPickup = false; } catch { }
            }
            else
            {
                pickup = inst.AddComponent<LightPickup>();
            }

            if (driftTo != null)
            {
                var drift = inst.AddComponent<LightDrift>();
                // Raised toward head height at the destination too, so the drift does not dive
                // into the terrain as it leaves.
                drift.Target = driftTo.Value + Vector3.up * 2.2f;
                drift.Speed = Mathf.Max(0.1f, _cfg.RunLightDriftSpeed);
            }

            _lights.Add(new Light
            {
                Id = zdo.m_uid,
                Where = at,
                FadesAt = Time.time + Mathf.Max(5f, fadeSecondsOverride > 0f ? fadeSecondsOverride : _cfg.RunLightFadeSeconds),
                Pickup = pickup,
            });
        }

        /// <summary>
        /// Call about once a second. Returns how many lights the player reached this tick; anything
        /// that faded is reported through <paramref name="lost"/> so the host can say so.
        /// </summary>
        public int Tick(Player player, out int lost)
        {
            lost = 0;
            if (player == null || _lights.Count == 0) return 0;

            int taken = 0;
            float reach = Mathf.Max(2f, _cfg.RunLightReachRadius);
            Vector3 here = player.transform.position;

            foreach (var light in _lights.ToList())
            {
                Vector3? live = Position(light);

                // Remember every moment the player stands close to a LIVE light. An item-mode
                // light's take destroys the object, and the only evidence left is that the player
                // was just there — without this, an E-press followed by a sprint (dev speed made
                // it easy) left the taken light as a zombie that later "faded", telling the
                // player the forest took a light that was in their pocket.
                if (live != null)
                {
                    // A drifting light's "where" is wherever it has got to — the orphan fallback
                    // and the fade message must chase the same thing the player is chasing.
                    light.Where = live.Value;

                    if (Vector3.Distance(here, live.Value) <= 8f)
                        light.NearAt = Time.time;
                }

                // Taking is an E-PRESS on the light itself, through the game's own interact
                // prompt. Proximity remains only for a light whose object is GONE (zone unloaded):
                // there is nothing left to press E on, and a light that became untakeable through
                // no fault of the player's should not count against them.
                bool claimed = !ReferenceEquals(light.Pickup, null) && light.Pickup != null && light.Pickup.Claimed;
                // For an item-mode light, the take IS the object vanishing. Two proofs accepted:
                // still standing near where it stood, OR having been near it within the last few
                // seconds — the E-then-sprint case, where the poll arrives after the player left.
                bool orphanReached = live == null &&
                    (Vector3.Distance(here, light.Where) <= Mathf.Max(reach, 6f) ||
                     Time.time - light.NearAt <= 3f);

                if (claimed || orphanReached)
                {
                    taken++;
                    Taken++;
                    Destroy(light);
                    _lights.Remove(light);
                    continue;
                }

                if (Time.time >= light.FadesAt)
                {
                    lost++;
                    Lost++;
                    Destroy(light);
                    _lights.Remove(light);
                }
            }

            return taken;
        }

        private static Vector3? Position(Light light)
        {
            try
            {
                var zdo = ZDOMan.instance?.GetZDO(light.Id);
                return zdo == null ? (Vector3?)null : zdo.GetPosition();
            }
            catch
            {
                return null;
            }
        }

        private static void Destroy(Light light)
        {
            try
            {
                var zdo = ZDOMan.instance?.GetZDO(light.Id);
                if (zdo != null) ZDOMan.instance.DestroyZDO(zdo);
            }
            catch
            {
                // An orphaned light is untidy, not broken — and it is non-persistent either way.
            }
        }
    }
}
