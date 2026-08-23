using System;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Act III's character: the swamp does not let go of its dead.
    ///
    /// Bonemass is a heap of corpses that stood up. So in his act, what you kill sometimes gets
    /// back up too — a draugr you put down rises again as bone, once, right where it fell.
    ///
    /// It is the same design as Eikthyr's lightning and the Elder's watchers: a small thing that
    /// happens where you are already looking, tied to the god whose act it is. It blocks nothing
    /// and completes nothing.
    ///
    /// Two deliberate limits. It fires on a CHANCE, because an ambush every time is a tax rather
    /// than tension — the lesson the contested deer kills paid for. And a risen skeleton cannot
    /// itself rise, or a single draugr would become an unkillable queue.
    /// </summary>
    internal class FenWatch
    {
        private readonly IConfiguration _cfg;
        private readonly System.Random _rng;

        private const string RisenPrefab = "Skeleton";

        public FenWatch(IConfiguration cfg, System.Random rng)
        {
            _cfg = cfg;
            _rng = rng ?? new System.Random();
        }

        public void Reset() { }

        /// <summary>
        /// Offers a fallen creature back to the swamp. Call from the death hook during Act III.
        ///
        /// Returns true when something rose, purely so the host can say so.
        /// </summary>
        public bool OnCharacterDied(Character c)
        {
            if (c == null || _cfg.RunFenRisenChance <= 0f) return false;

            // Players and tamed animals stay dead. Raising a companion the player just watched die
            // would read as cruelty rather than atmosphere.
            try
            {
                if (c.IsPlayer() || c.IsTamed()) return false;
            }
            catch
            {
                return false;
            }

            // Bone does not rise from bone: without this, one draugr becomes an endless relay.
            string name = PrefabNameOf(c);
            if (name.IndexOf("Skeleton", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (_rng.NextDouble() > _cfg.RunFenRisenChance) return false;

            return Raise(c.transform.position);
        }

        private bool Raise(Vector3 where)
        {
            var scene = ZNetScene.instance;
            var prefab = scene == null ? null : scene.GetPrefab(RisenPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] No '{RisenPrefab}' prefab — the dead stay down.");
                return false;
            }

            // Slightly off the corpse so it does not spawn inside whatever killed it, and lifted
            // clear of the ground for the reason every spawn in this mode is: arriving inside the
            // terrain is how a spawn turns into a corpse.
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            Vector3 pos = where
                        + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f
                        + Vector3.up * 0.5f;

            var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (inst == null) return false;

            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo != null) zdo.Persistent = false;

            // NOT tamed — this is the opposite of Bonecaller. The same prefab, raised by the swamp
            // rather than by you, and it is not on your side.
            int level = Mathf.Clamp(_cfg.RunFenRisenLevel, 1, 3);
            if (level > 1)
            {
                try
                {
                    var ch = inst.GetComponent<Character>();
                    if (ch != null) ch.SetLevel(level);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ICanShowYouTheWorld] Could not star the risen: {e.Message}");
                }
            }

            return true;
        }

        private static string PrefabNameOf(Character c)
        {
            var go = c.gameObject;
            return go == null ? string.Empty : go.name.Replace("(Clone)", string.Empty);
        }
    }
}
