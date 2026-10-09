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
            try { TickSongs(gods); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Songs: " + e.Message); }
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

        // --- The Skald ---

        private SkaldSong _song = SkaldSong.None;
        private SkaldSong _secondSong = SkaldSong.None;
        private float _crescendoAt = float.NegativeInfinity;
        private float _crescendoUntil = float.NegativeInfinity;

        // What ApplySongs last laid its loans against: a respawn (a new Player) or a felled god (the tempered numbers,
        // the second song) is noticed by the tick and re-laid, rather than waiting for the next key.
        private Player _songPlayer;
        private int _songGods = -1;

        private const string WarSongAllyName = "ICSYTW_WarSongAlly";
        private static readonly int WarSongAllyHash = WarSongAllyName.GetStableHashCode();

        /// <summary>One template per strength the song can have (the sung factor, the crescendo's), keyed by the factor
        /// to 0.01. AddStatusEffect clones what it is given, so a template is never consumed: nothing is created per
        /// ally per second (an unreferenced ScriptableObject would stand until assets unload).</summary>
        private static readonly Dictionary<int, SE_Stats> WarSongAllyTemplates = new Dictionary<int, SE_Stats>();

        private static string SongBoonId(SkaldSong song)
        {
            switch (song)
            {
                case SkaldSong.March: return "march";
                case SkaldSong.War: return "warsong";
                case SkaldSong.Bragi: return "bragi";
                default: return null;
            }
        }

        private static SkaldSong SongOf(string boonId)
        {
            switch (boonId)
            {
                case "march": return SkaldSong.March;
                case "warsong": return SkaldSong.War;
                case "bragi": return SkaldSong.Bragi;
                default: return SkaldSong.None;
            }
        }

        /// <summary>The three songs' boons. The HUD reads them as switches, not as cooldowns or charges.</summary>
        public static bool IsSongBoon(string boonId) => SongOf(boonId) != SkaldSong.None;

        /// <summary>True while this song is sung: the chosen one, and the one before it after the Queen. For the HUD.</summary>
        public bool IsSung(string boonId)
        {
            var song = SongOf(boonId);
            return song != SkaldSong.None && Sung(song, _defeatedBossCount());
        }

        private static string SongName(SkaldSong song)
        {
            switch (song)
            {
                case SkaldSong.March: return "The Marching Song";
                case SkaldSong.War: return "The War Song";
                case SkaldSong.Bragi: return "The Saga of Bragi";
                default: return "";
            }
        }

        /// <summary>A rung key: switch to that song; a crescendo if one is due.</summary>
        private bool SwitchSong(SkaldSong song)
        {
            if (FindHeld(SongBoonId(song)) == null || Player.m_localPlayer == null) return false;
            if (_song == song) { LastActivationMessage = "That song is already sung."; return false; }

            _secondSong = _song;
            _song = song;
            bool swell = WayRules.CrescendoDue(Time.time, _crescendoAt);
            if (swell)
            {
                _crescendoAt = Time.time;
                _crescendoUntil = Time.time + WayRules.CrescendoSeconds;
            }

            try { ApplySongs(); }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Song failed: " + ex.Message);
                SafeInvoke(EndSongs);
                return false;
            }

            LastActivationMessage = swell ? "The song swells." : SongName(song) + ".";
            return true;
        }

        private bool Sung(SkaldSong s, int gods) => _song == s || (WayRules.SongsAtOnce(gods) > 1 && _secondSong == s);

        /// <summary>The standing songs' player halves, as loans (the existing lenders "march" and "warsong").</summary>
        private void ApplySongs()
        {
            int gods = _defeatedBossCount();
            float boost = Time.time < _crescendoUntil ? 2f : 1f;
            var player = Player.m_localPlayer;
            if (player == null) return;
            _songPlayer = player;
            _songGods = gods;

            RepayLender("march");
            if (Sung(SkaldSong.March, gods))
            {
                SyncLoanOwner(player);
                LendFieldFraction(player, "RunSpeed", "march", WayRules.MarchSpeed(gods) * boost);
                LendFieldFraction(player, "WalkSpeed", "march", WayRules.MarchSpeed(gods) * boost);
                LendFieldFraction(player, "JogSpeed", "march", WayRules.MarchSpeed(gods) * boost);   // the default gait
                LendFieldFraction(player, "StaminaRegen", "march", MarchStaminaFraction * boost);
            }

            if (Sung(SkaldSong.War, gods)) ApplyWeaponMultiplier(WarFactor(gods, boost), "warsong");
            else RemoveWeaponMultiplier("warsong");
        }

        /// <summary>The War Song as a damage factor: the sung bonus, doubled in a crescendo.</summary>
        private static float WarFactor(int gods, float boost) => 1f + (WayRules.WarDamage(gods) - 1f) * boost;

        /// <summary>
        /// Once a second: the allies' halves, Bragi's mending, and the crescendo running out. One song is always
        /// playing (spec): a Skald who holds the Marching Song and has chosen nothing yet - a fresh run, a resume,
        /// after a respawn - starts it here by himself. That is no switch, so no crescendo: _crescendoAt and
        /// _crescendoUntil stay untouched and the first real switch still swells.
        /// </summary>
        private void TickSongs(int gods)
        {
            if (!Holds("poet")) { if (_song != SkaldSong.None) EndSongs(); return; }

            var player = Player.m_localPlayer;
            if (player == null) return;

            if (_song == SkaldSong.None)
            {
                if (!Holds("march")) return;
                _song = SkaldSong.March;
                ApplySongs();
            }

            // The crescendo running out, a respawn and a felled god all change what the loans should be.
            bool expired = _crescendoUntil > 0f && Time.time >= _crescendoUntil;
            if (expired) _crescendoUntil = float.NegativeInfinity;
            if (expired || !ReferenceEquals(_songPlayer, player) || _songGods != gods) ApplySongs();

            float boost = Time.time < _crescendoUntil ? 2f : 1f;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 15f, list);

            bool bragi = Sung(SkaldSong.Bragi, gods);
            bool war = Sung(SkaldSong.War, gods);
            float heal = WayRules.BragiPerSecond(gods) * boost;
            if (bragi) player.Heal(heal, false);
            foreach (var c in list)
            {
                if (c == null || c.IsPlayer() || !c.IsTamed() || c.IsDead()) continue;
                if (bragi) c.Heal(heal, false);
                if (war) LayWarSong(c, WarFactor(gods, boost));
            }
        }

        /// <summary>The cached template for one strength; recreated if Unity has let go of it.</summary>
        private static SE_Stats WarSongAllyTemplate(float factor)
        {
            int key = (int)Math.Round(factor * 100f);
            if (WarSongAllyTemplates.TryGetValue(key, out var cached) && cached != null) return cached;

            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.hideFlags = HideFlags.DontUnloadUnusedAsset;
            se.name = WarSongAllyName;
            se.m_name = "War Song";
            se.m_ttl = 3f;
            se.m_modifyAttackSkill = Skills.SkillType.All;
            se.m_damageModifier = key / 100f;
            WarSongAllyTemplates[key] = se;
            return se;
        }

        /// <summary>The War Song on one ally: a 3 s SE_Stats, re-laid each second while in reach (per creature, never m_shared).</summary>
        private static void LayWarSong(Character c, float factor)
        {
            var view = c.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            if (!view.IsOwner()) view.ClaimOwnership();
            var seman = c.GetSEMan();
            if (seman == null) return;
            seman.RemoveStatusEffect(WarSongAllyHash, quiet: true);
            seman.AddStatusEffect(WarSongAllyTemplate(factor));
        }

        private void EndSongs()
        {
            _song = _secondSong = SkaldSong.None;
            _crescendoUntil = float.NegativeInfinity;
            _songPlayer = null;
            _songGods = -1;
            RepayLender("march");
            RemoveWeaponMultiplier("warsong");
        }

        // --- The Húskarl ---

        private const string GuardName = "ICSYTW_Guard";
        private static readonly int GuardHash = GuardName.GetStableHashCode();
        private static GuardEffect _guard;

        private static GuardEffect Guard()
        {
            if (_guard != null) return _guard;
            var se = ScriptableObject.CreateInstance<GuardEffect>();
            se.name = GuardName;
            se.m_name = "Guard";
            se.m_tooltip = "What lands on your shield comes back to you.";
            se.m_ttl = 0f;
            se.m_blockStaminaUseModifier = -WayRules.GuardStaminaRefund;
            _guard = se;
            return se;
        }

        private static bool _guardLogged;

        /// <summary>The Húskarl's passive: lays Guard on the player (idempotent; re-run on respawn).</summary>
        private void ApplyGuard()
        {
            try
            {
                var seman = Player.m_localPlayer?.GetSEMan();
                if (seman == null || seman.HaveStatusEffect(GuardHash)) return;
                GuardEffect.OnGuard = OnGuard;
                seman.AddStatusEffect(Guard());
                if (!_guardLogged)
                {
                    _guardLogged = true;
                    // The first launch's proof that a mod-defined StatusEffect subclass works (research: likely, not proven).
                    Debug.Log($"[ICanShowYouTheWorld] Guard on: SE '{GuardName}' ({seman.HaveStatusEffect(GuardHash)}).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Guard failed: {ex.Message}");
            }
        }

        private void UnapplyGuard()
        {
            GuardEffect.OnGuard = null;
            try { Player.m_localPlayer?.GetSEMan()?.RemoveStatusEffect(GuardHash, quiet: true); }
            catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] Guard removal failed: {ex.Message}"); }
        }

        private void OnGuard(bool parry)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            int gods = _defeatedBossCount();
            bool wall = IsWindowOpen("bulwark");
            player.Heal(parry ? WayRules.GuardParryHeal(gods, wall) : WayRules.GuardBlockHeal(gods, wall), false);
        }
    }
}
