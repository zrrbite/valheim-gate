using System;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Act II's character: the Black Forest resents being cut.
    ///
    /// Eikthyr's act had a theme — he is the deer god, so the meadows filled with starred deer, a
    /// named Herald, and lightning over every carcass. Act II had none: it was copper, a smelter,
    /// bronze, a portal. A shopping list.
    ///
    /// The Elder is a tree. His children are greydwarves. So the forest NOTICES the axe: fell
    /// enough of it and something comes to see who is doing it. Nothing here blocks progress or
    /// touches a questline — it is atmosphere, and atmosphere is what turns a tier into a place.
    ///
    /// Modelled on <see cref="DeerHerd"/>'s contested kills, including the lesson that came with
    /// them: a summon that fires EVERY time stops being tension and becomes a tax.
    /// </summary>
    internal class ForestWatch
    {
        private readonly IConfiguration _cfg;
        private readonly System.Random _rng;

        /// <summary>The Elder's children, as the game names them.</summary>
        private const string WatcherPrefab = "Greydwarf";
        private const string EldritchPrefab = "Greydwarf_Elite";

        /// <summary>Chop count at the last check; NaN until the first reading seeds it.</summary>
        private float _lastChops = float.NaN;

        public ForestWatch(IConfiguration cfg, System.Random rng)
        {
            _cfg = cfg;
            _rng = rng ?? new System.Random();
        }

        /// <summary>Forgets the chop baseline. Called when a run starts or ends.</summary>
        public void Reset() => _lastChops = float.NaN;

        /// <summary>
        /// Call about once a second during Act II with the player's current lifetime chop count.
        ///
        /// Watches the count RISE rather than hooking tree destruction, because the mod's only
        /// injected hook is Character.OnDeath and a tree is not a character. The first reading
        /// seeds the baseline and does nothing, so loading in with ten thousand chops behind you
        /// does not summon the forest on the spot.
        /// </summary>
        public void OnChopped(Player player, float chops)
        {
            if (player == null || float.IsNaN(chops)) return;

            if (float.IsNaN(_lastChops))
            {
                _lastChops = chops;
                return;
            }

            float threshold = Mathf.Max(1f, _cfg.RunForestNoticeChops);
            if (chops - _lastChops < threshold) return;

            _lastChops = chops;

            if (_cfg.RunForestNoticeChance <= 0f) return;
            if (_rng.NextDouble() > _cfg.RunForestNoticeChance) return;

            Summon(player);
        }

        /// <summary>
        /// Sends the forest to have a look.
        ///
        /// Spawned BEHIND and around the player rather than in front: the point is being found,
        /// not being ambushed head-on, and a greydwarf appearing in your swing is a cheap shock
        /// rather than an unsettling one.
        /// </summary>
        private void Summon(Player player)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return;

            int count = Mathf.Max(1, _cfg.RunForestNoticeCount);

            // One in four brings something bigger. Rare enough that felling a forest stays
            // survivable in bronze, often enough that the axe never feels free.
            bool eldritch = _rng.NextDouble() < 0.25;

            for (int i = 0; i < count; i++)
            {
                string wanted = eldritch && i == 0 ? EldritchPrefab : WatcherPrefab;

                var prefab = scene.GetPrefab(wanted);
                if (prefab == null)
                {
                    Debug.LogWarning($"[ICanShowYouTheWorld] No '{wanted}' prefab — the forest stays quiet.");
                    return;
                }

                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float radius = 12f + (float)(_rng.NextDouble() * 8.0);
                Vector3 pos = player.transform.position
                            + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (inst == null) continue;

                // Non-persistent for the same reason every summon in this mode is: what the mode
                // conjures must not outlive the session in someone's world. Power is loaned, and
                // so is menace.
                var view = inst.GetComponent<ZNetView>();
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                if (zdo != null) zdo.Persistent = false;

                // Starred at full health only — SetLevel recomputes max health from the CURRENT
                // value, so starring anything already hurt bakes the damage in. Same trap the deer
                // and the contested-kill greylings both guard against.
                int level = Mathf.Clamp(_cfg.RunForestNoticeLevel, 1, 3);
                if (level > 1)
                {
                    try
                    {
                        var ch = inst.GetComponent<Character>();
                        if (ch != null) ch.SetLevel(level);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ICanShowYouTheWorld] Could not star a watcher: {e.Message}");
                    }
                }
            }
        }
    }
}
