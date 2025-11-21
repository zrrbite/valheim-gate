using System;
using System.Collections.Generic;
using UnityEngine;
using ICanShowYouTheWorld.Core;
using ICanShowYouTheWorld.GameAPI;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Implementation of combat service.
    /// Handles god mode, AoE damage/healing, defensive abilities, and damage scaling.
    /// </summary>
    public class CombatService : ICombatService
    {
        private readonly IGameAPI _game;
        private readonly IConfiguration _config;

        // State
        private bool _godMode;
        private bool _autoDodgeActive;
        private bool _knockbackAoEActive;
        private bool _barrierAoEActive;
        private bool _freezeMonstersActive;
        private bool _immunityActive;
        private int _damageCounter;

        // AoE Power
        private float _aoePower = 25f;
        private const float MinAoePower = 5f;
        private const float MaxAoePower = 200f;

        // Auto-dodge
        private const float DodgeDistance = 3f;
        private const float DodgeCooldown = 1f;
        private float _lastDodgeTime = -999f;

        // Tunables
        private float _knockbackRadius = 10f;
        private float _knockbackStrength = 300f;
        private float _barrierRadius = 10f;
        private float _barrierStrength = 5f;
        private float _healThreshold = 0.85f;
        private float _critThreshold = 0.10f;
        private float _critMultiplier = 2f;

        // Panic jump
        private const float PanicJumpDuration = 1.25f;
        private const float PanicJumpCooldown = 2.0f;
        private const float PanicJumpStaminaUse = 0f;
        private const float PanicJumpForceBase = 10f;
        private const float PanicJumpForcePerAoe = 0.25f;
        private const float PanicJumpForwardBase = 3f;
        private const float PanicJumpForwardPerAoe = 0.15f;
        private static readonly Vector2 PanicJumpForceClamp = new Vector2(12f, 45f);
        private static readonly Vector2 PanicJumpForwardClamp = new Vector2(0f, 20f);
        private float _panicJumpCdUntil;

        public CombatService(IGameAPI game, IConfiguration config)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // --- God Mode ---
        public bool GodMode => _godMode;

        public void ToggleGodMode()
        {
            _godMode = !_godMode;

            if (!_game.IsLocalPlayerValid)
            {
                return;
            }

            var player = _game.LocalPlayer;
            player.SetGodMode(_godMode);
            player.SetNoPlacementCost(_godMode);
            player.m_guardianPowerCooldown = 1f;

            // Set all food to 30 mins
            foreach (var food in player.GetFoods())
            {
                food.m_time = 25 * 60;
            }

            _game.ShowMessage($"God Mode {(_godMode ? "ON" : "OFF")}", MessageType.TopLeft);
        }

        public bool RequireGodMode(string ability)
        {
            if (!_godMode)
            {
                _game.ShowMessage($"{ability} requires God Mode", MessageType.TopLeft);
                return false;
            }
            return true;
        }

        // --- AoE Power System ---
        public float AoePower => _aoePower;

        public void IncreaseAoePower()
        {
            _aoePower = Mathf.Min(MaxAoePower, _aoePower + 5f);
            _game.ShowMessage($"AOE Power: {_aoePower:0}", MessageType.TopLeft);
        }

        public void DecreaseAoePower()
        {
            _aoePower = Mathf.Max(MinAoePower, _aoePower - 5f);
            _game.ShowMessage($"AOE Power: {_aoePower:0}", MessageType.TopLeft);
        }

        // --- AoE Healing ---
        public void AoeRegen(float radius)
        {
            if (!_game.IsLocalPlayerValid)
            {
                return;
            }

            var list = new List<Character>();
            Character.GetCharactersInRange(
                _game.LocalPlayer.transform.position,
                radius,
                list
            );

            int healedCount = 0;
            int critHealedCount = 0;

            foreach (var c in list)
            {
                if (!c.IsPlayer() && !c.IsTamed()) continue;

                float hpPct = c.GetHealthPercentage();

                // Only heal those below the HealThreshold
                if (hpPct < _healThreshold)
                {
                    // Choose normal vs. crit heal
                    float amount = (hpPct <= _critThreshold)
                        ? _aoePower * _critMultiplier
                        : _aoePower;

                    c.Heal(amount, false);
                    healedCount++;

                    if (hpPct <= _critThreshold)
                        critHealedCount++;
                }
            }

            if (healedCount > 0)
            {
                if (critHealedCount > 0)
                    _game.ShowMessage($"AoE Regen: {healedCount} healed (incl. {critHealedCount} critical for {_aoePower * _critMultiplier:0} HP)", MessageType.TopLeft);
                else
                    _game.ShowMessage($"AoE Regen: {healedCount} healed for {_aoePower:0} HP", MessageType.TopLeft);
            }
        }

        public void CastHealAOE()
        {
            if (!RequireGodMode("AoE heal")) return;
            if (!_game.IsLocalPlayerValid) return;

            var prefab = ZNetScene.instance.GetPrefab("DvergerStaffHeal_aoe");
            if (prefab == null)
            {
                _game.ShowMessage("Missing heal prefab", MessageType.TopLeft);
                return;
            }

            UnityEngine.Object.Instantiate(
                prefab,
                _game.LocalPlayer.transform.position + Vector3.up,
                Quaternion.identity
            );
            _game.ShowMessage("Heal AOE cast", MessageType.TopLeft);
        }

        // --- AoE Damage ---
        public void CastDmgAOE()
        {
            if (!_game.IsLocalPlayerValid) return;

            GameObject prefab = ZNetScene.instance.GetPrefab("aoe_nova");

            if (!prefab)
            {
                _game.ShowMessage("Missing object aoe_nova", MessageType.TopLeft);
            }
            else
            {
                Vector3 vector = UnityEngine.Random.insideUnitSphere;
                Vector3 spawnPos = _game.LocalPlayer.transform.position +
                    _game.LocalPlayer.transform.forward * 4f + Vector3.up + vector;

                _game.ShowMessage("Spawning Nova!", MessageType.TopLeft);
                UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            }
        }

        public void KillAllMonsters()
        {
            if (!RequireGodMode("Kill all monsters")) return;
            if (!_game.IsLocalPlayerValid) return;

            var list = new List<Character>();
            Character.GetCharactersInRange(_game.LocalPlayer.transform.position, 30f, list);

            int killed = 0;
            foreach (var c in list)
            {
                if (!c.IsPlayer() && !c.IsTamed())
                {
                    c.Damage(new HitData { m_damage = { m_damage = 1e10f } });
                    killed++;
                }
            }

            _game.ShowMessage($"Monsters killed: {killed}", MessageType.TopLeft);
        }

        // --- Defensive Abilities ---
        public bool AutoDodgeActive => _autoDodgeActive;

        public void ToggleAutoDodge()
        {
            if (!_game.IsLocalPlayerValid) return;

            _autoDodgeActive = !_autoDodgeActive;
            var player = _game.LocalPlayer;

            if (_autoDodgeActive)
                player.m_onDamaged += HandleAutoDodge;
            else
                player.m_onDamaged -= HandleAutoDodge;

            _game.ShowMessage($"Auto-Dodge {(_autoDodgeActive ? "ON" : "OFF")}", MessageType.TopLeft);
        }

        private void HandleAutoDodge(float damage, Character attacker)
        {
            if (!_autoDodgeActive) return;
            if (!_game.IsLocalPlayerValid) return;

            // Cooldown
            if (Time.time - _lastDodgeTime < DodgeCooldown) return;

            var player = _game.LocalPlayer;
            // Direction from attacker to player
            Vector3 dir = (player.transform.position - attacker.transform.position).normalized;

            // Pick a perpendicular vector (left or right randomly)
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
            if (UnityEngine.Random.value > 0.5f) perp = -perp;

            // Target dodge position
            Vector3 target = player.transform.position + perp * DodgeDistance;

            // Project to ground so you don't end up in mid-air
            if (Physics.Raycast(target + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                target.y = hit.point.y + 0.1f;

            // Teleport the player
            player.TeleportTo(target, player.transform.rotation, distantTeleport: true);

            _lastDodgeTime = Time.time;
        }

        public bool KnockbackAoEActive => _knockbackAoEActive;

        public void ToggleKnockbackAoE()
        {
            _knockbackAoEActive = !_knockbackAoEActive;
            _game.ShowMessage($"AoE Knockback {(_knockbackAoEActive ? "ENABLED" : "DISABLED")}", MessageType.TopLeft);
        }

        public bool BarrierAoEActive => _barrierAoEActive;

        public void ToggleBarrierAoE()
        {
            _barrierAoEActive = !_barrierAoEActive;

            // Note: CheatVisualizer will need to be refactored separately
            // For now, we'll just toggle state
            // CheatVisualizer.ToggleBarrierRing(_barrierRadius);

            _game.ShowMessage($"Barrier AoE {(_barrierAoEActive ? "ON" : "OFF")} (r={_barrierRadius:0}, s={_barrierStrength:0})", MessageType.TopLeft);
        }

        public void ToggleImmunity()
        {
            if (!RequireGodMode("Immunity")) return;
            if (!_game.IsLocalPlayerValid) return;

            var m = _game.LocalPlayer.m_damageModifiers;

            if (!_immunityActive)
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

            _immunityActive = !_immunityActive;
        }

        // --- Status Effects ---
        public bool FreezeMonstersActive => _freezeMonstersActive;

        public void ToggleFreezeMonsters()
        {
            if (!_game.IsLocalPlayerValid) return;

            _freezeMonstersActive = !_freezeMonstersActive;

            // Grab everyone in 50m
            var list = new List<Character>();
            Character.GetCharactersInRange(
                _game.LocalPlayer.transform.position,
                50f,
                list
            );

            int affected = 0;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;

                // 0f = freeze, 1f = normal
                c.m_speed = _freezeMonstersActive ? 0f : 1f;
                affected++;
            }

            _game.ShowMessage($"Monster Freeze {(_freezeMonstersActive ? "ON" : "OFF")} ({affected} {(_freezeMonstersActive ? "frozen" : "unfrozen")})", MessageType.TopLeft);
        }

        // --- Special Abilities ---
        public void PanicJumpBuff()
        {
            if (!_game.IsLocalPlayerValid) return;

            var p = _game.LocalPlayer;

            if (Time.time < _panicJumpCdUntil)
            {
                float left = _panicJumpCdUntil - Time.time;
                p.Message(MessageHud.MessageType.TopLeft, $"Panic jump CD: {left:0.0}s");
                return;
            }

            // Derive forces from current AoePower
            float up = Mathf.Clamp(
                PanicJumpForceBase + (_aoePower * PanicJumpForcePerAoe),
                PanicJumpForceClamp.x,
                PanicJumpForceClamp.y
            );
            float fwd = Mathf.Clamp(
                PanicJumpForwardBase + (_aoePower * PanicJumpForwardPerAoe),
                PanicJumpForwardClamp.x,
                PanicJumpForwardClamp.y
            );

            // Capture current values
            float jForce = p.m_jumpForce;
            float jForward = p.m_jumpForceForward;
            float jCost = p.m_jumpStaminaUsage;

            // Apply buff
            p.m_jumpForce = up;
            p.m_jumpForceForward = fwd;
            p.m_jumpStaminaUsage = PanicJumpStaminaUse;

            p.Message(MessageHud.MessageType.TopLeft, $"Panic jump ready — press Space!  (↑{up:0.0} →{fwd:0.0})");
            Console.instance?.Print($"[Cheat] Panic jump buff: up={up:0.0}, fwd={fwd:0.0}, AoePower={_aoePower}");

            // Auto-restore
            p.StartCoroutine(RestoreJumpAfter(PanicJumpDuration, p, jForce, jForward, jCost));

            _panicJumpCdUntil = Time.time + PanicJumpCooldown;
        }

        private static System.Collections.IEnumerator RestoreJumpAfter(float delay, Player p, float jForce, float jForward, float jCost)
        {
            yield return new WaitForSeconds(delay);
            if (!p) yield break;

            p.m_jumpForce = jForce;
            p.m_jumpForceForward = jForward;
            p.m_jumpStaminaUsage = jCost;

            p.Message(MessageHud.MessageType.TopLeft, "Panic jump ended");
        }

        // --- Weapon Damage Scaling ---
        public int DamageCounter => _damageCounter;

        public void IncreaseDamageCounter()
        {
            _damageCounter++;
            _game.ShowMessage($"Damage Counter: {_damageCounter}", MessageType.TopLeft);
            ApplySuperWeapon();
        }

        public void DecreaseDamageCounter()
        {
            _damageCounter--;
            _game.ShowMessage($"Damage Counter: {_damageCounter}", MessageType.TopLeft);
            ApplySuperWeapon();
        }

        // --- Movement Speed ---
        public float CurrentRunSpeed
        {
            get
            {
                if (!_game.IsLocalPlayerValid) return 0f;
                return _game.LocalPlayer.m_runSpeed;
            }
        }

        public void SpeedUp()
        {
            if (!_game.IsLocalPlayerValid) return;
            SetSpeed(_game.LocalPlayer.m_runSpeed + _config.SpeedIncrement);
        }

        public void SpeedDown()
        {
            if (!_game.IsLocalPlayerValid) return;
            SetSpeed(_game.LocalPlayer.m_runSpeed - _config.SpeedIncrement);
        }

        public void SetSpeed(float speed)
        {
            if (!_game.IsLocalPlayerValid) return;

            var p = _game.LocalPlayer;
            p.m_runSpeed = p.m_walkSpeed = speed;
            p.m_jumpForce = speed;
            p.m_jumpForceForward = speed;

            _game.ShowMessage($"Speed: {speed:0.0}", MessageType.TopLeft);
        }

        private void ApplySuperWeapon()
        {
            if (!_game.IsLocalPlayerValid) return;

            // Apply current damage counter to equipped weapons
            foreach (var item in _game.LocalPlayer.GetInventory().GetEquippedItems())
            {
                if (!item.IsWeapon()) continue;

                // Get base damage types
                var baseDamages = item.GetDamage();
                int dmg = _damageCounter * 10;

                var updated = new HitData.DamageTypes
                {
                    m_slash = baseDamages.m_slash > 0 ? dmg : 0,
                    m_blunt = baseDamages.m_blunt > 0 ? dmg : 0,
                    m_pierce = baseDamages.m_pierce > 0 ? dmg : 0,
                    m_frost = baseDamages.m_frost > 0 ? dmg : 0,
                    m_lightning = baseDamages.m_lightning > 0 ? dmg : 0,
                    m_poison = baseDamages.m_poison > 0 ? dmg : 0,
                    m_spirit = baseDamages.m_spirit > 0 ? dmg : 0,
                    m_chop = baseDamages.m_chop > 0 ? dmg : 0,
                    m_pickaxe = baseDamages.m_pickaxe > 0 ? dmg : 0,
                    m_damage = baseDamages.m_damage > 0 ? dmg : 0,
                };

                item.m_shared.m_damages = updated;
            }

            _game.ShowMessage($"Applied {_damageCounter} damage counters to weapons", MessageType.TopLeft);
        }

        // --- Tunables ---
        public float KnockbackRadius
        {
            get => _knockbackRadius;
            set => _knockbackRadius = value;
        }

        public float KnockbackStrength
        {
            get => _knockbackStrength;
            set => _knockbackStrength = value;
        }

        public float BarrierRadius
        {
            get => _barrierRadius;
            set => _barrierRadius = value;
        }

        public float BarrierStrength
        {
            get => _barrierStrength;
            set => _barrierStrength = value;
        }

        public float HealThreshold
        {
            get => _healThreshold;
            set => _healThreshold = value;
        }

        public float CritThreshold
        {
            get => _critThreshold;
            set => _critThreshold = value;
        }

        public float CritMultiplier
        {
            get => _critMultiplier;
            set => _critMultiplier = value;
        }
    }
}
