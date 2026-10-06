using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Puts the run's ship fittings (<see cref="ShipFittings"/>) on every ship that is loaded, and
    /// gives each ship its own numbers back when the run ends.
    /// </summary>
    /// <remarks>
    /// The fittings FOLLOW THE CAPTAIN, so they are applied to ships rather than stored on one: once a
    /// second, each loaded ship's numbers are set from ITS OWN ORIGINALS times the run's fittings. A
    /// ship's originals are captured the first time it is seen, so a second pass never compounds the
    /// first. These are instance fields the game never saves - a ship that unloads and loads again is
    /// vanilla until the next pass, and nothing reaches the world save.
    ///
    /// Destroyed ships compare equal to null (Unity), which is exactly "gone": their entries are
    /// dropped, and a ship found again later is captured afresh from a vanilla instance.
    ///
    /// THE HELM'S SECOND KEY HAS NO COLLIDER OF ITS OWN, deliberately. The first draft put a small
    /// trigger around the controls, the way the witch's reforge works. On a ship that is a trap: a
    /// trigger parented under the ship joins the ship's rigidbody, Unity hands its trigger events to
    /// Ship's own OnTriggerEnter/Exit, and Ship.OnTriggerExit takes the player OFF the ship ("Player
    /// over board", m_players, s_currentShips, InNumShipVolumes) - so stepping away from the helm on
    /// deck would have told the game you had left the ship (1.0.16 IL). Instead the helm's own hover
    /// text gets a line (ShipControlls.m_hoverText, an instance field, restored at run end), and the
    /// host watches for Shift+E while the helm is looked at (RunService.HandleHelmInput).
    /// </remarks>
    internal sealed class Shipwright
    {
        private sealed class Original
        {
            public Ship Ship;
            public float Sail;
            public float Oars;
            public bool AshlandsReady;
            public WearNTear Hull;
            public HitData.DamageModifiers Damages;
            public ShipControlls Helm;
            public string HelmText;
        }

        private readonly Dictionary<int, Original> _ships = new Dictionary<int, Original>();
        private bool _reported;

        /// <summary>Set by the host every tick: the helm offers fittings (a ship built, or the raft step done).</summary>
        public bool Offering { get; private set; }

        /// <summary>Set by the host every tick: what the ship already carries, for the helm's hover.</summary>
        public string Fitted { get; private set; }

        /// <summary>About once a second while a run is live.</summary>
        public void Tick(ShipFittingState fit, bool offering)
        {
            Offering = offering;
            Fitted = ShipFittings.Summary(fit);

            try
            {
                foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
                {
                    if (ship == null) continue;
                    int id = ship.GetInstanceID();
                    if (!_ships.TryGetValue(id, out var o) || !ReferenceEquals(o.Ship, ship))
                    {
                        o = Capture(ship);
                        _ships[id] = o;
                    }
                    Apply(o, fit);
                    Label(o, offering);
                }

                foreach (var gone in _ships.Where(kv => kv.Value.Ship == null).Select(kv => kv.Key).ToList())
                    _ships.Remove(gone);
            }
            catch (Exception ex)
            {
                if (_reported) return;
                _reported = true;
                Debug.LogWarning("[ICanShowYouTheWorld] Ship fittings could not be applied: " + ex.Message);
            }
        }

        /// <summary>Every loaded ship back to its own numbers and its helm's own hover text. At run end.</summary>
        public void Restore()
        {
            foreach (var o in _ships.Values)
            {
                try
                {
                    if (o.Helm != null) o.Helm.m_hoverText = o.HelmText;
                    if (o.Ship == null) continue;
                    o.Ship.m_sailForceFactor = o.Sail;
                    o.Ship.m_backwardForce = o.Oars;
                    o.Ship.m_ashlandsReady = o.AshlandsReady;
                    if (o.Hull != null) o.Hull.m_damages = o.Damages;
                }
                catch { }
            }
            _ships.Clear();
            Offering = false;
            Fitted = null;
            _reported = false;
        }

        private static Original Capture(Ship ship)
        {
            var hull = ship.GetComponent<WearNTear>();
            var helm = ship.m_shipControlls;
            if (helm == null) helm = ship.GetComponentInChildren<ShipControlls>();
            return new Original
            {
                Helm = helm,
                HelmText = helm != null ? helm.m_hoverText : null,
                Ship = ship,
                Sail = ship.m_sailForceFactor,
                Oars = ship.m_backwardForce,
                AshlandsReady = ship.m_ashlandsReady,
                Hull = hull,
                Damages = hull != null ? hull.m_damages : default(HitData.DamageModifiers),
            };
        }

        private static void Apply(Original o, ShipFittingState fit)
        {
            float sail = ShipFittings.SailMultiplier(fit);
            o.Ship.m_sailForceFactor = o.Sail * sail;
            o.Ship.m_backwardForce = o.Oars * sail;
            o.Ship.m_ashlandsReady = o.AshlandsReady || ShipFittings.AshlandsReady(fit);

            if (o.Hull == null) return;
            int tier = fit?.Hull ?? 0;
            o.Hull.m_damages = tier <= 0 ? o.Damages : Fortify(o.Damages, Plating(tier));
        }

        /// <summary>The resistance a hull tier stands for: slightly, plainly, very.</summary>
        private static HitData.DamageModifier Plating(int tier) =>
            tier == 1 ? HitData.DamageModifier.SlightlyResistant
            : tier == 2 ? HitData.DamageModifier.Resistant
            : HitData.DamageModifier.VeryResistant;

        /// <summary>Each damage type at the stronger of the ship's own and the plating - never weaker.</summary>
        private static HitData.DamageModifiers Fortify(HitData.DamageModifiers own, HitData.DamageModifier plating)
        {
            var d = own;
            d.m_blunt = Stronger(d.m_blunt, plating);
            d.m_slash = Stronger(d.m_slash, plating);
            d.m_pierce = Stronger(d.m_pierce, plating);
            d.m_chop = Stronger(d.m_chop, plating);
            d.m_pickaxe = Stronger(d.m_pickaxe, plating);
            d.m_fire = Stronger(d.m_fire, plating);
            d.m_frost = Stronger(d.m_frost, plating);
            d.m_lightning = Stronger(d.m_lightning, plating);
            d.m_poison = Stronger(d.m_poison, plating);
            d.m_spirit = Stronger(d.m_spirit, plating);
            return d;
        }

        private static HitData.DamageModifier Stronger(HitData.DamageModifier own, HitData.DamageModifier plating) =>
            Taken(plating) < Taken(own) ? plating : own;

        /// <summary>The share of a hit a modifier lets through. Immune and Ignore are kept as they are.</summary>
        private static float Taken(HitData.DamageModifier m)
        {
            switch (m)
            {
                case HitData.DamageModifier.Immune:
                case HitData.DamageModifier.Ignore: return 0f;
                case HitData.DamageModifier.VeryResistant: return 0.25f;
                case HitData.DamageModifier.Resistant: return 0.5f;
                case HitData.DamageModifier.SlightlyResistant: return 0.75f;
                case HitData.DamageModifier.SlightlyWeak: return 1.25f;
                case HitData.DamageModifier.Weak: return 1.5f;
                case HitData.DamageModifier.VeryWeak: return 2f;
                default: return 1f;
            }
        }

        /// <summary>The helm's hover gains the fitting line while the helm offers fittings, and loses it after.</summary>
        private void Label(Original o, bool offering)
        {
            if (o.Helm == null || o.HelmText == null) return;
            if (!offering) { o.Helm.m_hoverText = o.HelmText; return; }

            // ShipControlls.GetHoverText prefixes "[Use] " and localises the whole string, so the
            // $KEY_ tokens here are resolved by the game itself.
            string alt = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive() ? "$KEY_AltKeys" : "$KEY_AltPlace";
            string line = $"\n[<color=yellow><b>{alt} + $KEY_Use</b></color>] Fit out the ship";
            if (!string.IsNullOrEmpty(Fitted)) line += $"  <color=#c8a85a>({Fitted})</color>";
            o.Helm.m_hoverText = o.HelmText + line;
        }
    }
}
