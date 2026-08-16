using System;
using System.IO;
using UnityEngine;

namespace ICanShowYouTheWorld.Core
{
    /// <summary>
    /// Interface for mod configuration. Allows for testing and future extensibility.
    /// </summary>
    public interface IConfiguration
    {
        // === Pet System ===
        float PetBuffRadius { get; set; }
        float PetBuffMultiplier { get; set; }
        float PetHealthMultiplier { get; set; }

        // === Combat System ===
        int DefaultDamageCounter { get; set; }
        int DamageCounterIncrement { get; set; }
        float SpeedIncrement { get; set; }
        float DefaultRunSpeed { get; set; }

        // === AoE & Buff System ===
        float DefaultAoePower { get; set; }
        float AoePowerIncrement { get; set; }
        float RenewalTickInterval { get; set; }
        float GuardianGiftRadius { get; set; }
        float CloakOfFlamesRadius { get; set; }
        float CloakOfFlamesDamage { get; set; }
        float AoeRenewalRadius { get; set; }

        // === Teleport System ===
        float TeleportSafeFallDistance { get; set; }
        bool TeleportRequireGodMode { get; set; }

        // === Cleanup System ===
        float TrashCleanupRadius { get; set; }

        // === UI Settings ===
        float TrackingWindowWidth { get; set; }
        float TrackingWindowHeight { get; set; }
        float ModesWindowWidth { get; set; }
        float ModesWindowHeight { get; set; }
        float PetsWindowWidth { get; set; }
        float PetsWindowHeight { get; set; }
        float UiScale { get; set; }
        float TrackingRange { get; set; }
        float PetDisplayRange { get; set; }

        // === Run Mode ===
        float RunResourceRate { get; set; }
        float RunSkillGainRate { get; set; }
        float RunMoveStaminaRate { get; set; }
        float RunStaminaRegenRate { get; set; }
        float RunHeatEnemyDamageWeight { get; set; }
        float RunHeatEnemyLevelUpWeight { get; set; }
        float RunHeatScoreWeight { get; set; }
        float RunParTimeMinutes { get; set; }
        float RunDeathHeatPenalty { get; set; }
        float RunRerollHeatCost { get; set; }
        float RunChallengeRefillSeconds { get; set; }
        float RunBoonOfferTimeoutSeconds { get; set; }
        string RunFinalBossKey { get; set; }

        // === Debug & System ===
        bool EnableDebugMode { get; set; }
        bool EnableDebugLogs { get; set; }
        string ConfigVersion { get; set; }

        // === Methods ===
        void Load();
        void Save();
        void ResetToDefaults();
    }

    /// <summary>
    /// Configuration implementation with JSON persistence.
    /// Settings are saved to Application.persistentDataPath/ICanShowYouTheWorld.json
    /// </summary>
    [Serializable]
    public class Configuration : IConfiguration
    {
        // === Pet System ===
        [SerializeField] private float petBuffRadius = 10f;
        [SerializeField] private float petBuffMultiplier = 1.2f;
        [SerializeField] private float petHealthMultiplier = 1.5f;

        // === Combat System ===
        [SerializeField] private int defaultDamageCounter = 1;
        [SerializeField] private int damageCounterIncrement = 1;
        [SerializeField] private float speedIncrement = 0.5f;
        [SerializeField] private float defaultRunSpeed = 7f;

        // === AoE & Buff System ===
        [SerializeField] private float defaultAoePower = 50f;
        [SerializeField] private float aoePowerIncrement = 10f;
        [SerializeField] private float renewalTickInterval = 1f;
        [SerializeField] private float guardianGiftRadius = 20f;
        [SerializeField] private float cloakOfFlamesRadius = 8f;
        [SerializeField] private float cloakOfFlamesDamage = 20f;
        [SerializeField] private float aoeRenewalRadius = 20f;

        // === Teleport System ===
        [SerializeField] private float teleportSafeFallDistance = 5f;
        [SerializeField] private bool teleportRequireGodMode = false;

