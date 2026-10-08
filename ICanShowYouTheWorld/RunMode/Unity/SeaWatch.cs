using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>What SeaWatch needs to know each second, from the run.</summary>
    internal struct SeaSettings
    {
        public bool Enabled;
        public float Heat;
        public float FullHeat;
        public float PeakPerMinute;
        public int ActIndex;
        public int WardTier;
    }

    /// <summary>
    /// The sea answers the heat - the game side of <see cref="SeaDanger"/> (2026-10-08; see the spec,
    /// docs/superpowers/specs/2026-10-08-sea-danger-design.md). Once a second: is the player at sea, is a roll due,
    /// what comes; then it spawns them off the bow or from the coast, hunting the player and never saved; and it
    /// keeps count of what it sent, for the limit and for the coins.
    /// </summary>
    internal sealed class SeaWatch
    {
        private const float RingNear = 150f;
        private const float RingFar = 300f;
        private const int RingDirections = 16;
        private const float EncounterReach = 300f;

        private sealed class Group
        {
            public SeaCreature Creature;
            public int Level;
            public readonly List<Character> Members = new List<Character>();
        }

        private readonly Action<string> _say;
        private readonly Action _firstEncounter;
        private readonly Func<System.Random> _runRng;
        private bool _pulseFailLogged;
        private readonly List<Group> _groups = new List<Group>();
        private SeaVoyage _voyage = new SeaVoyage();
        private bool _noSerpentLogged;

        // The Ward's pulse (Task 5): when it next strikes, and the effect it shows.
        private float _nextPulse;
        private readonly List<Character> _inRange = new List<Character>();
        private GameObject _spark;
        private bool _sparkResolved;
        /// <summary>
        /// The effect where the ward strikes, first that the game has (the self-check names which). Read out of the
        /// game's assets on 2026-10-08, after the first guesses ("vfx_lightning", "fx_lightning") resolved to nothing
        /// on the Mac: prefab names are case-sensitive, and the sky's own bolt is "fx_Lightning", the last resort.
        /// </summary>
        internal static readonly string[] SparkPrefabs =
            { "fx_chainlightning_hit", "fx_lightningstaffprojectile_hit", "fx_lightningweapon_hit", "fx_Lightning" };

        /// <param name="rng">The run's generator, asked each time: StartRun and a resume reseed the run's, and a
        /// watcher built once kept the first one (final review, 2026-10-08).</param>
        public SeaWatch(Action<string> say, Action firstEncounter, Func<System.Random> rng)
        {
            _say = say;
            _firstEncounter = firstEncounter;
            var fallback = new System.Random();
            _runRng = () => rng?.Invoke() ?? fallback;
        }

        /// <summary>A run starts or ends: forget the voyage and what was sent (they vanish with their area).</summary>
        public void Reset()
        {
            _groups.Clear();
            _voyage = new SeaVoyage();
            _nextPulse = 0f;
        }

        public void HornBlown() => _voyage.HornBlown(Time.time);

        /// <summary>Once a second while a run is live.</summary>
        public void Tick(Player player, SeaSettings s)
        {
            if (player == null) return;
            Prune(player);
            // The Ward works whether sea danger is on or not. Its own try, so a target that throws can stop neither
            // the sea's rolls nor the next pulse (final review, 2026-10-08).
            try { Pulse(player, s); }
            catch (Exception e)
            {
                if (!_pulseFailLogged) Debug.LogWarning("[ICanShowYouTheWorld] Ward pulse failed: " + e.Message);
                _pulseFailLogged = true;
            }

            if (!s.Enabled || s.ActIndex < SeaDanger.FirstActIndex)
            {
                _voyage.Tick(Time.time, false);   // never a voyage here; a horn blown now is forgotten in time
                return;
            }
            var ship = Ship.GetLocalShip();
            var turn = _voyage.Tick(Time.time, AtSea(ship, true));
            float h = SeaDanger.Dial(s.Heat, s.FullHeat);
            if (SeaDanger.Comes(turn, h, s.PeakPerMinute, _runRng().NextDouble()))
                Encounter(player, ship, s, h, turn == SeaTurn.Certain ? "the horn" : "rolled");
        }

        /// <summary>Dev: an encounter now, if the player is aboard a ship over open water. False otherwise.</summary>
        public bool Force(Player player, SeaSettings s)
        {
            var ship = Ship.GetLocalShip();
            if (player == null || !AtSea(ship, false)) return false;
            // Said on screen too: on the Mac, a press that brought nothing looked like a dead key (final review).
            if (!Encounter(player, ship, s, SeaDanger.Dial(s.Heat, s.FullHeat), "dev"))
                _say?.Invoke("DEV: no room for the sea here - sail out to deeper water.");
            return true;
        }

        /// <summary>Whether <paramref name="c"/> is one the sea sent this run, and as what.</summary>
        public bool IsSent(Character c, out SeaCreature creature, out int level)
        {
            foreach (var g in _groups)
                foreach (var m in g.Members)
                    if (ReferenceEquals(m, c))
                    {
                        creature = g.Creature;
                        level = g.Level;
                        return true;
                    }
            creature = SeaCreature.Serpent;
            level = 0;
            return false;
        }

        /// <summary>
        /// The Ward (2026-10-08): every few seconds while the player is aboard, lightning strikes every attacker within
        /// the ward's radius of the ship - through the ordinary damage path, so kills count. Who counts as an attacker
        /// is <see cref="ShipFittings.WardStrikes"/>: an alerted, hostile monster, never a grazing deer.
        /// </summary>
        private void Pulse(Player player, SeaSettings s)
        {
            if (s.WardTier < 1 || Time.time < _nextPulse) return;
            var ship = Ship.GetLocalShip();
            if (ship == null) return;
            _nextPulse = Time.time + ShipFittings.WardPulseSeconds;

            float damage = ShipFittings.WardDamage(s.WardTier);
            _inRange.Clear();
            Character.GetCharactersInRange(ship.transform.position, ShipFittings.WardRadius(s.WardTier), _inRange);
            foreach (var c in _inRange)
            {
                // One target at a time: one that throws is skipped, not the rest (final review, 2026-10-08).
                try { StrikeOne(c, player, damage); }
                catch (Exception e)
                {
                    if (!_pulseFailLogged) Debug.LogWarning("[ICanShowYouTheWorld] Ward strike failed: " + e.Message);
                    _pulseFailLogged = true;
                }
            }
        }

        private void StrikeOne(Character c, Player player, float damage)
        {
            if (c == null) return;
            var ai = c.GetBaseAI();
            if (!ShipFittings.WardStrikes(new WardTarget
                {
                    Player = c.IsPlayer(), Tamed = c.IsTamed(), Dead = c.IsDead(), Enemy = BaseAI.IsEnemy(player, c),
                    LightningImmune = c.GetDamageModifiers(null).m_lightning == HitData.DamageModifier.Immune,
                    Monster = ai is MonsterAI, Alerted = ai != null && ai.IsAlerted(),
                })) return;

            var hit = new HitData();
            hit.m_damage.m_lightning = damage;
            hit.m_point = c.GetCenterPoint();
            hit.SetAttacker(player);
            c.Damage(hit);
            Spark(hit.m_point);
        }

        /// <summary>A small spark where the ward strikes, if the game has the effect (asset names are guesses).</summary>
        private void Spark(Vector3 at)
        {
            if (!_sparkResolved)
            {
                _sparkResolved = true;
                var scene = ZNetScene.instance;
                if (scene != null)
                    foreach (var name in SparkPrefabs)
                    {
                        _spark = scene.GetPrefab(name);
                        if (_spark != null) break;
                    }
                if (_spark == null) Debug.Log("[ICanShowYouTheWorld] Ward: no lightning effect resolved; it strikes unseen.");
            }
            if (_spark == null) return;
            try { UnityEngine.Object.Instantiate(_spark, at, Quaternion.identity); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Ward spark failed: " + e.Message); }
        }

        /// <summary>Open water under <paramref name="p"/> by the world generator's ground (also the Gatherer's shore).</summary>
        internal static bool OverOpenWater(Vector3 p)
        {
            var wg = WorldGenerator.instance;
            var zs = ZoneSystem.instance;
            return wg != null && zs != null && LandSpot.OpenWater(wg.GetHeight(p.x, p.z), zs.m_waterLevel);
        }

        /// <summary>Aboard a ship over open water - and, for the rolls, moving (sail or oars).</summary>
        private static bool AtSea(Ship ship, bool moving) =>
            ship != null && OverOpenWater(ship.transform.position) && (!moving || Mathf.Abs(ship.GetSpeed()) >= 1f);

        private void Prune(Player player)
        {
            Vector3 at = player.transform.position;
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                // Unity's null is wanted here: a destroyed creature is gone.
                _groups[i].Members.RemoveAll(c => c == null || c.IsDead() ||
                                                  Vector3.Distance(c.transform.position, at) > EncounterReach);
                if (_groups[i].Members.Count > 0) continue;
                _groups.RemoveAt(i);
                _voyage.Ended();
            }
        }

        /// <summary>Sends one encounter. False when nothing came: no creature, or no room for it.</summary>
        private bool Encounter(Player player, Ship ship, SeaSettings s, float h, string why)
        {
            Vector3 at = ship.transform.position;
            var coasts = CoastsNear(at);
            var wg = WorldGenerator.instance;
            bool ashSea = wg != null && wg.GetBiome(at) == Heightmap.Biome.AshLands;
            var enc = SeaDanger.Choose(coasts.Keys, ashSea, s.ActIndex, h, _runRng().NextDouble);

            var prefab = Prefab(enc.Prefab);
            if (prefab == null)
            {
                // A creature the game does not have: a Serpent instead (the self-check names it).
                enc = SeaDanger.Make(SeaCreature.Serpent, null, h);
                prefab = Prefab(enc.Prefab);
            }
            if (prefab == null)
            {
                if (!_noSerpentLogged) Debug.LogWarning("[ICanShowYouTheWorld] Sea: no Serpent prefab - the sea stays calm.");
                _noSerpentLogged = true;
                return false;
            }

            var group = new Group { Creature = enc.Creature, Level = enc.Level };
            Vector3 dir;
            if (enc.Flies && enc.From.HasValue && coasts.TryGetValue(enc.From.Value, out dir)) SpawnFlyers(prefab, enc, at, dir, group);
            else if (!enc.Flies) SpawnSwimmers(prefab, enc, ship, group);

            if (group.Members.Count == 0)
            {
                Debug.Log($"[ICanShowYouTheWorld] Sea: no room for {enc.Prefab} ({why}) - no open water ahead.");
                return false;
            }

            _groups.Add(group);
            _voyage.Arrived(Time.time);
            _firstEncounter?.Invoke();
            _say?.Invoke(enc.Message);
            Debug.Log($"[ICanShowYouTheWorld] Sea: {group.Members.Count} x {enc.Prefab}, level {enc.Level} ({why}); " +
                      $"heat {s.Heat:0.#}, dial {h:0.00}, gap {SeaDanger.GapMinutes(h, s.PeakPerMinute):0.#} min.");
            return true;
        }

        private static GameObject Prefab(string name) => ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;

        /// <summary>Each reached flyer coast within 300 m, with the direction it lies in from the ship.</summary>
        private static Dictionary<SeaCoast, Vector3> CoastsNear(Vector3 at)
        {
            var found = new Dictionary<SeaCoast, Vector3>();
            var wg = WorldGenerator.instance;
            var zs = ZoneSystem.instance;
            if (wg == null || zs == null) return found;

            foreach (float r in new[] { RingNear, RingFar })
                for (int i = 0; i < RingDirections; i++)
                {
                    float a = i * Mathf.PI * 2f / RingDirections;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var p = at + dir * r;
                    if (wg.GetHeight(p.x, p.z) <= zs.m_waterLevel + 1f) continue;
                    SeaCoast coast;
                    if (!CoastOf(wg.GetBiome(p), out coast) || found.ContainsKey(coast)) continue;
                    found[coast] = dir;
                }
            return found;
        }

        private static bool CoastOf(Heightmap.Biome biome, out SeaCoast coast)
        {
            switch (biome)
            {
                case Heightmap.Biome.Mountain: coast = SeaCoast.Mountain; return true;
                case Heightmap.Biome.Plains: coast = SeaCoast.Plains; return true;
                case Heightmap.Biome.Mistlands: coast = SeaCoast.Mistlands; return true;
                case Heightmap.Biome.AshLands: coast = SeaCoast.Ashlands; return true;
                default: coast = SeaCoast.Mountain; return false;
            }
        }

        /// <summary>Sea creatures surface 50-70 m ahead, within 45 degrees of the heading, over three metres of water. Up to 8 tries.</summary>
        private void SpawnSwimmers(GameObject prefab, SeaEncounter enc, Ship ship, Group group)
        {
            var zs = ZoneSystem.instance;
            var wg = WorldGenerator.instance;
            if (zs == null || wg == null) return;
            Vector3 fwd = ship.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            for (int tries = 0; tries < 8; tries++)
            {
                float angle = (float)(_runRng().NextDouble() * 90.0 - 45.0);
                float dist = 50f + (float)_runRng().NextDouble() * 20f;
                Vector3 spot = ship.transform.position + Quaternion.Euler(0f, angle, 0f) * fwd * dist;
                if (!SeaDanger.DeepEnough(wg.GetHeight(spot.x, spot.z), zs.m_waterLevel)) continue;
                spot.y = zs.m_waterLevel;
                for (int i = 0; i < enc.Count; i++) Make(prefab, spot + Side(i), enc.Level, ship.transform.position, group);
                return;
            }
        }

        /// <summary>Flyers come from the coast's side, 40-60 m out, 15 m above the water or the cliff below.</summary>
        private void SpawnFlyers(GameObject prefab, SeaEncounter enc, Vector3 ship, Vector3 dir, Group group)
        {
            var zs = ZoneSystem.instance;
            if (zs == null) return;
            float dist = 40f + (float)_runRng().NextDouble() * 20f;
            Vector3 spot = ship + dir * dist;
            var wg = WorldGenerator.instance;
            spot.y = SeaDanger.FlyerHeight(wg != null ? wg.GetHeight(spot.x, spot.z) : zs.m_waterLevel, zs.m_waterLevel);
            for (int i = 0; i < enc.Count; i++) Make(prefab, spot + Side(i), enc.Level, ship, group);
        }

        private static Vector3 Side(int i) => i == 0 ? Vector3.zero : new Vector3((i % 2 * 2 - 1) * 3f * ((i + 1) / 2), 0f, 0f);

        private static void Make(GameObject prefab, Vector3 at, int level, Vector3 lookAt, Group group)
        {
            Vector3 look = lookAt - at;
            look.y = 0f;
            var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
            var inst = UnityEngine.Object.Instantiate(prefab, at, rot);
            var ch = inst != null ? inst.GetComponent<Character>() : null;
            if (ch == null) return;

            // Set at full health: SetLevel recomputes max health from the current value (DeerHerd).
            if (level > 1) ch.SetLevel(level);

            // Never saved, like the Herald and the contest pack: nothing piles up in the player's world.
            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo != null) zdo.Persistent = false;

            var monster = inst.GetComponent<MonsterAI>();
            if (monster != null) monster.SetHuntPlayer(true);
            var ai = inst.GetComponent<BaseAI>();
            if (ai != null) ai.Alert();

            group.Members.Add(ch);
        }
    }
}
