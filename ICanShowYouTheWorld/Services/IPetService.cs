namespace ICanShowYouTheWorld.Services
{
    /// <summary>
    /// Service for managing tamed pets.
    /// Handles pet buffing, damage management, and health modifications.
    /// </summary>
    public interface IPetService
    {
        /// <summary>
        /// Buff all tamed pets within range (10m).
        /// Sets max health to 5000 and applies damage multiplier to equipped weapons.
        /// </summary>
        /// <param name="incrLevel">If true, increases pet level to 2.</param>
        void BuffAllPets(bool incrLevel = false);

        /// <summary>
        /// Reset pet weapons to a fixed damage template.
        /// Sets all pet weapons to fixed values: 20 blunt, 20 slash, 20 pierce.
        /// Requires god mode.
        /// </summary>
        void SetBaselinePetDmg();

        /// <summary>
        /// Reset pet weapons based on their active damage channels.
        /// Weapons using generic damage get 100 damage, typed channels get 50 each.
        /// Preserves which damage types the weapon originally used.
        /// Requires god mode.
        /// </summary>
        void ResetPetDmg();

        /// <summary>
        /// Reset pet weapons to their original prefab damage values.
        /// Uses cached original values from when pets were first buffed.
        /// </summary>
        void ResetPetBuffs();

        /// <summary>
        /// Pet search radius in meters (default: 10m).
        /// </summary>
        float PetRadius { get; }

        /// <summary>
        /// Pet damage multiplier when buffing (default: 1.2x).
        /// </summary>
        float PetDamageMultiplier { get; }
    }
}