        // === Cleanup System ===
        [SerializeField] private float trashCleanupRadius = 1f;

        // === UI Settings ===
        [SerializeField] private float trackingWindowWidth = 300f;
        [SerializeField] private float trackingWindowHeight = 250f;
        [SerializeField] private float modesWindowWidth = 325f;
        [SerializeField] private float modesWindowHeight = 550f;
        [SerializeField] private float petsWindowWidth = 200f;
        [SerializeField] private float petsWindowHeight = 250f;
        // 0 = auto: scale the UI so it keeps the same relative size it has on
        // a 800px-tall screen (the Steam Deck). Set explicitly to override.
        [SerializeField] private float uiScale = 0f;
        [SerializeField] private float trackingRange = 100f;
        [SerializeField] private float petDisplayRange = 50f;

        // === Run Mode ===
        [SerializeField] private float runResourceRate = 3f;
        [SerializeField] private float runSkillGainRate = 3f;
        [SerializeField] private float runMoveStaminaRate = 0.5f;
        [SerializeField] private float runStaminaRegenRate = 1.5f;
        [SerializeField] private float runHeatEnemyDamageWeight = 0.05f;
        [SerializeField] private float runHeatEnemyLevelUpWeight = 0.05f;
        [SerializeField] private float runHeatScoreWeight = 0.1f;
        [SerializeField] private float runParTimeMinutes = 240f;
        [SerializeField] private float runDeathHeatPenalty = 3f;
        [SerializeField] private float runRerollHeatCost = 1f;
        [SerializeField] private float runChallengeRefillSeconds = 120f;
        [SerializeField] private float runBoonOfferTimeoutSeconds = 45f;
        [SerializeField] private string runFinalBossKey = "defeated_goblinking";

        // === Debug & System ===
        [SerializeField] private bool enableDebugMode = false;
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private string configVersion = "1.0";

        // === Properties ===
        public float PetBuffRadius { get => petBuffRadius; set => petBuffRadius = value; }
        public float PetBuffMultiplier { get => petBuffMultiplier; set => petBuffMultiplier = value; }
        public float PetHealthMultiplier { get => petHealthMultiplier; set => petHealthMultiplier = value; }

        public int DefaultDamageCounter { get => defaultDamageCounter; set => defaultDamageCounter = value; }
        public int DamageCounterIncrement { get => damageCounterIncrement; set => damageCounterIncrement = value; }
        public float SpeedIncrement { get => speedIncrement; set => speedIncrement = value; }
        public float DefaultRunSpeed { get => defaultRunSpeed; set => defaultRunSpeed = value; }

        public float DefaultAoePower { get => defaultAoePower; set => defaultAoePower = value; }
        public float AoePowerIncrement { get => aoePowerIncrement; set => aoePowerIncrement = value; }
        public float RenewalTickInterval { get => renewalTickInterval; set => renewalTickInterval = value; }
        public float GuardianGiftRadius { get => guardianGiftRadius; set => guardianGiftRadius = value; }
        public float CloakOfFlamesRadius { get => cloakOfFlamesRadius; set => cloakOfFlamesRadius = value; }
        public float CloakOfFlamesDamage { get => cloakOfFlamesDamage; set => cloakOfFlamesDamage = value; }
        public float AoeRenewalRadius { get => aoeRenewalRadius; set => aoeRenewalRadius = value; }

        public float TeleportSafeFallDistance { get => teleportSafeFallDistance; set => teleportSafeFallDistance = value; }
        public bool TeleportRequireGodMode { get => teleportRequireGodMode; set => teleportRequireGodMode = value; }

        public float TrashCleanupRadius { get => trashCleanupRadius; set => trashCleanupRadius = value; }

