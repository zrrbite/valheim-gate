using System;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Act I's answer to "why are there always greydwarves".
    ///
    /// Something has been farming Eikthyr's herd — every deer that falls gives up its light, and
    /// the forest sends its children to carry them off. The Gatherer is what they carry them TO: a
    /// two-star brute, fat on stolen light, that has been following the hunt the whole act.
    ///
    /// It does not hide and it does not need a bearing. When the Herald falls, the biggest thing
    /// in the meadows finally decides you are worth its attention and comes to you. That is the
    /// opposite of the Herald on purpose: one act, two named creatures, one you must find and one
    /// that finds you.
    ///
    /// Killing it releases what it was holding, and the lights show you Eikthyr's altar — which is
    /// the discovery step that follows, retold as a consequence instead of a checklist item.
    /// </summary>
    internal class TheGatherer
    {
        private readonly IConfiguration _cfg;
        private readonly System.Random _rng;

        /// <summary>The heaviest thing the Black Forest lends the meadows.</summary>
        private static readonly string[] Candidates = { "Greydwarf_Elite", "Greydwarf" };

        /// <summary>Matched by identity, so an ordinary brute cannot finish its step.</summary>
        public const string KillName = "__the_gatherer";

        public const string Name = "The Gatherer";

        /// <summary>Close enough to arrive as an event; far enough not to land in the player's swing.</summary>
        private const float ArrivalRange = 28f;

        private ZDOID _it = ZDOID.None;

        public TheGatherer(IConfiguration cfg, System.Random rng)
        {
            _cfg = cfg;
            _rng = rng ?? new System.Random();
        }

        public void Reset() => _it = ZDOID.None;

        /// <summary>Where it stands, or null when it is not in the world. The lights home on this.</summary>
        public Vector3? Position()
        {
            if (_it == ZDOID.None) return null;

            try
            {
                var zdo = ZDOMan.instance?.GetZDO(_it);
                return zdo == null ? (Vector3?)null : zdo.GetPosition();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Whether it is currently in the world.</summary>
        public bool Alive
        {
            get
            {
                if (_it == ZDOID.None) return false;

                try { return ZDOMan.instance?.GetZDO(_it) != null; }
                catch { return false; }
            }
        }

        /// <summary>
        /// Sends it in, if it is not already out. Returns true on the tick it arrives.
        ///
        /// Respawned whenever it is not alive, exactly as the Herald is: a creature whose zone
        /// unloaded should come back rather than leaving the step unfinishable. The difference is
        /// that this one spawns beside the PLAYER, so it can never end up somewhere unloaded — the
        /// bug that made the Herald unfindable for two versions.
        /// </summary>
        public bool TryArrive(Player player, int stolen)
        {
            if (player == null || Alive) return false;

            var scene = ZNetScene.instance;
            if (scene == null) return false;

            GameObject prefab = null;
            foreach (var name in Candidates)
            {
                prefab = scene.GetPrefab(name);
                if (prefab != null) break;
            }

            if (prefab == null)
            {
                Debug.LogError("[ICanShowYouTheWorld] No greydwarf prefab — the Gatherer cannot come.");
                return false;
            }

            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            Vector3 pos = player.transform.position
                        + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ArrivalRange;

            var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (inst == null) return false;

            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null)
            {
                UnityEngine.Object.Destroy(inst);
                return false;
            }

            zdo.Persistent = false;
            _it = zdo.m_uid;

            var ch = inst.GetComponent<Character>();
            if (ch != null)
            {
                // Set at full health: SetLevel recomputes max health from the CURRENT value, so
                // starring anything already hurt bakes the damage in permanently.
                // Fat on stolen light: every one the forest took while you were slow is health it
                // arrives with. The story and the difficulty are the same number, which is the
                // best kind of consequence — nothing has to be explained.
                try
                {
                    ch.SetLevel(Mathf.Clamp(_cfg.RunGathererLevel, 1, 3));

                    if (stolen > 0)
                    {
                        float bonus = 1f + stolen * Mathf.Max(0f, _cfg.RunGathererGrowthPerLight);
                        ch.SetMaxHealth(ch.GetMaxHealth() * bonus);
                    }
                }
                catch { }
                ch.m_name = Name;
            }

            // And it LOOKS like what it ate. Same number as the health bonus and the arrival
            // line, said a third way — the thing walking at you glows brighter the more of your
            // race it won. A star and a name were doing all the work before this.
            CreatureDressing.Apply(inst, CreatureDressing.Gatherer(stolen));

            // SetHuntPlayer, not SetTarget — the latter is not on MonsterAI. Hunting rather than
            // targeting is the right verb anyway: it comes looking, and keeps looking.
            var ai = inst.GetComponent<MonsterAI>();
            if (ai != null)
            {
                try { ai.SetHuntPlayer(true); } catch { /* it will find him regardless */ }
            }

            return true;
        }

        /// <summary>
        /// Its synthetic kill name if this was it, null otherwise. Matched by ZDOID, so a brute
        /// that merely looks the part cannot finish the step.
        /// </summary>
        public string OnCharacterDied(Character c)
        {
            if (c == null || _it == ZDOID.None) return null;

            try
            {
                var view = c.GetComponent<ZNetView>();
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                if (zdo == null || zdo.m_uid != _it) return null;
            }
            catch
            {
                return null;
            }

            _it = ZDOID.None;
            return KillName;
        }
    }
}
