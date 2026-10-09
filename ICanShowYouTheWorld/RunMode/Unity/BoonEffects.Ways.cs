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
            try { TickSmith(gods); }
            catch (Exception e) { Debug.LogWarning("[ICanShowYouTheWorld] Smith: " + e.Message); }
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

        // --- The Sæfari ---

        private const string TideName = "ICSYTW_TideBorne";
        private static readonly int TideHash = TideName.GetStableHashCode();
        private static SE_Stats _tideBorne;

        /// <summary>
        /// One template, made once and kept (the Guard's and the hush's pattern): SEMan clones what it is handed, and an
        /// unreferenced ScriptableObject made per apply would never be collected.
        /// </summary>
        private static SE_Stats TideBorne()
        {
            if (_tideBorne != null) return _tideBorne;
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = TideName;
            se.m_name = "Tide-borne";
            se.m_tooltip = "The water never tires you.";
            se.m_ttl = 0f;
            // Player.OnSwimming asks the SEMan to modify its stamina use; -1 is minus a hundred percent. (Her regen did
            // nothing in water: Player.UpdateStats zeroes it while swimming - research 2026-10-08, section 7.)
            se.m_swimStaminaUseModifier = -1f;
            _tideBorne = se;
            return se;
        }

        private static bool _tideBorneLogged;

        /// <summary>The Sæfari's passive: the water never tires her (idempotent; re-run on respawn).</summary>
        private void ApplyTideBorne()
        {
            try
            {
                var seman = Player.m_localPlayer?.GetSEMan();
                if (seman == null || seman.HaveStatusEffect(TideHash)) return;
                seman.AddStatusEffect(TideBorne());
                if (!_tideBorneLogged)
                {
                    _tideBorneLogged = true;
                    Debug.Log($"[ICanShowYouTheWorld] Tide-borne on: SE '{TideName}' ({seman.HaveStatusEffect(TideHash)}).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Tide-borne failed: {ex.Message}");
            }
        }

        private static void UnapplyTideBorne()
        {
            try { Player.m_localPlayer?.GetSEMan()?.RemoveStatusEffect(TideHash, quiet: true); }
            catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] Tide-borne removal failed: {ex.Message}"); }
        }

        /// <summary>Undertow's hit: a little blunt, a shove, and a stagger that cannot be refused (the game forces one at 100).</summary>
        private const float UndertowBlunt = 5f;
        private const float UndertowPush = 60f;
        private const float UndertowStagger = 100f;

        /// <summary>When Stormcaller's storm ends (Time.time); SeaWatch reads it through RunService.</summary>
        public float StormUntil { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// Stormcaller: for twenty seconds (thirty after Moder) her Ward strikes every second, twice as far. Nothing is
        /// lent or written to the game - SeaWatch.Pulse reads the end time and does the rest - so the only thing to take
        /// down is the time itself (Unapply, UnapplyAll).
        /// </summary>
        private bool ActivateStormcaller()
        {
            var held = FindHeld("stormcaller");
            if (held == null || held.CooldownRemaining > 0f) return false;
            StormUntil = Time.time + WayRules.StormcallerSeconds(_defeatedBossCount());
            held.CooldownRemaining = held.Def.CooldownSeconds;
            LastActivationMessage = "The storm gathers in your ward.";
            return true;
        }

        /// <summary>
        /// Undertow: one HitData per foe - a push, a forced stagger (multiplier 100) and Wet - like Rend's path, so
        /// it all runs on the owner in one RPC (research 2026-10-08, section 5). Bosses are skipped, as for Warcry: a
        /// boss's animator may have no stagger state - and when only gods are in reach the refusal says so, rather than
        /// claiming nothing is there. A wave that finds nobody refuses, as Rend does. Aboard a ship it bursts from the
        /// player AND from the ship (spec: it also strikes what is in the water around her ship): the union of both
        /// circles, each foe once, thrown back from whichever centre is nearer to it.
        /// </summary>
        private bool ActivateUndertow()
        {
            var held = FindHeld("undertow");
            if (held == null || held.CooldownRemaining > 0f) return false;
            var player = Player.m_localPlayer;
            if (player == null) return false;

            try
            {
                var centres = new List<Vector3> { player.transform.position };
                var ship = Ship.GetLocalShip();
                if (ship != null) centres.Add(ship.transform.position);

                float radius = WayRules.UndertowRadius(_defeatedBossCount());
                var foes = FoesAround(centres, radius, player, skipBosses: true);
                if (foes.Count == 0)
                {
                    LastActivationMessage = FoesAround(centres, radius, player, skipBosses: false).Count > 0
                        ? "The gods stand against the wave."
                        : "Nothing within the wave's reach.";
                    return false;
                }

                float scale = ClassDamageScale();
                foreach (var foe in foes)
                {
                    var hit = new HitData();
                    hit.m_damage.m_blunt = UndertowBlunt * scale;
                    hit.m_pushForce = UndertowPush;
                    hit.m_staggerMultiplier = UndertowStagger;
                    hit.m_statusEffectHash = SEMan.s_statusEffectWet;
                    AimHit(hit, foe.Key, foe.Value, player);
                    DamageOne(foe.Key, hit, "Undertow");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] Undertow failed: " + ex.Message);
                return false;
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// Every hostile within <paramref name="radius"/> of ANY of the centres, once each, paired with the centre nearer
        /// to it - the point a push should run away from. One centre is HostilesNear with its source attached.
        /// </summary>
        private static List<KeyValuePair<Character, Vector3>> FoesAround(IList<Vector3> centres, float radius, Player player, bool skipBosses)
        {
            var found = new List<KeyValuePair<Character, Vector3>>();
            var seen = new HashSet<Character>();
            foreach (var centre in centres)
                foreach (var c in HostilesNear(centre, radius, player, skipBosses))
                {
                    if (!seen.Add(c)) continue;
                    Vector3 nearest = centres[0];
                    float best = float.MaxValue;
                    foreach (var other in centres)
                    {
                        float d = (c.transform.position - other).sqrMagnitude;
                        if (d >= best) continue;
                        best = d;
                        nearest = other;
                    }
                    found.Add(new KeyValuePair<Character, Vector3>(c, nearest));
                }
            return found;
        }

        // --- The Smiðr ---

        private const string ForgeSkinName = "ICSYTW_ForgeSkin";
        private static readonly int ForgeSkinHash = ForgeSkinName.GetStableHashCode();

        /// <summary>One template per armour factor (x1.5, x2), keyed by the factor to 0.01, made once and kept (the War Song's
        /// pattern): SEMan clones what it is handed, and an unreferenced ScriptableObject made per apply would never be
        /// collected.</summary>
        private static readonly Dictionary<int, SE_Stats> ForgeSkinTemplates = new Dictionary<int, SE_Stats>();

        /// <summary>The factor Forge-skin was last laid at, as a template key; -1 when it is not on.</summary>
        private int _forgeSkinKey = -1;

        private readonly List<ZDOID> _watchPosts = new List<ZDOID>();

        /// <summary>The cached template for one armour factor; recreated if Unity has let go of it.</summary>
        private static SE_Stats ForgeSkinTemplate(float factor)
        {
            int key = (int)Math.Round(factor * 100f);
            if (ForgeSkinTemplates.TryGetValue(key, out var cached) && cached != null) return cached;

            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.hideFlags = HideFlags.DontUnloadUnusedAsset;
            se.name = ForgeSkinName;
            se.m_name = "Forge-skin";
            se.m_tooltip = key >= 200
                ? "Your armour is twice as hard, and your gear never wears."
                : "Your armour is half again as hard, and your gear never wears.";
            se.m_ttl = 0f;
            // SE_Stats.ModifyArmorMods: armor = (armor + add) * (1 + mult), for the player only (research 2026-10-08, section 3).
            se.m_armorMultiplier = key / 100f - 1f;
            ForgeSkinTemplates[key] = se;
            return se;
        }

        /// <summary>
        /// Forge-skin: armour x1.5 (x2 after the Queen), the game's own m_armorMultiplier. Idempotent: it is re-laid
        /// (removed, then added) only when the factor has changed or the effect is missing - a death takes it, and the
        /// respawn's Apply puts it back.
        /// </summary>
        private void ApplyForgeSkin()
        {
            try
            {
                var seman = Player.m_localPlayer?.GetSEMan();
                if (seman == null) return;
                float f = WayRules.ArmourFactor(_defeatedBossCount());
                int key = (int)Math.Round(f * 100f);
                if (_forgeSkinKey == key && seman.HaveStatusEffect(ForgeSkinHash)) return;
                seman.RemoveStatusEffect(ForgeSkinHash, quiet: true);
                seman.AddStatusEffect(ForgeSkinTemplate(f));
                _forgeSkinKey = key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Forge-skin failed: {ex.Message}");
            }
        }

        private void UnapplyForgeSkin()
        {
            _forgeSkinKey = -1;
            try { Player.m_localPlayer?.GetSEMan()?.RemoveStatusEffect(ForgeSkinHash, quiet: true); }
            catch (Exception ex) { Debug.LogWarning($"[ICanShowYouTheWorld] Forge-skin removal failed: {ex.Message}"); }
        }

        /// <summary>Once a second while "craftsman" is held: armour current, gear whole, walls within 20 m unworn.</summary>
        private void TickSmith(int gods)
        {
            if (!Holds("craftsman")) return;
            ApplyForgeSkin();
            var player = Player.m_localPlayer;
            if (player == null) return;

            // His gear never wears: the item INSTANCE's durability, never m_shared (that one is every copy's).
            var equipped = player.GetInventory()?.GetEquippedItems();
            if (equipped != null)
                foreach (var item in equipped)
                {
                    if (item == null || item.m_shared == null || !item.m_shared.m_useDurability) continue;
                    float max = item.GetMaxDurability();
                    if (item.m_durability < max) item.m_durability = max;
                }

            ReinforceAround(player.transform.position);
        }

        /// <summary>
        /// Watch-post: raises a ballista where the Smiðr stands - loaded for good, blind to players and tames, never saved.
        /// Raised the way the Field Forge's stations are (RaiseStation): Instantiate, a non-persistent ZDO, owned, tracked by
        /// ZDOID, so it can never be saved into the world and is taken down through the ZDO even from an unloaded zone.
        /// Per instance, never the prefab:
        ///
        ///  - Turret.m_targetPlayers and m_targetTamed go false (both default true): the turret has no faction check, so
        ///    without this it would shoot him and his pack. It still shoots every awake untamed non-player it can see, deer
        ///    included (research 2026-10-08, section 4). Its bolts have no owner, so a stray one can hit anyone (vanilla).
        ///  - m_maxAmmo = 0 with m_defaultAmmo set is the game's infinite ammunition; m_returnAmmoOnDestroy goes false.
        ///  - Piece.m_canBeRemoved goes false and m_resources is emptied, so breaking it drops nothing.
        ///
        /// At most WayRules.WatchPosts stand at once: raising another takes the oldest down.
        /// </summary>
        private bool ActivateWatchPost()
        {
            var held = FindHeld("watchpost");
            if (held == null || held.CooldownRemaining > 0f) return false;
            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            var prefab = scene != null ? scene.GetPrefab("piece_turret") : null;
            int gods = _defeatedBossCount();
            var bolt = scene != null ? scene.GetPrefab(WayRules.WatchPostBolt(gods))?.GetComponent<ItemDrop>() : null;
            if (player == null || prefab == null || bolt == null) { LastActivationMessage = "No ballista answers."; return false; }

            // Footing first, before anything is spent or raised. A ballista is a static piece with nothing under it but the
            // ground height the game finds: raised at sea (aboard a ship, or over open water) it sank to the seabed and
            // shot at nothing. So no ship, no open water, and ground that is really there - no cooldown spent on a refusal.
            // GetSolidHeight raises the point by its last argument and casts straight down to the first solid it meets. From
            // 2 m above his feet, not 5: inside a hall with a roof lower than that, the ray met the roof first and the
            // ballista stood on it. The price: a slope rising more than 2 m in three paces is out of the ray's reach.
            Vector3 at = player.transform.position + player.transform.forward * 3f;
            float ground = 0f;
            if (Ship.GetLocalShip() != null || SeaWatch.OverOpenWater(at) ||
                ZoneSystem.instance == null || !ZoneSystem.instance.GetSolidHeight(at, out ground, 2))
            {
                LastActivationMessage = "No footing for a ballista here.";
                return false;
            }
            at.y = ground;

            var man = ZDOMan.instance;
            _watchPosts.RemoveAll(id => man == null || man.GetZDO(id) == null);

            var inst = UnityEngine.Object.Instantiate(prefab, at, Quaternion.LookRotation(player.transform.forward));
            if (inst == null) { LastActivationMessage = "The watch-post would not stand."; return false; }
            var view = inst.GetComponent<ZNetView>();
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo == null)
            {
                // Untrackable without a ZDO, and so impossible to take down - never leave one.
                UnityEngine.Object.Destroy(inst);
                LastActivationMessage = "The watch-post would not stand.";
                return false;
            }
            zdo.Persistent = false;
            if (!view.IsOwner()) view.ClaimOwnership();

            var piece = inst.GetComponent<Piece>();
            if (piece != null) { piece.m_canBeRemoved = false; piece.m_resources = new Piece.Requirement[0]; }
            var turret = inst.GetComponent<Turret>();
            if (turret != null)
            {
                turret.m_targetPlayers = false;
                turret.m_targetTamed = false;
                turret.m_maxAmmo = 0;
                turret.m_defaultAmmo = bolt;
                turret.m_returnAmmoOnDestroy = false;
            }
            _watchPosts.Add(zdo.m_uid);

            // The new one first, then the oldest down: a failed raising leaves the old one standing.
            while (_watchPosts.Count > WayRules.WatchPosts(gods))
            {
                DestroyByZdo(_watchPosts[0]);
                _watchPosts.RemoveAt(0);
            }

            held.CooldownRemaining = held.Def.CooldownSeconds;
            LastActivationMessage = "The watch-post stands.";
            return true;
        }

        private void TakeDownWatchPosts()
        {
            foreach (var id in _watchPosts.ToList())
            {
                try { DestroyByZdo(id); }
                catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] Watch-post cleanup: " + ex.Message); }
            }
            _watchPosts.Clear();
        }
    }
}