        public float TrackingWindowWidth { get => trackingWindowWidth; set => trackingWindowWidth = value; }
        public float TrackingWindowHeight { get => trackingWindowHeight; set => trackingWindowHeight = value; }
        public float ModesWindowWidth { get => modesWindowWidth; set => modesWindowWidth = value; }
        public float ModesWindowHeight { get => modesWindowHeight; set => modesWindowHeight = value; }
        public float PetsWindowWidth { get => petsWindowWidth; set => petsWindowWidth = value; }
        public float PetsWindowHeight { get => petsWindowHeight; set => petsWindowHeight = value; }
        public float UiScale { get => uiScale; set => uiScale = value; }
        public float TrackingRange { get => trackingRange; set => trackingRange = value; }
        public float PetDisplayRange { get => petDisplayRange; set => petDisplayRange = value; }

        public float RunResourceRate { get => runResourceRate; set => runResourceRate = value; }
        public float RunSkillGainRate { get => runSkillGainRate; set => runSkillGainRate = value; }
        public float RunMoveStaminaRate { get => runMoveStaminaRate; set => runMoveStaminaRate = value; }
        public float RunStaminaRegenRate { get => runStaminaRegenRate; set => runStaminaRegenRate = value; }
        public float RunHeatEnemyDamageWeight { get => runHeatEnemyDamageWeight; set => runHeatEnemyDamageWeight = value; }
        public float RunHeatEnemyLevelUpWeight { get => runHeatEnemyLevelUpWeight; set => runHeatEnemyLevelUpWeight = value; }
        public float RunHeatScoreWeight { get => runHeatScoreWeight; set => runHeatScoreWeight = value; }
        public float RunParTimeMinutes { get => runParTimeMinutes; set => runParTimeMinutes = value; }
        public float RunDeathHeatPenalty { get => runDeathHeatPenalty; set => runDeathHeatPenalty = value; }
        public float RunRerollHeatCost { get => runRerollHeatCost; set => runRerollHeatCost = value; }
        public float RunChallengeRefillSeconds { get => runChallengeRefillSeconds; set => runChallengeRefillSeconds = value; }
        public float RunBoonOfferTimeoutSeconds { get => runBoonOfferTimeoutSeconds; set => runBoonOfferTimeoutSeconds = value; }
        public string RunFinalBossKey { get => runFinalBossKey; set => runFinalBossKey = value; }

        public bool EnableDebugMode { get => enableDebugMode; set => enableDebugMode = value; }
        public bool EnableDebugLogs { get => enableDebugLogs; set => enableDebugLogs = value; }
        public string ConfigVersion { get => configVersion; set => configVersion = value; }

        /// <summary>
        /// Load configuration from JSON file. Creates default config if file doesn't exist.
        /// </summary>
        public void Load()
        {
            try
            {
                string path = GetConfigPath();

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    JsonUtility.FromJsonOverwrite(json, this);

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ICanShowYouTheWorld] Configuration loaded from: {path}");
                    }
                }
                else
                {
                    // First time - save defaults
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ICanShowYouTheWorld] No config found, creating default at: {path}");
                    }
                    Save();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to load configuration: {ex.Message}");
                Debug.LogError($"[ICanShowYouTheWorld] Using default configuration values");
            }
        }

        /// <summary>
        /// Save current configuration to JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonUtility.ToJson(this, prettyPrint: true);

                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json);

                if (enableDebugLogs)
                {
                    Debug.Log($"[ICanShowYouTheWorld] Configuration saved to: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to save configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset all settings to default values.
        /// </summary>
        public void ResetToDefaults()
        {
            // Create a new instance and copy its values
            var defaults = new Configuration();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), this);

            if (enableDebugLogs)
            {
                Debug.Log("[ICanShowYouTheWorld] Configuration reset to defaults");
            }
        }

        /// <summary>
        /// Get the full path to the configuration file.
        /// </summary>
        private string GetConfigPath()
        {
            return Path.Combine(Application.persistentDataPath, "ICanShowYouTheWorld.json");
        }

        /// <summary>
        /// Get configuration file path (for external access/debugging).
        /// </summary>
        public static string GetConfigFilePath()
        {
            return Path.Combine(Application.persistentDataPath, "ICanShowYouTheWorld.json");
        }
    }
}
