using System;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.Services;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// MACBOOK-TEMP (2026-10-06): the dev ship key - a Karve on the nearest deep water, and you at its
    /// helm. Remove with the rest of the MacBook test keys. Testing the fittings, the god's wind and the
    /// Wind-horn wants a ship at sea; on a MacBook with no mouse, building one and walking to a coast
    /// are both out ("it would be nice if i could be teleported to water to spawn said ship").
    /// </summary>
    /// <remarks>
    /// Three stages, because a ship can only be built where the world is loaded. A far sea is reached
    /// first: you are sent to the shore short of the berth (<see cref="Arriving"/>), and the ship is
    /// built once you are there. Then you are put on its deck at the helm and the helm is taken for you
    /// (<see cref="Boarding"/>): ShipControlls.Interact asks only that you stand on that ship, within
    /// reach, and not over-weight (1.0.17 IL). The ship is claimed as yours, so the built-piece scan
    /// counts it (the helm then offers fittings) and a hammer can take it down again.
    /// </remarks>
    internal sealed class DevShip
    {
        private const string Prefab = "Karve";
        private const float SearchRadius = 4000f;

        /// <summary>A berth this close is in loaded ground already: build at once.</summary>
        private const float BuildHere = 40f;

        /// <summary>A far berth: you arrive this far short of it, toward where you stood.</summary>
        private const float Approach = 14f;

        private const float ArriveSeconds = 40f;
        private const float BoardSeconds = 12f;

        private enum Stage { None, Arriving, Boarding }

        private readonly Action<string> _say;
        private Stage _stage;
        private DevSea.Berth _berth;
        private Ship _ship;
        private float _until;
        private float _nextTry;

        public DevShip(Action<string> say)
        {
            _say = say;
        }

        private Vector3 Dir => new Vector3(_berth.DirX, 0f, _berth.DirZ);

        private Vector3 BerthAt(float water) => new Vector3(_berth.X, water, _berth.Z);

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public void Launch(Player player)
        {
            var world = WorldGenerator.instance;
            var zone = ZoneSystem.instance;
            if (player == null || world == null || zone == null)
            {
                _say("DEV: the world is not ready for a ship.");
                return;
            }

            float water = zone.m_waterLevel;
            Vector3 from = player.transform.position;
            var found = DevSea.Find((x, z) => world.GetHeight(x, z), water, from.x, from.z, SearchRadius);
            if (!found.HasValue)
            {
                _stage = Stage.None;
                _say($"DEV: no sea within {SearchRadius / 1000f:0} km.");
                return;
            }

            _berth = found.Value;
            float away = Flat(from, BerthAt(water));
            if (away <= BuildHere)
            {
                Build(player);
                return;
            }

            var teleport = ModBootstrap.GetService<ITeleportService>();
            if (teleport == null)
            {
                _say("DEV: no teleport service.");
                return;
            }

            Vector3 shore = BerthAt(water) - Dir * Approach;
            shore.y = Mathf.Max(world.GetHeight(shore.x, shore.z), water) + 1.5f;
            teleport.TeleportTo(shore, Quaternion.LookRotation(Dir));
            _stage = Stage.Arriving;
            _until = Time.time + ArriveSeconds;
            _say($"DEV: the nearest sea is {away:0} m away - going there, and the ship follows.");
        }

        /// <summary>Every frame of a dev run. Does nothing unless a ship is on its way.</summary>
        public void Tick(Player player)
        {
            if (_stage == Stage.None) return;
            if (player == null)
            {
                _stage = Stage.None;
                return;
            }

            try
            {
                if (_stage == Stage.Arriving) TickArriving(player);
                else TickBoarding(player);
            }
            catch (Exception ex)
            {
                _stage = Stage.None;
                Debug.LogWarning("[ICanShowYouTheWorld] DEV ship failed: " + ex);
                _say("DEV: the ship failed - see the player log.");
            }
        }

        private void TickArriving(Player player)
        {
            if (Time.time > _until)
            {
                _stage = Stage.None;
                _say("DEV: never reached the sea - press again.");
                return;
            }

            var scene = ZNetScene.instance;
            var zone = ZoneSystem.instance;
            if (scene == null || zone == null || player.IsTeleporting()) return;

            Vector3 berth = BerthAt(zone.m_waterLevel);
            if (!scene.IsAreaReady(berth) || Flat(player.transform.position, berth) > BuildHere + Approach) return;

            Build(player);
        }

        private void Build(Player player)
        {
            _stage = Stage.None;

            var scene = ZNetScene.instance;
            var zone = ZoneSystem.instance;
            var prefab = scene != null ? scene.GetPrefab(Prefab) : null;
            if (prefab == null || zone == null)
            {
                _say("DEV: no Karve to build.");
                return;
            }

            var made = UnityEngine.Object.Instantiate(prefab, BerthAt(zone.m_waterLevel) + Vector3.up * 0.3f,
                                                      Quaternion.LookRotation(Dir));
            _ship = made != null ? made.GetComponent<Ship>() : null;
            if (_ship == null)
            {
                _say("DEV: the Karve would not build.");
                return;
            }

            // Yours, as if built: the scan counts it (and the helm offers fittings), a hammer removes it.
            var piece = made.GetComponent<Piece>();
            var profile = Game.instance?.GetPlayerProfile();
            if (piece != null && profile != null)
                piece.SetCreator(profile.GetPlayerID(), default(Splatform.PlatformUserID));

            var teleport = ModBootstrap.GetService<ITeleportService>();
            var helm = made.GetComponentInChildren<ShipControlls>();
            if (teleport == null || helm == null || helm.m_attachPoint == null)
            {
                _say("DEV: a Karve is on the water; swim to it and take the helm with E.");
                return;
            }

            teleport.TeleportTo(helm.m_attachPoint.position + Vector3.up * 0.5f, made.transform.rotation);
            _stage = Stage.Boarding;
            _until = Time.time + BoardSeconds;
            _nextTry = 0f;
            _say("DEV: a Karve, yours. Taking the helm...");
        }

        private void TickBoarding(Player player)
        {
            if (_ship == null)   // Unity's null: destroyed counts
            {
                _stage = Stage.None;
                _say("DEV: the ship is gone.");
                return;
            }

            if (player.IsAttachedToShip())
            {
                _stage = Stage.None;
                _say("DEV: at the helm. W to sail; Shift+E for the fittings.");
                return;
            }

            if (Time.time > _until)
            {
                _stage = Stage.None;
                _say("DEV: the Karve is here - take the helm with E (not while over-weight).");
                return;
            }

            if (player.IsTeleporting() || Time.time < _nextTry) return;
            _nextTry = Time.time + 0.5f;

            var helm = _ship.GetComponentInChildren<ShipControlls>();
            if (helm != null && player.GetStandingOnShip() == _ship) helm.Interact(player, false, false);
        }
    }
}
