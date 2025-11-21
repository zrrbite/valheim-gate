using UnityEngine;

namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Service for combat-related functionality.
    /// Handles god mode, AoE damage/healing, defensive abilities, and damage scaling.
    /// </summary>
    public interface ICombatService
    {
        // --- God Mode ---
        /// <summary>
        /// Gets whether god mode is currently active.
        /// </summary>
        bool GodMode { get; }

        /// <summary>
        /// Toggle god mode on/off. When enabled: invulnerability, no placement cost, extended food duration.
        /// </summary>
        void ToggleGodMode();

        /// <summary>
        /// Check if god mode is active. If not, shows message to user.
        /// </summary>
        /// <param name="ability">Name of ability requiring god mode.</param>
        /// <returns>True if god mode is active, false otherwise.</returns>
        bool RequireGodMode(string ability);

        // --- AoE Power System ---
        /// <summary>
        /// Base power for AoE abilities (healing, damage, buffs). Range: 5-200.
        /// </summary>
        float AoePower { get; }

        /// <summary>
        /// Increase AoE power by 5 (capped at 200).
        /// </summary>
        void IncreaseAoePower();

        /// <summary>
        /// Decrease AoE power by 5 (minimum 5).
        /// </summary>
        void DecreaseAoePower();

        // --- AoE Healing ---
        /// <summary>
        /// Heal all players and tamed creatures within radius.
        /// Healing amount based on AoePower. Critical heal (2x) for targets below 10% HP.
        /// </summary>
        /// <param name="radius">Search radius for targets to heal.</param>
        void AoeRegen(float radius);

        /// <summary>
        /// Spawn a visual heal AoE effect at player position.
        /// Requires god mode.
        /// </summary>
        void CastHealAOE();

        // --- AoE Damage ---
        /// <summary>
        /// Spawn a damage nova AoE at player position.
        /// Spawns "aoe_nova" prefab 4m in front of player.
        /// </summary>
        void CastDmgAOE();

        /// <summary>
        /// Kill all untamed, non-player characters within 30m radius.
        /// Requires god mode.
        /// </summary>
        void KillAllMonsters();

        // --- Defensive Abilities ---
        /// <summary>
        /// Toggle auto-dodge mode. When active, automatically dodges away from nearby enemies.
        /// </summary>
        void ToggleAutoDodge();

        /// <summary>
        /// Gets whether auto-dodge is currently active.
        /// </summary>
        bool AutoDodgeActive { get; }

        /// <summary>
        /// Toggle knockback AoE. When active, pushes enemies away from player.
        /// </summary>
        void ToggleKnockbackAoE();

        /// <summary>
        /// Gets whether knockback AoE is currently active.
        /// </summary>
        bool KnockbackAoEActive { get; }

        /// <summary>
        /// Toggle barrier AoE. When active, creates a force field that pushes enemies back.
        /// </summary>
        void ToggleBarrierAoE();

        /// <summary>
        /// Gets whether barrier AoE is currently active.
        /// </summary>
        bool BarrierAoEActive { get; }

        /// <summary>
        /// Toggle immunity to all damage and status effects.
        /// </summary>
        void ToggleImmunity();

        // --- Status Effects ---
        /// <summary>
        /// Toggle monster freezing. When active, all nearby monsters are frozen in place.
        /// </summary>
        void ToggleFreezeMonsters();

        /// <summary>
        /// Gets whether monster freezing is currently active.
        /// </summary>
        bool FreezeMonstersActive { get; }

        // --- Special Abilities ---
        /// <summary>
        /// Activate panic jump buff. Player has short window to press Space for super jump.
        /// Jump force scales with AoePower.
        /// </summary>
        void PanicJumpBuff();

        // --- Weapon Damage Scaling ---
        /// <summary>
        /// Current damage multiplier counter.
        /// </summary>
        int DamageCounter { get; }

        /// <summary>
        /// Increase weapon damage multiplier.
        /// </summary>
        void IncreaseDamageCounter();

        /// <summary>
        /// Decrease weapon damage multiplier.
        /// </summary>
        void DecreaseDamageCounter();

        // --- Movement Speed ---
        /// <summary>
        /// Current player run speed.
        /// </summary>
        float CurrentRunSpeed { get; }

        /// <summary>
        /// Increase player run speed by configured increment.
        /// </summary>
        void SpeedUp();

        /// <summary>
        /// Decrease player run speed by configured increment.
        /// </summary>
        void SpeedDown();

        /// <summary>
        /// Set player run speed to specific value.
        /// </summary>
        /// <param name="speed">Target run speed.</param>
        void SetSpeed(float speed);

        // --- Tunables ---
        /// <summary>
        /// Knockback radius for knockback AoE (default: 10f).
        /// </summary>
        float KnockbackRadius { get; set; }

        /// <summary>
        /// Knockback force strength (default: 300f).
        /// </summary>
        float KnockbackStrength { get; set; }

        /// <summary>
        /// Barrier AoE radius (default: 10f).
        /// </summary>
        float BarrierRadius { get; set; }

        /// <summary>
        /// Barrier push strength (default: 5f).
        /// </summary>
        float BarrierStrength { get; set; }

        /// <summary>
        /// Heal threshold percentage (0-1). Only heal targets below this % (default: 0.85 = 85%).
        /// </summary>
        float HealThreshold { get; set; }

        /// <summary>
        /// Critical heal threshold percentage (0-1). Targets below this get 2x heal (default: 0.10 = 10%).
        /// </summary>
        float CritThreshold { get; set; }

        /// <summary>
        /// Critical heal multiplier (default: 2f).
        /// </summary>
        float CritMultiplier { get; set; }
    }
}
