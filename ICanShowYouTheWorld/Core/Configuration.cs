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
        float RunStaminaRate { get; set; }
        float RunHudMenuOffset { get; set; }
        float RunBuildScanRadius { get; set; }
        float RunDiscoverRadius { get; set; }
        float RunSidePanelX { get; set; }
        float RunDeerStarChance { get; set; }
        int RunDeerStarLevel { get; set; }
        float RunDeerGreylingChance { get; set; }
        float RunHomewardCooldownMinutes { get; set; }
        int RunDeerGreylingMin { get; set; }
        int RunDeerGreylingMax { get; set; }
        int RunDeerGreylingLevel { get; set; }
        bool RunDeerLightning { get; set; }
        float RunHeatEnemyDamageWeight { get; set; }
        float RunHeatEnemyLevelUpWeight { get; set; }
        float RunHeatScoreWeight { get; set; }
        float RunParTimeMinutes { get; set; }
        float RunDeathHeatPenalty { get; set; }
        float RunRerollHeatCost { get; set; }
        float RunChallengeRefillSeconds { get; set; }
        float RunBoonOfferTimeoutSeconds { get; set; }
        float RunBossHpPerBoon { get; set; }
        float RunBossHpPerHeat { get; set; }
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
        [SerializeField] private float runStaminaRegenRate = 2.5f;

        // Multiplies EVERY stamina cost the player pays (Player.UseStamina scales its argument by
        // this before spending), so it reaches the drains the other two don't: blocking, dodging,
        // jumping, bows. Movement is scaled by this AND runMoveStaminaRate, which is intended —
        // exploring on foot is where a run spends most of its stamina.
        [SerializeField] private float runStaminaRate = 0.75f;

        // How far LEFT the run HUD slides while the crafting window or map is open, so both stay
        // readable and the HUD's buttons stay clickable. The right value depends on resolution and
        // UI scale, which is why it is a config knob rather than a constant.
        [SerializeField] private float runHudMenuOffset = 470f;

        // Radius, in metres, of the once-a-second scan that decides whether the player has built a
        // fire / bed / chest / door (the questline's build steps and the door task's gate). Wide
        // enough to take in a starter house from anywhere inside it, small enough that the scan
        // stays cheap; the scan stops altogether once all four have been found.
        [SerializeField] private float runBuildScanRadius = 20f;

        // How close counts as having FOUND a boss altar, for the questline's discovery steps.
        // Generous enough that arriving at the location registers without hunting for its exact
        // centre, tight enough that passing a mile away does not.
        [SerializeField] private float runDiscoverRadius = 30f;

        // How far from the left edge the bottom-left panels (tracker, stash) sit. They were flush
        // at 10px, which overlapped Valheim's own health/food readout (owner, alpha40: "they block
        // health and food") — and then 320 overshot ("TOO far right, they should only be moved just
        // past the health bar"). 190 clears the bar without stranding the panels mid-screen.
        //
        // Config rather than a constant because the right number depends on resolution and UI
        // scale, the same call as runHudMenuOffset. Both panels are draggable too, and keep where
        // they are put until the game window resizes.
        [SerializeField] private float runSidePanelX = 120f;

        // --- Eikthyr's Herd: the deer of Act I ---
        //
        // Eikthyr is the stag god, so his act's deer are his. None of this makes deer AGGRESSIVE —
        // they run AnimalAI, which has no attack at all — it makes them harder to catch and makes
        // killing one mean something.
        [SerializeField] private float runDeerStarChance = 0.5f;    // of unstarred deer met in Act I
        [SerializeField] private int runDeerStarLevel = 2;          // 2 = one star, 3 = two stars
        [SerializeField] private float runDeerGreylingChance = 0.35f; // a kill draws the forest's attention
        // A pack, not a straggler: one greyling wandering up to the carcass reads as an
        // accident rather than the forest noticing. Starred to match — same convention as
        // runDeerStarLevel, where 2 is one star.
        // Long enough to plan around, short enough never to strand anyone. Boss charges are
        // spent first, so a boss kill still buys something this does not.
        [SerializeField] private float runHomewardCooldownMinutes = 10f;
        [SerializeField] private int runDeerGreylingMin = 3;
        [SerializeField] private int runDeerGreylingMax = 5;
        [SerializeField] private int runDeerGreylingLevel = 2;
        [SerializeField] private bool runDeerLightning = true;
        [SerializeField] private float runHeatEnemyDamageWeight = 0.05f;
        [SerializeField] private float runHeatEnemyLevelUpWeight = 0.05f;
        [SerializeField] private float runHeatScoreWeight = 0.1f;
        [SerializeField] private float runParTimeMinutes = 240f;
        [SerializeField] private float runDeathHeatPenalty = 3f;
        [SerializeField] private float runRerollHeatCost = 1f;
        [SerializeField] private float runChallengeRefillSeconds = 45f;
        [SerializeField] private float runBoonOfferTimeoutSeconds = 45f;
        [SerializeField] private float runBossHpPerBoon = 0.12f;
        [SerializeField] private float runBossHpPerHeat = 0.03f;
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
        public float RunStaminaRate { get => runStaminaRate; set => runStaminaRate = value; }
        public float RunHudMenuOffset { get => runHudMenuOffset; set => runHudMenuOffset = value; }
        public float RunBuildScanRadius { get => runBuildScanRadius; set => runBuildScanRadius = value; }
        public float RunDiscoverRadius { get => runDiscoverRadius; set => runDiscoverRadius = value; }
        public float RunSidePanelX { get => runSidePanelX; set => runSidePanelX = value; }
        public float RunDeerStarChance { get => runDeerStarChance; set => runDeerStarChance = value; }
        public int RunDeerStarLevel { get => runDeerStarLevel; set => runDeerStarLevel = value; }
        public float RunDeerGreylingChance { get => runDeerGreylingChance; set => runDeerGreylingChance = value; }
        public float RunHomewardCooldownMinutes { get => runHomewardCooldownMinutes; set => runHomewardCooldownMinutes = value; }
        public int RunDeerGreylingMin { get => runDeerGreylingMin; set => runDeerGreylingMin = value; }
        public int RunDeerGreylingMax { get => runDeerGreylingMax; set => runDeerGreylingMax = value; }
        public int RunDeerGreylingLevel { get => runDeerGreylingLevel; set => runDeerGreylingLevel = value; }
        public bool RunDeerLightning { get => runDeerLightning; set => runDeerLightning = value; }
        public float RunHeatEnemyDamageWeight { get => runHeatEnemyDamageWeight; set => runHeatEnemyDamageWeight = value; }
        public float RunHeatEnemyLevelUpWeight { get => runHeatEnemyLevelUpWeight; set => runHeatEnemyLevelUpWeight = value; }
        public float RunHeatScoreWeight { get => runHeatScoreWeight; set => runHeatScoreWeight = value; }
        public float RunParTimeMinutes { get => runParTimeMinutes; set => runParTimeMinutes = value; }
        public float RunDeathHeatPenalty { get => runDeathHeatPenalty; set => runDeathHeatPenalty = value; }
        public float RunRerollHeatCost { get => runRerollHeatCost; set => runRerollHeatCost = value; }
        public float RunChallengeRefillSeconds { get => runChallengeRefillSeconds; set => runChallengeRefillSeconds = value; }
        public float RunBoonOfferTimeoutSeconds { get => runBoonOfferTimeoutSeconds; set => runBoonOfferTimeoutSeconds = value; }
        public float RunBossHpPerBoon { get => runBossHpPerBoon; set => runBossHpPerBoon = value; }
        public float RunBossHpPerHeat { get => runBossHpPerHeat; set => runBossHpPerHeat = value; }
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
