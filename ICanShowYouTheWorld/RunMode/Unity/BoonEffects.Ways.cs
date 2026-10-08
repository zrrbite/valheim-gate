using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The ways' always-on engines (2026-10-08, class balance; docs/superpowers/specs/2026-10-08-class-balance-design.md).
    /// Ticked once a second by RunService.PollCompanionPassives. Each engine keys on its way's PASSIVE boon id, so it
    /// runs exactly while the way is held, and Unapply of that id takes it down.
    /// </summary>
    public partial class BoonEffects
    {
        private bool Holds(string id) => _heldBoons().Any(h => h.Def.Id == id);

        /// <summary>Once a second while a run is live.</summary>
        public void TickWays()
        {
            int gods = _defeatedBossCount();
            try { TickPackRegen(gods); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Pack regen: " + e.Message); }
        }

        /// <summary>The Hunter's engine tempered by the Queen: every animal on her side within 30 m mends.</summary>
        private void TickPackRegen(int gods)
        {
            float rate = WayRules.PackRegenPerSecond(gods);
            if (rate <= 0f || !Holds("shepherd")) return;
            var player = Player.m_localPlayer;
            if (player == null) return;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 30f, list);
            foreach (var c in list)
                if (c != null && !c.IsPlayer() && c.IsTamed() && !c.IsDead()) c.Heal(rate, false);
        }
    }
}
