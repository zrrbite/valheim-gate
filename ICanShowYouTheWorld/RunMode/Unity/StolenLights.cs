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
        public void Release(Vector3 at)
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

            var inst = UnityEngine.Object.Instantiate(prefab, at + Vector3.up * 1.2f, Quaternion.identity);
            if (inst == null) return;

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

            _lights.Add(new Light
            {
                Id = zdo.m_uid,
                Where = at,
                FadesAt = Time.time + Mathf.Max(5f, _cfg.RunLightFadeSeconds),
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
                // The live position where the object still exists, the remembered place otherwise:
                // a light whose zone unloaded should still be reachable rather than silently gone.
                Vector3 at = Position(light) ?? light.Where;

                if (Vector3.Distance(here, at) <= reach)
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
