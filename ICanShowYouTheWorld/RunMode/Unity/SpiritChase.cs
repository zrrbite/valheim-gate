using System;
using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Act I's opening mystery: a light in the dark that knows where Eikthyr is.
    ///
    /// Eikthyr spawns darkness, so his act begins with something you can only follow, not fight.
    /// The spirit drifts at the edge of the world, and reaching it — not killing it — is what tells
    /// you where the hunt goes.
    ///
    /// Deliberately VAGUER than the Herald. The Herald says "north-east, 140m" because it is a hunt
    /// and a hunt has a quarry. This says "far to the north-east" and nothing more, because it is a
    /// rumour. A number would turn a chase into a walk.
    ///
    /// The prefab is a fallback CHAIN — a real spirit if this build has one, a named starred deer
    /// if it does not. That is not politeness: this step opens the hunt track, a track is a linear
    /// chain, and Act I has already paid once for putting an unspawnable step in front of others.
    /// </summary>
    internal class SpiritChase
    {
        private readonly IConfiguration _cfg;
        private readonly System.Random _rng;

        /// <summary>
        /// Tried in order. "Ghost" is the only one this assembly can be shown to reference, so the
        /// wisp is a hope and the deer is a guarantee.
        /// </summary>
        private static readonly string[] Candidates = { "Wisp", "Ghost", "Deer" };

        /// <summary>What the questline step matches on. Synthetic — no prefab is called this.</summary>
        public const string FoundMeasure = "SpiritFound";

        private const float MinDistance = 200f;
        private const float MaxDistance = 380f;

        /// <summary>How close the player must get before it is spawned, and before it counts as reached.</summary>
        private const float SpawnRange = 70f;
        private const float ReachRange = 12f;

        private Vector3? _target;
        private ZDOID _spirit = ZDOID.None;
        private bool _found;

        public SpiritChase(IConfiguration cfg, System.Random rng)
        {
            _cfg = cfg;
            _rng = rng ?? new System.Random();
        }

        public bool Found => _found;

        /// <summary>
        /// True once it has actually been placed in the world, so the host can SAY so.
        ///
        /// "I was not sure I saw the light" is the failure this answers: a thing that appears
        /// silently at the edge of vision, at night, is a thing you can finish the step without
        /// ever having seen.
        /// </summary>
        public bool Spawned { get; private set; }

        /// <summary>
        /// Metres to the light, or -1 when there is nothing to be far from.
        ///
        /// The BEARING is deliberately vague — a rumour, not a readout — but the player still has
        /// to be able to tell warm from cold, and prose alone could not do it: "antlers somewhere
        /// out past the firelight" reads like a hint and carries nothing. This is what the bar
        /// draws.
        /// </summary>
        public float DistanceFrom(Player player)
        {
            if (player == null || _found) return -1f;

            Vector3? at = Position() ?? _target;
            if (at == null) return -1f;

            Vector3 delta = at.Value - player.transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <summary>The furthest it is ever placed, so closeness has a fixed scale to fill.</summary>
        public const float Reach = MaxDistance;

        public void Reset()
        {
            _target = null;
            _spirit = ZDOID.None;
            _found = false;
            Spawned = false;
        }

        /// <summary>The remembered place, so a resume does not send the player somewhere new.</summary>
        public Vector3? Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>
        /// Call about once a second while the spirit step is in play. Returns true on the tick the
        /// player reaches it.
        ///
        /// Remembers a PLACE and spawns near it, rather than spawning far away and hoping — the
        /// exact fix the Herald needed, where a creature placed outside the loaded area had its
        /// non-persistent ZDO released and respawned in a loop once a second.
        /// </summary>
        public bool Tick(Player player)
        {
            if (player == null || _found) return false;

            Vector3 here = player.transform.position;

            if (_target == null)
            {
                // On land, and only on land. A random bearing at a random distance put this in the
                // sea once already: "the marker for the light was in the water, I had to go out to
                // trigger it." Nothing is placed until somewhere standable is found.
                _target = BiomeCompass.LandNear(here, MinDistance, MaxDistance, _rng);
                if (_target == null) return false;
            }

            // Flat distance: the target carries its own ground height, and comparing a hilltop
            // to a valley floor in 3D would report the player as further away than they are.
            Vector3 flat = _target.Value; flat.y = here.y;
            float toTarget = Vector3.Distance(here, flat);

            if (toTarget <= SpawnRange && !Alive) Spawn(_target.Value);

            // Reached either the spirit itself or the place it haunts. Both count: a light you
            // walked into is a light you found, and the alternative is a step that fails because a
            // zone unloaded at the wrong moment.
            Vector3? live = Position();
            float reach = live != null ? Vector3.Distance(here, live.Value) : toTarget;

            if (reach > ReachRange) return false;

            _found = true;
            Despawn();
            return true;
        }

        /// <summary>
        /// Which way the lights are, in words. No distance, on purpose — see the class summary.
        /// </summary>
        public string Bearing(Player player)
        {
            if (player == null || _found) return null;

            Vector3? at = Position() ?? _target;
            if (at == null) return null;

            Vector3 delta = at.Value - player.transform.position;
            delta.y = 0f;

            float distance = delta.magnitude;
            if (distance < 1f) return null;

            string where = BiomeCompass.Compass(delta);

            // Four bands, and the wording carries the distance rather than a number. The player
            // learns they are getting warmer without ever being told how warm.
            if (distance > 300f) return $"Something pale drifts far to the {where}.";
            if (distance > 120f) return $"A light, somewhere to the {where}.";
            if (distance > 40f)  return $"The light is close now, to the {where}.";
            return "It is here. Very close.";
        }

        private bool Alive => Position() != null;

        private Vector3? Position()
        {
            if (_spirit == ZDOID.None) return null;

            try
            {
                var zdo = ZDOMan.instance?.GetZDO(_spirit);
                return zdo == null ? (Vector3?)null : zdo.GetPosition();
            }
            catch
            {
                return null;
            }
        }

        private void Spawn(Vector3 at)
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
                Debug.LogError("[ICanShowYouTheWorld] No spirit prefab found — the chase cannot start.");
                return;
            }

            var inst = UnityEngine.Object.Instantiate(prefab, at + Vector3.up * 1.5f, Quaternion.identity);
            if (inst == null) return;

            // WHICH prefab actually got used, once. There is no wisp in this build's assembly, so
            // the chain almost certainly lands on Ghost — and "I was not sure I saw the light"
            // could mean it looked wrong or that it never spawned. One line separates those.
            Debug.Log($"[ICanShowYouTheWorld] The pale light is a '{chosen}'.");

            // Half again as big as whatever it is. A Ghost at night against dark meadows is easy
            // to walk past, and the whole step is walking toward it.
            inst.transform.localScale *= 1.6f;

            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null)
            {
                UnityEngine.Object.Destroy(inst);
                return;
            }

            zdo.Persistent = false;
            _spirit = zdo.m_uid;

            var ch = inst.GetComponent<Character>();
            if (ch != null)
            {
                // TAMED, so it never fights. This is a thing to follow, not a thing to kill, and a
                // Ghost that decided to defend itself would end an Act I player in leather.
                try { ch.SetTamed(true); } catch { /* flavour, not function */ }
                ch.m_name = "A pale light";
            }

            // The fallback deer must not look like the deer you are about to hunt.
            if (chosen == "Deer" && ch != null)
            {
                try { ch.SetLevel(3); } catch { }
            }

            Spawned = true;
        }

        private void Despawn()
        {
            var pos = Position();
            if (pos == null) { _spirit = ZDOID.None; return; }

            try
            {
                var zdo = ZDOMan.instance?.GetZDO(_spirit);
                if (zdo != null) ZDOMan.instance.DestroyZDO(zdo);
            }
            catch
            {
                // A spirit that outlives the moment is untidy, not broken.
            }

            _spirit = ZDOID.None;
        }
    }
}
