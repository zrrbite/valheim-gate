using System;
using System.Collections.Generic;
using UnityEngine;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Implementation of pet management service.
    /// Handles buffing, damage management, and health modifications for tamed creatures.
    /// </summary>
    public class PetService : IPetService
    {
        private readonly IGameAPI _game;
        private readonly IConfiguration _config;
        private readonly ICombatService _combat;

        // Cached original weapon damage for restoring
        private readonly Dictionary<string, HitData.DamageTypes> _originalWeaponDamage =
            new Dictionary<string, HitData.DamageTypes>();

        // Group baseline for damage scaling
        private HitData.DamageTypes _groupBaseline;
        private bool _groupBaselineValid = false;

        // Constants
        private const float PET_RADIUS = 10f;
        private const float GROUP_MULT = 1.2f;

        public PetService(IGameAPI game, IConfiguration config, ICombatService combat)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public float PetRadius => PET_RADIUS;
        public float PetDamageMultiplier => GROUP_MULT;

        public void BuffAllPets(bool incrLevel = false)
        {
            if (!_combat.RequireGodMode("Buff tamed")) return;
            if (!_game.IsLocalPlayerValid) return;

            ComputeGroupBaseline();
            if (!_groupBaselineValid)
            {
                _game.ShowMessage("No baseline, nothing buffed", MessageType.TopLeft);
                return;
            }

            var boosted = ScaleDamage(_groupBaseline, GROUP_MULT);

            var list = new List<Character>();
            Character.GetCharactersInRange(_game.LocalPlayer.transform.position, PET_RADIUS, list);

            int touched = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var ch = list[i];
                if (ch == null) continue;
                if (!ch.IsTamed() || ch.IsPlayer()) continue;

                // HP buff
                ch.SetMaxHealth(5000f);
                ch.Heal(ch.GetMaxHealth(), false);

                // Only affects humanoid-style pets (skeletons, dvergr support mages, etc.)
                Humanoid hum = ch as Humanoid;
                if (hum != null)
                {
                    var inv = hum.GetInventory();
                    if (inv != null)
                    {
                        var eq = inv.GetEquippedItems();
                        if (eq != null)
                        {
                            for (int w = 0; w < eq.Count; w++)
                            {
                                var it = eq[w];
                                if (!it.IsWeapon()) continue;

                                // Apply boosted damage
                                it.m_shared.m_damages = CopyDamage(boosted);
                                it.m_durability = it.m_shared.m_maxDurability;

                                // Debug per weapon
                                string afterDbg = DebugDamageString(it.m_shared.m_damages);
                                Console.instance?.Print($"[PetBuff] Applied to {ch.GetHoverName()} weapon {it.m_shared.m_name}: {afterDbg}");
                            }
                        }
                    }
                }

                if (incrLevel)
                    ch.SetLevel(2);

                touched++;
            }

            _game.ShowMessage($"Buffed {touched} pets (x{GROUP_MULT:0.0})", MessageType.TopLeft);
        }

        public void SetBaselinePetDmg()
        {
            if (!_combat.RequireGodMode("Reset pet dmg")) return;
            if (!_game.IsLocalPlayerValid) return;

            // Fixed stats you want pets to have after reset
            const float FIXED_BLUNT = 20f;
            const float FIXED_SLASH = 20f;
            const float FIXED_PIERCE = 20f;
            const float FIXED_FROST = 0f;
            const float FIXED_FIRE = 0f;
            const float FIXED_GENERIC = 0f;
            const float FIXED_CHOP = 0f;
            const float FIXED_PICK = 0f;

            int changed = 0;

            var list = new List<Character>();
            Character.GetCharactersInRange(_game.LocalPlayer.transform.position, PET_RADIUS, list);

            foreach (Character ch in list)
            {
                // Only touch tamed non-player followers
                if (ch == null) continue;
                if (!ch.IsTamed() || ch.IsPlayer()) continue;

                // We only support humanoid-style followers here (skeletons, dvergr mages, etc.)
                Humanoid h = ch as Humanoid;
                if (h == null) continue;

                var inv = h.GetInventory();
                if (inv == null) continue;

                var eq = inv.GetEquippedItems();
                if (eq == null) continue;

                foreach (var it in eq)
                {
                    if (!it.IsWeapon()) continue;

                    // Slam damage to fixed template
                    var fixedDmg = new HitData.DamageTypes
                    {
                        m_blunt = FIXED_BLUNT,
                        m_slash = FIXED_SLASH,
                        m_pierce = FIXED_PIERCE,
                        m_damage = FIXED_GENERIC,
                        m_chop = FIXED_CHOP,
                        m_pickaxe = FIXED_PICK,
                        // Elemental
                        m_fire = 0f,
                        m_frost = FIXED_FROST,
                        m_lightning = FIXED_FIRE,
                        m_poison = 0f,
                        m_spirit = 0f
                    };

                    it.m_shared.m_damages = fixedDmg;
                    it.m_durability = it.m_shared.m_maxDurability;
                    changed++;
                }
            }

            _game.ShowMessage($"Reset pet dmg on {changed} weapons to fixed template", MessageType.TopLeft);
        }

        public void ResetPetDmg()
        {
            if (!_combat.RequireGodMode("Reset pet dmg")) return;
            if (!_game.IsLocalPlayerValid) return;

            const float FIXED_GENERIC = 100f;  // If weapon uses only m_damage
            const float FIXED_TYPED = 50f;     // If weapon uses typed channels

            int changed = 0;
            var list = new List<Character>();
            Character.GetCharactersInRange(_game.LocalPlayer.transform.position, PET_RADIUS, list);

            foreach (var ch in list)
            {
                if (ch == null || ch.IsPlayer() || !ch.IsTamed()) continue;

                var h = ch as Humanoid;
                if (h == null) continue;

                var eq = h.GetInventory()?.GetEquippedItems();
                if (eq == null) continue;

                foreach (var it in eq)
                {
                    if (!it.IsWeapon()) continue;

                    var baseD = it.m_shared.m_damages;

                    bool usesGeneric = baseD.m_damage > 0f;
                    bool usesTyped =
                        baseD.m_blunt > 0f || baseD.m_slash > 0f || baseD.m_pierce > 0f ||
                        baseD.m_fire > 0f || baseD.m_frost > 0f || baseD.m_lightning > 0f ||
                        baseD.m_poison > 0f || baseD.m_spirit > 0f || baseD.m_chop > 0f ||
                        baseD.m_pickaxe > 0f;

                    var upd = baseD; // Start from what it already uses

                    if (usesGeneric)
                    {
                        // Weapon relies on generic damage only
                        upd.m_damage = FIXED_GENERIC;
                        // Don't zero the typed channels; just leave as-is to avoid surprises
                    }
                    else if (usesTyped)
                    {
                        // Normalize only the channels it actually had
                        if (baseD.m_blunt > 0f) upd.m_blunt = FIXED_TYPED;
                        if (baseD.m_slash > 0f) upd.m_slash = FIXED_TYPED;
                        if (baseD.m_pierce > 0f) upd.m_pierce = FIXED_TYPED;
                        if (baseD.m_fire > 0f) upd.m_fire = FIXED_TYPED;
                        if (baseD.m_frost > 0f) upd.m_frost = FIXED_TYPED;
                        if (baseD.m_lightning > 0f) upd.m_lightning = FIXED_TYPED;
                        if (baseD.m_poison > 0f) upd.m_poison = FIXED_TYPED;
                        if (baseD.m_spirit > 0f) upd.m_spirit = FIXED_TYPED;
                        if (baseD.m_chop > 0f) upd.m_chop = FIXED_TYPED;
                        if (baseD.m_pickaxe > 0f) upd.m_pickaxe = FIXED_TYPED;
                    }
                    else
                    {
                        // Edge case: everything is 0 (projectile/ability weapon).
                        // Leave it alone to avoid forcing 0
                        continue;
                    }

                    it.m_shared.m_damages = upd;
                    it.m_durability = it.m_shared.m_maxDurability;
                    changed++;
                }
            }

            _game.ShowMessage($"Reset pet dmg on {changed} weapons (preserved active channels)", MessageType.TopLeft);
        }

        public void ResetPetBuffs()
        {
            if (!_game.IsLocalPlayerValid) return;

            var list = new List<Character>();
            Character.GetCharactersInRange(_game.LocalPlayer.transform.position, PET_RADIUS, list);

            int touched = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var ch = list[i];
                if (ch == null) continue;
                if (!ch.IsTamed() || ch.IsPlayer()) continue;

                Humanoid hum = ch as Humanoid;
                if (hum != null)
                {
                    var inv = hum.GetInventory();
                    if (inv != null)
                    {
                        var eq = inv.GetEquippedItems();
                        if (eq != null)
                        {
                            for (int w = 0; w < eq.Count; w++)
                            {
                                var it = eq[w];
                                if (!it.IsWeapon()) continue;

                                string wName = it.m_shared.m_name;

                                HitData.DamageTypes orig;
                                if (_originalWeaponDamage.TryGetValue(wName, out orig))
                                {
                                    it.m_shared.m_damages = CopyDamage(orig);
                                    it.m_durability = it.m_shared.m_maxDurability;

                                    string dbg = DebugDamageString(orig);
                                    Console.instance?.Print($"[PetBuff] Reset {ch.GetHoverName()} weapon {it.m_shared.m_name} to {dbg}");
                                }
                            }
                        }
                    }
                }

                touched++;
            }

            _game.ShowMessage($"Reset {touched} pet buffs", MessageType.TopLeft);
        }

        // --- Private Helper Methods (extracted from DamageHelpers) ---

        private void ComputeGroupBaseline()
        {
            if (!_game.IsLocalPlayerValid) return;

            _groupBaselineValid = false;
            float bestScore = -1f;

            var list = new List<Character>();
            Character.GetCharactersInRange(_game.LocalPlayer.transform.position, PET_RADIUS, list);

            for (int i = 0; i < list.Count; i++)
            {
                var ch = list[i];
                if (ch == null) continue;
                if (!ch.IsTamed() || ch.IsPlayer()) continue;

                var hum = ch as Humanoid;
                if (hum == null) continue;

                var inv = hum.GetInventory();
                if (inv == null) continue;

                var eq = inv.GetEquippedItems();
                if (eq == null) continue;

                for (int w = 0; w < eq.Count; w++)
                {
                    var it = eq[w];
                    if (!it.IsWeapon()) continue;

                    string wName = it.m_shared.m_name;

                    // Capture original prefab stats once
                    if (!_originalWeaponDamage.ContainsKey(wName))
                    {
                        _originalWeaponDamage[wName] = CopyDamage(it.m_shared.m_damages);
                    }

                    var baseDmg = _originalWeaponDamage[wName];
                    float score = ScoreDamage(baseDmg);

                    if (!_groupBaselineValid)
                    {
                        _groupBaseline = CopyDamage(baseDmg);
                        bestScore = score;
                        _groupBaselineValid = true;
                    }
                    else
                    {
                        if (score < bestScore)
                        {
                            _groupBaseline = CopyDamage(baseDmg);
                            bestScore = score;
                        }
                    }
                }
            }

            if (_groupBaselineValid)
            {
                // Debug print baseline
                string dbg = DebugDamageString(_groupBaseline);
                Console.instance?.Print($"[PetBuff] Baseline score={bestScore:0.0} dmg={dbg}");
                _game.ShowMessage($"Pet baseline dmg set: {dbg}", MessageType.TopLeft);
            }
            else
            {
                _game.ShowMessage("No pet weapons found for baseline", MessageType.TopLeft);
            }
        }

        private HitData.DamageTypes CopyDamage(HitData.DamageTypes src)
        {
            var d = new HitData.DamageTypes();
            d.m_blunt = src.m_blunt;
            d.m_slash = src.m_slash;
            d.m_pierce = src.m_pierce;
            d.m_fire = src.m_fire;
            d.m_frost = src.m_frost;
            d.m_lightning = src.m_lightning;
            d.m_poison = src.m_poison;
            d.m_spirit = src.m_spirit;
            d.m_chop = src.m_chop;
            d.m_pickaxe = src.m_pickaxe;
            d.m_damage = src.m_damage;
            return d;
        }

        private float ScoreDamage(HitData.DamageTypes d)
        {
            // Keep score simple/physical
            return d.m_blunt
                 + d.m_slash
                 + d.m_pierce
                 + d.m_damage
                 + d.m_chop
                 + d.m_pickaxe;
        }

        private HitData.DamageTypes ScaleDamage(HitData.DamageTypes d, float mult)
        {
            var o = new HitData.DamageTypes();
            o.m_blunt = d.m_blunt * mult;
            o.m_slash = d.m_slash * mult;
            o.m_pierce = d.m_pierce * mult;
            o.m_fire = d.m_fire * mult;
            o.m_frost = d.m_frost * mult;
            o.m_lightning = d.m_lightning * mult;
            o.m_poison = d.m_poison * mult;
            o.m_spirit = d.m_spirit * mult;
            o.m_chop = d.m_chop * mult;
            o.m_pickaxe = d.m_pickaxe * mult;
            o.m_damage = d.m_damage * mult;
            return o;
        }

        private string DebugDamageString(HitData.DamageTypes d)
        {
            // Compact readable dump of key types
            return $"blunt={d.m_blunt:0.0} slash={d.m_slash:0.0} pierce={d.m_pierce:0.0} " +
                   $"dmg={d.m_damage:0.0} chop={d.m_chop:0.0} pick={d.m_pickaxe:0.0}";
        }
    }
}
