using System;
using System.Collections.Generic;
using System.Linq;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Eikthyr's Herd — what happens to deer while Act I is running.
    ///
    /// Eikthyr is the stag god, so his act's deer are his. The brief was "aggressive deer, larger
    /// deer, deer with abilities", and the hard constraint discovered first is that **deer cannot be
    /// made to attack**: they run AnimalAI, which has no attack at all, and giving them one is Unity
    /// asset work this build cannot do. Everything here works around that rather than against it —
    /// deer become harder to CATCH, and killing one becomes an event.
    ///
    /// Three behaviours, all confined to Act I:
    ///
    ///   1. Starred deer. Character.SetLevel is how the game itself makes a starred spawn: visibly
    ///      larger, several times the health, and faster. One arrow becomes a chase.
    ///   2. The Herald. A named two-star deer spawned for its own questline step, tracked by ZDOID
    ///      so that ITS death is distinguishable from any other deer's.
    ///   3. Contested kills. A deer dying may draw greylings to the carcass, and may crack with
    ///      lightning. The danger comes from what the noise attracts, not from the deer.
    ///
    /// Kept out of RunService because it is a self-contained subject with its own state, and that
    /// file is already the largest in the mod.
    /// </summary>
    public class DeerHerd
    {
        /// <summary>The vanilla deer prefab. Checked at run start by the name validator like any other.</summary>
        public const string DeerPrefab = "Deer";

        /// <summary>What a deer death may summon. Meadows-appropriate: the forest, not the Black Forest.</summary>
        public const string ContestPrefab = "Greyling";

        /// <summary>
        /// Reported to the challenge engine when the Herald dies, and matched by its questline step.
        ///
        /// SYNTHETIC — deliberately not a real prefab name. The Herald is an ordinary Deer wearing a
        /// name, so reporting "Deer" would let any deer complete its step. The name validator is
        /// told to skip this one (see RunService.SyntheticCreatureNames), since looking it up in
        /// ZNetScene would correctly report that no such creature exists.
        /// </summary>
        public const string HeraldKillName = "EikthyrHerald";

        /// <summary>Shown over the Herald, and in the message when it falls.</summary>
        public const string HeraldName = "Eikthyr's Herald";

        /// <summary>Two stars: the biggest, fastest deer the game can make without new assets.</summary>
        private const int HeraldLevel = 3;

        /// <summary>
        /// How far from the player the Herald is placed. Far enough not to appear on top of them,
        /// near enough to be findable without a search.
        /// </summary>
        private const float HeraldSpawnDistance = 24f;

        /// <summary>Candidate lightning effects, tried in order; the first that resolves is used.</summary>
        private static readonly string[] LightningPrefabs =
            { "fx_eikthyr_stomp", "vfx_lightning", "fx_lightning", "fx_Eikthyr_stomp" };

        private readonly IConfiguration _cfg;
        private readonly System.Random _rng;
        private readonly List<Character> _scanBuffer = new List<Character>();

        /// <summary>The live Herald, by ZDOID. Empty when none is out.</summary>
        private ZDOID _herald = ZDOID.None;

        /// <summary>
        /// Resolved once per run: the first lightning prefab that exists, or null when none do.
        /// Cached because a miss means walking the whole candidate list on every deer death.
        /// </summary>
        private GameObject _lightning;
        private bool _lightningResolved;

        public DeerHerd(IConfiguration cfg, System.Random rng)
        {
            _cfg = cfg;
            _rng = rng ?? new System.Random();
        }

        /// <summary>Forgets the Herald and the cached effect. Called when a run starts or ends.</summary>
        public void Reset()
        {
            _herald = ZDOID.None;
            _lightning = null;
            _lightningResolved = false;
        }

        /// <summary>
        /// Stars unstarred deer near the player. Call about once a second, only during Act I.
        ///
        /// Two guards matter. Deer are only upgraded at FULL health, because SetLevel recomputes max
        /// health and would otherwise heal — or, worse, appear to heal — a deer the player was
        /// halfway through killing. And each deer is rolled once, since a deer that lost the roll
        /// would otherwise be rolled again every second until it won.
        /// </summary>
        public void UpgradeNearbyDeer(Vector3 origin, float radius)
        {
            if (_cfg.RunDeerStarChance <= 0f) return;

            _scanBuffer.Clear();
            try { Character.GetCharactersInRange(origin, radius, _scanBuffer); }
            catch { return; }

            int level = Mathf.Clamp(_cfg.RunDeerStarLevel, 1, 3);

            foreach (var c in _scanBuffer)
            {
                if (c == null || c.IsTamed() || c.IsPlayer()) continue;
                if (PrefabNameOf(c) != DeerPrefab) continue;
                if (c.GetLevel() >= level) continue;
                if (c.GetHealth() < c.GetMaxHealth()) continue;

                // Rolled once per deer: the marker rides the character itself, so a deer that lost
                // is not re-rolled every second for the rest of its life.
                if (WasRolled(c)) continue;
                MarkRolled(c);

                if (_rng.NextDouble() > _cfg.RunDeerStarChance) continue;

                try { c.SetLevel(level); }
                catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Could not star a deer: {e.Message}"); }
            }

            _scanBuffer.Clear();
        }

        /// <summary>
        /// True when the Herald is alive somewhere in the world — including in an unloaded zone,
        /// which is why this asks the ZDO rather than looking for a GameObject.
        /// </summary>
        public bool HeraldAlive =>
            _herald != ZDOID.None && ZDOMan.instance != null && ZDOMan.instance.GetZDO(_herald) != null;

        /// <summary>
        /// Places the Herald near the player, if one is not already out. Returns true when it spawned.
        ///
        /// Non-persistent, like the Packbrother wolves: a Herald that survived a reload would litter
        /// the world, whereas one that vanishes is simply respawned by the next call — which is what
        /// makes this self-healing if the player logs out mid-hunt.
        /// </summary>
        public bool TrySpawnHerald(Player player)
        {
            if (player == null || HeraldAlive) return false;

            var scene = ZNetScene.instance;
            var prefab = scene == null ? null : scene.GetPrefab(DeerPrefab);
            if (prefab == null)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Cannot spawn the Herald: no '{DeerPrefab}' prefab.");
                return false;
            }

            // A ring around the player rather than straight ahead, so it is not always in the same
            // place relative to where they happen to be facing.
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * HeraldSpawnDistance;
            Vector3 pos = player.transform.position + offset;

            try { pos.y = ZoneSystem.instance.GetSolidHeight(pos) + 0.5f; }
            catch { /* Keep the player's own height if the ground cannot be sampled. */ }

            var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (inst == null) return false;

            var ch = inst.GetComponent<Character>();
            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;

            if (ch == null || zdo == null)
            {
                // Never leave a half-built Herald standing: with no ZDO it is untrackable, so its
                // death could never be attributed and the quest step could never complete.
                UnityEngine.Object.Destroy(inst);
                return false;
            }

            zdo.Persistent = false;
            ch.SetLevel(HeraldLevel);
            ch.m_name = HeraldName;

            _herald = zdo.m_uid;
            return true;
        }

        /// <summary>
        /// Answers what a death means to the herd.
        ///
        /// Returns the synthetic <see cref="HeraldKillName"/> when the Herald itself died — matched
        /// by identity, not by name, so a player who happens to be killing ordinary deer cannot
        /// finish the Herald's step by accident. Returns null for anything else.
        /// </summary>
        public string OnCharacterDied(Character c)
        {
            if (c == null) return null;

            bool wasHerald = IsHerald(c);
            if (wasHerald) _herald = ZDOID.None;

            if (PrefabNameOf(c) != DeerPrefab) return null;

            ContestTheKill(c.transform.position);
            Strike(c.transform.position);

            return wasHerald ? HeraldKillName : null;
        }

        private bool IsHerald(Character c)
        {
            if (_herald == ZDOID.None) return false;

            var view = c.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && zdo.m_uid == _herald;
        }

        /// <summary>
        /// The forest notices. Greylings are drawn to the carcass, which is how a hunt gets tense
        /// without deer needing to do something they cannot do.
        ///
        /// A CHANCE rather than a certainty, on purpose: an ambush every single time stops being
        /// tension and becomes a tax on hunting at all.
        /// </summary>
        private void ContestTheKill(Vector3 position)
        {
            if (_cfg.RunDeerGreylingChance <= 0f) return;
            if (_rng.NextDouble() > _cfg.RunDeerGreylingChance) return;

            var scene = ZNetScene.instance;
            var prefab = scene == null ? null : scene.GetPrefab(ContestPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] No '{ContestPrefab}' prefab — deer kills go uncontested.");
                return;
            }

            int count = 1 + _rng.Next(Math.Max(1, _cfg.RunDeerGreylingMax));

            for (int i = 0; i < count; i++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                Vector3 pos = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 4f;

                var inst = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (inst == null) continue;

                // Non-persistent for the same reason the Herald is: summoned company should not
                // outlive the session and accumulate in someone's world.
                var view = inst.GetComponent<ZNetView>();
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                if (zdo != null) zdo.Persistent = false;
            }
        }

        /// <summary>
        /// A crack of lightning over the carcass — Eikthyr's motif, and pure flavour.
        ///
        /// The prefab name is asset data this build cannot verify, so it tries several candidates
        /// and settles for none of them rather than failing loudly every time a deer dies. Flavour
        /// that quietly does not happen is an acceptable outcome; a log line per kill is not.
        /// </summary>
        private void Strike(Vector3 position)
        {
            if (!_cfg.RunDeerLightning) return;

            if (!_lightningResolved)
            {
                _lightningResolved = true;
                var scene = ZNetScene.instance;

                if (scene != null)
                    _lightning = LightningPrefabs.Select(scene.GetPrefab).FirstOrDefault(p => p != null);

                if (_lightning == null)
                    Debug.Log("[ICanShowYouTheWorld] No lightning effect prefab resolved; deer kills will be quiet.");
            }

            if (_lightning == null) return;

            try { UnityEngine.Object.Instantiate(_lightning, position + Vector3.up, Quaternion.identity); }
            catch (Exception e) { Debug.LogWarning($"[ICanShowYouTheWorld] Lightning effect failed: {e.Message}"); }
        }

        // --- Once-per-deer roll marker ---
        //
        // Kept as a set of ZDOIDs rather than a flag on the character, because a Character is a
        // Unity object that can be destroyed and re-created as its zone unloads and reloads, while
        // the ZDOID is stable across that. Bounded so a very long run cannot grow it without limit.
        private const int MaxRolled = 512;
        private readonly HashSet<ZDOID> _rolled = new HashSet<ZDOID>();

        private bool WasRolled(Character c)
        {
            var id = ZdoIdOf(c);
            return id != ZDOID.None && _rolled.Contains(id);
        }

        private void MarkRolled(Character c)
        {
            var id = ZdoIdOf(c);
            if (id == ZDOID.None) return;

            // Oldest-out is not worth tracking for a cosmetic roll: clearing wholesale simply means
            // a few deer get a second chance at a star, which is harmless.
            if (_rolled.Count >= MaxRolled) _rolled.Clear();

            _rolled.Add(id);
        }

        private static ZDOID ZdoIdOf(Character c)
        {
            var view = c.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo == null ? ZDOID.None : zdo.m_uid;
        }

        private static string PrefabNameOf(Character c)
        {
            var go = c.gameObject;
            return go == null ? string.Empty : go.name.Replace("(Clone)", string.Empty);
        }
    }
}
