namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Service for managing player buffs, auras, and periodic effects.
    /// Handles Guardian Gift, Renewal (HoT), AoE Renewal, Cloak of Flames, and Immunity.
    /// </summary>
    public interface IBuffService
    {
        // ===== State Properties =====

        /// <summary>
        /// Whether Renewal (HoT - Heal over Time) is active.
        /// Periodically heals player to full and restores stamina/eitr.
        /// </summary>
        bool RenewalActive { get; }

        /// <summary>
        /// Whether AoE Renewal is active.
        /// Periodically heals all tamed creatures and players in range.
        /// </summary>
        bool AOERenewalActive { get; }

        /// <summary>
        /// Whether Cloak of Flames is active.
        /// Periodically damages all hostile creatures in range.
        /// </summary>
        bool CloakActive { get; }

        /// <summary>
        /// Whether Guardian's Gift is active.
        /// Buffs player stats (HP, stamina, eitr, carry weight, armor, resistances).
        /// </summary>
        bool GiftActive { get; }

        /// <summary>
        /// Whether Immunity is active.
        /// Sets all damage modifiers to Immune.
        /// </summary>
        bool ImmunityActive { get; }

        /// <summary>
        /// Current AoE power level for healing and damage.
        /// Range: 5.0 to 200.0
        /// </summary>
        float AoePower { get; }

        // ===== Toggle Methods =====

        /// <summary>
        /// Toggle Renewal (HoT) on/off.
        /// When active, periodically heals player to full and restores stamina/eitr.
        /// </summary>
        void ToggleRenewal();

        /// <summary>
        /// Toggle AoE Renewal on/off.
        /// When active, periodically heals all tamed creatures and players in range.
        /// Requires god mode.
        /// </summary>
        void ToggleAoeRenewal();

        /// <summary>
        /// Toggle Cloak of Flames on/off.
        /// When active, periodically damages and burns all hostile creatures in range.
        /// Requires god mode.
        /// </summary>
        void ToggleCloakOfFlames();

        /// <summary>
        /// Toggle Guardian's Gift on/off.
        /// Buffs/unbuffs player stats including HP, stamina, eitr, carry weight, armor, and resistances.
        /// Requires god mode.
        /// </summary>
        void ToggleGuardianGift();

        /// <summary>
        /// Toggle Immunity on/off.
        /// Makes player immune to all damage types or slightly resistant.
        /// Requires god mode.
        /// </summary>
        void ToggleImmunity();

        // ===== Power Control =====

        /// <summary>
        /// Increase AoE power by 5.0 (up to max 200.0).
        /// Affects both healing and damage output.
        /// </summary>
        void IncreaseAoePower();

        /// <summary>
        /// Decrease AoE power by 5.0 (down to min 5.0).
        /// Affects both healing and damage output.
        /// </summary>
        void DecreaseAoePower();

        // ===== Periodic Update =====

        /// <summary>
        /// Called every frame to handle periodic buff effects.
        /// Must be called from CheatController.Update() or similar.
        /// </summary>
        void HandlePeriodic();
    }
}
