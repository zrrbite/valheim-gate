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
            try { TickFury(gods); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Fury: " + e.Message); }
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

        // --- The Berserker ---

        private readonly FuryMeter _fury = new FuryMeter();
        private int _lastEnemyHits = -1;
        private float _lastFuryBonus = -1f;

        /// <summary>The game's own landed-hit counter (PlayerStatType.EnemyHits): one per creature a hit lands on.</summary>
        private static int ReadEnemyHits()
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || profile.m_playerStats == null || profile.m_playerStats.Length == 0) return -1;
            return profile.m_playerStats[0].m_stats.TryGetValue(PlayerStatType.EnemyHits, out float v) ? (int)v : 0;
        }

        /// <summary>
        /// Fury, once a second while the Berserker's "warrior" is held: new landed hits since the last tick, the fade,
        /// and the weapon factor "fury" (inside the x2.5 ceiling). Bloodied heals through OnFoeDamaged.
        /// </summary>
        private void TickFury(int gods)
        {
            if (!Holds("warrior"))
            {
                if (_lastFuryBonus >= 0f) { RemoveWeaponMultiplier("fury"); _lastFuryBonus = -1f; }
                _fury.Reset();
                _lastEnemyHits = -1;
                return;
            }

            _fury.SetMax(WayRules.FuryMax(gods));
            int hits = ReadEnemyHits();
            if (hits >= 0)
            {
                if (_lastEnemyHits >= 0 && hits > _lastEnemyHits) _fury.Hit(hits - _lastEnemyHits, Time.time);
                _lastEnemyHits = hits;
            }
            _fury.Tick(Time.time);
            RefreshFuryFactor();

            SubscribeFoes();
        }

        /// <summary>
        /// Puts the weapon factor "fury" at the meter's present bonus if it has moved. Called by the tick, and by
        /// Blood Rage and Warcry right after they fill the meter, so a rage lands on the keypress, not a second later.
        /// </summary>
        private void RefreshFuryFactor()
        {
            if (Math.Abs(_fury.Bonus - _lastFuryBonus) <= 0.001f) return;
            _lastFuryBonus = _fury.Bonus;
            if (_fury.Bonus > 0f) ApplyWeaponMultiplier(1f + _fury.Bonus, "fury");
            else RemoveWeaponMultiplier("fury");
        }

        /// <summary>m_onDamaged on every creature within 30 m, idempotently (-= then +=), for Bloodied.</summary>
        private void SubscribeFoes()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 30f, list);
            foreach (var c in list)
            {
                if (c == null || c.IsPlayer()) continue;
                c.m_onDamaged -= OnFoeDamaged;
                c.m_onDamaged += OnFoeDamaged;
            }
        }

        /// <summary>Bloodied: at five Fury or more, each hit the player lands heals 10% of the damage it dealt.</summary>
        private void OnFoeDamaged(float damage, Character attacker)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || !ReferenceEquals(attacker, player) || !_fury.Bloodied || !Holds("warrior")) return;
                player.Heal(damage * WayRules.BloodiedShare, false);
            }
            catch { /* a missed heal is a missed heal */ }
        }
    }
}
