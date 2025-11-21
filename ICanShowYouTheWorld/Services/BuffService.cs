using System;
using System.Collections.Generic;
using UnityEngine;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Implementation of buff service.
    /// Manages player buffs, auras, and periodic effects like Guardian Gift, Renewal, AoE Renewal, Cloak of Flames, and Immunity.
    /// </summary>
    public class BuffService : IBuffService
    {
        private readonly IGameAPI _game;
        private readonly IConfiguration _config;
        private readonly ICombatService _combat;

        // ===== State =====
        public bool RenewalActive { get; private set; }
        public bool AOERenewalActive { get; private set; }
        public bool CloakActive { get; private set; }
        public bool GiftActive { get; private set; }
        public bool ImmunityActive { get; private set; }

        // ===== AoE Power =====
        public float AoePower { get; private set; } = 25f;
        private const float MinAoePower = 5f;
        private const float MaxAoePower = 200f;
        private const float AoeDmgScale = 1.5f;

        // ===== Healing Thresholds =====
        private const float CritThreshold = 0.10f;    // 10% HP for critical heal
        private const float CritMultiplier = 2f;      // 2x heal for critical
        private const float HealThreshold = 0.85f;    // Only heal below 85% HP

        // ===== Guardian Gift Snapshots =====
        private struct ItemSnapshot
        {
            public float durDrain, maxDur, curDur, armor;
        }

        private float origBaseHP;
        private float origBlockStaminaDrain;
        private float origRunStaminaDrain;
        private float origStaminaRegen;
        private float origStaminaRegenDelay;
        private float origEitrRegen;
        private float origEitrRegenDelay;
        private float origMaxCarryWeight;
        private Dictionary<ItemDrop.ItemData, ItemSnapshot> origItemStats;

        // ===== Eitr Weave Detection =====
        private static readonly HashSet<string> EitrTokens = new HashSet<string> {
            "$item_helmet_mage", "$item_chest_mage", "$item_legs_mage"
        };
        private static readonly HashSet<string> EitrPrefabs = new HashSet<string> {
            "HelmetMage", "ArmorMageChest", "ArmorMageLegs"
        };

        // ===== Periodic Timing =====
        private float lastRenewalTime;
        private float lastAoeRenewalTime;
        private float lastCloakTime;
        private const float RenewalInterval = 0.05f;    // ~50ms
        private const float AoeRenewalInterval = 0.06f; // ~60ms
        private const float CloakInterval = 0.075f;     // ~75ms

        public BuffService(IGameAPI game, IConfiguration config, ICombatService combat)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        // ===== Toggle Methods =====

        public void ToggleRenewal()
        {
            RenewalActive = !RenewalActive;
            _game.ShowMessage($"Renewal {(RenewalActive ? "ON" : "OFF")}", MessageType.TopLeft);
        }

        public void ToggleAoeRenewal()
        {
            if (!_combat.RequireGodMode("AoE Renewal")) return;
            AOERenewalActive = !AOERenewalActive;

            // Toggle visual ring
            CheatVisualizer.ToggleConformHeal(10f);

            _game.ShowMessage($"AoE Renewal {(AOERenewalActive ? "ON" : "OFF")}", MessageType.TopLeft);
        }

        public void ToggleCloakOfFlames()
        {
            if (!_combat.RequireGodMode("CoF")) return;
            CloakActive = !CloakActive;

            // Toggle visual ring
            CheatVisualizer.TogglePbaoeRing(5f);

            _game.ShowMessage($"Cloak of Flames {(CloakActive ? "ON" : "OFF")}", MessageType.TopLeft);
        }

        public void ToggleGuardianGift()
        {
            var p = Player.m_localPlayer;
            if (!_combat.RequireGodMode("Guardian's Gift")) return;

            if (!GiftActive)
            {
                // Snapshot player fields we're about to change
                origBaseHP = p.m_baseHP;
                origBlockStaminaDrain = p.m_blockStaminaDrain;
                origRunStaminaDrain = p.m_runStaminaDrain;
                origStaminaRegen = p.m_staminaRegen;
                origStaminaRegenDelay = p.m_staminaRegenDelay;
                origEitrRegen = p.m_eiterRegen;
                origEitrRegenDelay = p.m_eitrRegenDelay;
                origMaxCarryWeight = p.m_maxCarryWeight;

                // Snapshot equipped items
                origItemStats = new Dictionary<ItemDrop.ItemData, ItemSnapshot>();
                foreach (var item in p.GetInventory().GetEquippedItems())
                {
                    origItemStats[item] = new ItemSnapshot
                    {
                        durDrain = item.m_shared.m_durabilityDrain,
                        maxDur = item.m_shared.m_maxDurability,
                        curDur = item.m_durability,
                        armor = item.m_shared.m_armor
                    };
                }

                // Buff stats
                p.m_baseHP = p.m_baseHP + 30f; // Health buff
                p.m_blockStaminaDrain = 0.1f;
                p.m_runStaminaDrain = 0.1f;
                p.m_staminaRegen = 50f;
                p.m_staminaRegenDelay = 0.5f;
                p.m_eiterRegen = 50f;
                p.m_eitrRegenDelay = 0.1f;
                p.m_maxCarryWeight = 99999f;

                // Buff Eitr Weave armor
                foreach (var item in p.GetInventory().GetEquippedItems())
                {
                    if (!IsEitrWeave(item)) continue;

                    item.m_shared.m_durabilityDrain = 0.1f;
                    item.m_shared.m_maxDurability = 10000f;
                    item.m_durability = 10000f;
                    item.m_shared.m_armor = item.m_shared.m_armor + 30f;
                }

                // Buff resistances
                var m = p.m_damageModifiers;
                m.m_blunt = HitData.DamageModifier.SlightlyResistant;
                m.m_slash = HitData.DamageModifier.SlightlyResistant;
                m.m_pierce = HitData.DamageModifier.SlightlyResistant;
                m.m_fire = HitData.DamageModifier.Resistant;
                m.m_frost = HitData.DamageModifier.SlightlyResistant;
                m.m_lightning = HitData.DamageModifier.SlightlyResistant;
                m.m_poison = HitData.DamageModifier.SlightlyResistant;
                m.m_spirit = HitData.DamageModifier.SlightlyResistant;
                m.m_chop = HitData.DamageModifier.SlightlyResistant;
                m.m_pickaxe = HitData.DamageModifier.SlightlyResistant;

                GiftActive = true;
                _game.ShowMessage("Guardian's Gift: ON", MessageType.TopLeft);
            }
            else
            {
                // Revert player stats
                p.m_baseHP = origBaseHP;
                p.m_blockStaminaDrain = origBlockStaminaDrain;
                p.m_runStaminaDrain = origRunStaminaDrain;
                p.m_staminaRegen = origStaminaRegen;
                p.m_staminaRegenDelay = origStaminaRegenDelay;
                p.m_eiterRegen = origEitrRegen;
                p.m_eitrRegenDelay = origEitrRegenDelay;
                p.m_maxCarryWeight = origMaxCarryWeight;

                // Revert items
                foreach (var kv in origItemStats)
                {
                    var item = kv.Key;
                    var snap = kv.Value;

                    item.m_shared.m_maxDurability = snap.maxDur;
                    item.m_durability = snap.curDur;
                    item.m_shared.m_durabilityDrain = snap.durDrain;
                    item.m_shared.m_armor = snap.armor;
                }
                origItemStats = null;

                GiftActive = false;
                _game.ShowMessage("Guardian's Gift: OFF", MessageType.TopLeft);
            }
        }

        public void ToggleImmunity()
        {
            if (!_combat.RequireGodMode("Immunity")) return;
            var m = Player.m_localPlayer.m_damageModifiers;

            if (!ImmunityActive)
            {
                m.m_blunt = HitData.DamageModifier.Immune;
                m.m_slash = HitData.DamageModifier.Immune;
                m.m_pierce = HitData.DamageModifier.Immune;
                m.m_fire = HitData.DamageModifier.Immune;
                m.m_frost = HitData.DamageModifier.Immune;
                m.m_lightning = HitData.DamageModifier.Immune;
                m.m_poison = HitData.DamageModifier.Immune;
                m.m_spirit = HitData.DamageModifier.Immune;
                m.m_chop = HitData.DamageModifier.Immune;
                m.m_pickaxe = HitData.DamageModifier.Immune;
                _game.ShowMessage("Immunity: Godlike!", MessageType.TopLeft);
            }
            else
            {
                m.m_blunt = HitData.DamageModifier.SlightlyResistant;
                m.m_slash = HitData.DamageModifier.SlightlyResistant;
                m.m_pierce = HitData.DamageModifier.SlightlyResistant;
                m.m_fire = HitData.DamageModifier.SlightlyResistant;
                m.m_frost = HitData.DamageModifier.SlightlyResistant;
                m.m_lightning = HitData.DamageModifier.SlightlyResistant;
                m.m_poison = HitData.DamageModifier.SlightlyResistant;
                m.m_spirit = HitData.DamageModifier.SlightlyResistant;
                m.m_chop = HitData.DamageModifier.SlightlyResistant;
                m.m_pickaxe = HitData.DamageModifier.SlightlyResistant;
                _game.ShowMessage("Immunity: Normal", MessageType.TopLeft);
            }

            ImmunityActive = !ImmunityActive;
        }

        // ===== Power Control =====

        public void IncreaseAoePower()
        {
            AoePower = Mathf.Min(MaxAoePower, AoePower + 5f);
            _game.ShowMessage($"AOE Power: {AoePower:0}", MessageType.TopLeft);
        }

        public void DecreaseAoePower()
        {
            AoePower = Mathf.Max(MinAoePower, AoePower - 5f);
            _game.ShowMessage($"AOE Power: {AoePower:0}", MessageType.TopLeft);
        }

        // ===== Periodic Update =====

        public void HandlePeriodic()
        {
            if (!_game.IsLocalPlayerValid) return;

            float time = Time.time;

            // Renewal (HoT)
            if (RenewalActive && time - lastRenewalTime >= RenewalInterval)
            {
                Invigorate();
                lastRenewalTime = time;
            }

            // AoE Renewal
            if (AOERenewalActive && time - lastAoeRenewalTime >= AoeRenewalInterval)
            {
                AoeRegen(10f);
                lastAoeRenewalTime = time;
            }

            // Cloak of Flames
            if (CloakActive && time - lastCloakTime >= CloakInterval)
            {
                DamageAoE(5f);
                lastCloakTime = time;
            }
        }

        // ===== Periodic Actions =====

        private void Invigorate()
        {
            var p = Player.m_localPlayer;
            p.Heal(p.GetMaxHealth() - p.GetHealth(), true);
            p.AddStamina(p.GetMaxStamina());
            p.AddEitr(p.GetMaxEitr() - p.GetEitr());
        }

        private void AoeRegen(float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(
                Player.m_localPlayer.transform.position,
                radius,
                list
            );

            int healedCount = 0;
            int critHealedCount = 0;

            foreach (var c in list)
            {
                if (!c.IsPlayer() && !c.IsTamed()) continue;

                float hpPct = c.GetHealthPercentage();

                // Only heal those below HealThreshold
                if (hpPct < HealThreshold)
                {
                    // Choose normal vs. crit heal
                    float amount = (hpPct <= CritThreshold)
                        ? AoePower * CritMultiplier
                        : AoePower;

                    c.Heal(amount, false);
                    healedCount++;

                    if (hpPct <= CritThreshold)
                        critHealedCount++;
                }
            }

            if (healedCount > 0)
            {
                if (critHealedCount > 0)
                    _game.ShowMessage($"AoE Regen: {healedCount} healed (incl. {critHealedCount} critical for {AoePower * CritMultiplier:0} HP)", MessageType.TopLeft);
                else
                    _game.ShowMessage($"AoE Regen: {healedCount} healed for {AoePower:0} HP", MessageType.TopLeft);
            }
        }

        private void DamageAoE(float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(
                Player.m_localPlayer.transform.position,
                radius,
                list
            );

            float dmg = AoePower * AoeDmgScale;
            foreach (var c in list)
            {
                if (c.IsPlayer() || !c.IsMonsterFaction(10)) continue;
                c.Damage(new HitData { m_damage = { m_damage = dmg } });
                c.GetSEMan()?.AddStatusEffect(new SE_Burning(), resetTime: true, 5, 1);
            }
        }

        // ===== Helper Methods =====

        private static bool IsEitrWeave(ItemDrop.ItemData item)
        {
            if (item == null) return false;

            // Fast path: compare localization tokens
            if (!string.IsNullOrEmpty(item.m_shared.m_name) && EitrTokens.Contains(item.m_shared.m_name))
                return true;

            // Fallback: compare prefab name
            string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            if (!string.IsNullOrEmpty(prefabName) && EitrPrefabs.Contains(prefabName))
                return true;

            return false;
        }
    }
}
