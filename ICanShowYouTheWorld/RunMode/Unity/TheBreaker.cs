using System;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The Breaker — a troll that wandered out of the forest, and Act I's mini-boss.
    ///
    /// It is the one thing in this story that BREAKS light instead of carrying it. Every other
    /// creature converges on a light out of hunger and carries it home; the troll is what a splinter
    /// becomes when the harvest never reaches it — too big for the Elder's table, never fed, and
    /// long past collecting. That fiction was written for Act II and the fight sat there, where the
    /// player meets it after a whole act of trolls being ordinary. Moved to the Meadows on the
    /// owner's call, it lands while a troll is still the largest thing they have ever seen.
    ///
    /// **It is spawned, because the Meadows have no trolls.** Same reasoning as the Herald and the
    /// Gatherer: beside the player, so it can never be placed somewhere unloaded — the bug that made
    /// the Herald unfindable for two versions. Unstarred, deliberately: a troll against flint is
    /// already the hardest thing in the act, and stars would make it a wall rather than a fight.
    ///
    /// **The allies of convenience are the whole trick, and the game's own rule provides them.**
    /// `BaseAI.IsEnemy` was read out of this build's IL: a ForestMonster is hostile to every faction
    /// except AnimalsVeg, Boss and its own. So moving the troll off ForestMonsters is all it takes
    /// for the greydwarves to fight it — no reflection, no forced targets, nothing re-applied every
    /// tick against an AI that would re-pick its own target a second later.
    ///
    /// Demon rather than Undead because Demon is hostile to everything that matters here: the
    /// player, the forest, the player's raised skeletons, and the wildlife. Nothing in the world is
    /// on its side, which is exactly what the bible says it is.
    ///
    /// And the alliance is explicitly one of CONVENIENCE (owner: "allies by convenience. I'd have to
    /// dodge not to get aggro etc"). The greydwarves are not made friendly and are not tamed: they
    /// still want the player dead. They simply want the troll dead more, and standing between two
    /// of them is the player's problem.
    /// </summary>
    internal sealed class TheBreaker
    {
        /// <summary>Tried in order; the first that resolves is used.</summary>
        private static readonly string[] Candidates = { "Troll" };

        public const string Name = "The Breaker";

        /// <summary>How far out it appears. Further than the Gatherer's 28m, so it WALKS IN.</summary>
        private const float ArrivalRange = 34f;

        private readonly IConfiguration _cfg;
        private readonly System.Random _rng;

        private ZDOID _it = ZDOID.None;

        public TheBreaker(IConfiguration cfg, System.Random rng)
        {
            _cfg = cfg;
            _rng = rng ?? new System.Random();
        }

        public void Reset() => _it = ZDOID.None;

        public bool Alive
        {
            get
            {
                if (_it == ZDOID.None) return false;

                try
                {
                    var man = ZDOMan.instance;
                    return man != null && man.GetZDO(_it) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Brings it through. Returns false when it is already here, or when nothing could be spawned.
        /// </summary>
        public bool TryArrive(Player player)
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
                Debug.LogError("[ICanShowYouTheWorld] No troll prefab — the Breaker cannot come.");
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

            // Never written into the world file. A mini-boss that survived the run by living in the
            // save would be a permanent troll in somebody's meadow.
            zdo.Persistent = false;
            _it = zdo.m_uid;

            var ch = inst.GetComponent<Character>();
            if (ch != null)
            {
                ch.m_name = Name;

                // THE line of this class. See the class note: off ForestMonsters is what makes the
                // forest fight it, and Demon leaves nothing on its side.
                try { ch.m_faction = Character.Faction.Demon; }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ICanShowYouTheWorld] The Breaker kept its faction: " + ex.Message +
                                     " — it will fight alone, and so will you.");
                }
            }

            // Pale and cold-lit, like the Herald: this is not one of the forest's any more.
            try { CreatureDressing.Apply(inst, CreatureDressing.Herald()); }
            catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] The Breaker's look: " + ex.Message); }

            // SetHuntPlayer, not a target: it comes looking, and keeps looking.
            var ai = inst.GetComponent<MonsterAI>();
            if (ai != null)
            {
                try { ai.SetHuntPlayer(true); } catch { /* it will find him regardless */ }
            }

            return true;
        }

        /// <summary>
        /// It has moved on. Called when the step's clock runs out: a troll is weather, not a
        /// garrison, and a missed deadline should take the troll away rather than leave it standing
        /// in a meadow with nothing left to earn.
        /// </summary>
        public void Leave()
        {
            if (_it == ZDOID.None) return;

            try
            {
                var man = ZDOMan.instance;
                var zdo = man?.GetZDO(_it);

                // DestroyZDO refuses on a ZDO this client does not own, so the object is asked to
                // go first and the ZDO is only reached for as a fallback — the same ordering the
                // companions use.
                if (zdo != null)
                {
                    var view = ZNetScene.instance?.FindInstance(_it);
                    if (view != null) ZNetScene.instance.Destroy(view.gameObject);
                    else man.DestroyZDO(zdo);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The Breaker would not leave: " + ex.Message);
            }

            _it = ZDOID.None;
        }

        /// <summary>True when the character that died was this one. Matched by ZDOID.</summary>
        public bool OnCharacterDied(Character c)
        {
            if (c == null || _it == ZDOID.None) return false;

            try
            {
                var view = c.GetComponent<ZNetView>();
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                if (zdo == null || zdo.m_uid != _it) return false;
            }
            catch
            {
                return false;
            }

            _it = ZDOID.None;
            return true;
        }
    }
}
