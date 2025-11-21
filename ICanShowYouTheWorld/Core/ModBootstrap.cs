using System;
using UnityEngine;
using ICanShowYouTheWorld.GameAPI;
using ICanShowYouTheWorld.Services;

namespace ICanShowYouTheWorld.Core
{
    /// <summary>
    /// Bootstrap class for initializing the mod's dependency injection container and services.
    /// Replaces the original NotACheater.Run() as the mod's entry point.
    /// </summary>
    public static class ModBootstrap
    {
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the mod. Call this once at startup from the IL-patched entry point.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] ModBootstrap already initialized, skipping");
                return;
            }

            try
            {
                Debug.Log($"[ICanShowYouTheWorld] Starting initialization (v{ModVersion.VERSION})");

                // Step 1: Initialize ServiceContainer singleton
                var container = ServiceContainer.Instance;

                // Step 2: Create and register Configuration
                var config = new Configuration();
                config.Load(); // Load from JSON if exists, otherwise uses defaults
                container.RegisterInstance<IConfiguration>(config);
                Debug.Log("[ICanShowYouTheWorld] Configuration loaded");

                // Step 3: Create and register GameAPI
                var gameAPI = new ValheimGameAPI();
                container.RegisterInstance<IGameAPI>(gameAPI);
                Debug.Log("[ICanShowYouTheWorld] GameAPI initialized");

                // Step 4: Create and register services (order matters due to dependencies)

                // CombatService depends on IGameAPI and IConfiguration
                var combatService = new CombatService(gameAPI, config);
                container.RegisterInstance<ICombatService>(combatService);
                Debug.Log("[ICanShowYouTheWorld] CombatService registered");

                // TeleportService depends on IGameAPI and IConfiguration
                var teleportService = new TeleportService(gameAPI, config);
                container.RegisterInstance<ITeleportService>(teleportService);
                Debug.Log("[ICanShowYouTheWorld] TeleportService registered");

                // PetService depends on IGameAPI, IConfiguration, and ICombatService
                var petService = new PetService(gameAPI, config, combatService);
                container.RegisterInstance<IPetService>(petService);
                Debug.Log("[ICanShowYouTheWorld] PetService registered");

                // SpawnService depends on IGameAPI, IConfiguration, and ICombatService
                var spawnService = new SpawnService(gameAPI, config, combatService);
                container.RegisterInstance<ISpawnService>(spawnService);
                Debug.Log("[ICanShowYouTheWorld] SpawnService registered");

                // BuffService depends on IGameAPI, IConfiguration, and ICombatService
                var buffService = new BuffService(gameAPI, config, combatService);
                container.RegisterInstance<IBuffService>(buffService);
                Debug.Log("[ICanShowYouTheWorld] BuffService registered");

                _initialized = true;
                Debug.Log($"[ICanShowYouTheWorld] Initialization complete! All services registered.");

                // Display welcome message to player
                Console.instance?.Print($"ICanShowYouTheWorld v{ModVersion.VERSION} loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Initialization failed: {ex}");
                Console.instance?.Print($"[ERROR] ICanShowYouTheWorld failed to initialize: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get a service from the container. Safe to call after Initialize().
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "ModBootstrap not initialized. Call Initialize() first.");
            }

            return ServiceContainer.Instance.Get<T>();
        }

        /// <summary>
        /// Check if the mod has been initialized.
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Cleanup and reset (useful for testing or mod reload scenarios).
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            Debug.Log("[ICanShowYouTheWorld] Shutting down...");

            // Save configuration before shutdown
            try
            {
                var config = ServiceContainer.Instance.Get<IConfiguration>();
                config?.Save();
                Debug.Log("[ICanShowYouTheWorld] Configuration saved");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ICanShowYouTheWorld] Failed to save config during shutdown: {ex.Message}");
            }

            // Clear the container
            ServiceContainer.Instance.Clear();

            _initialized = false;
            Debug.Log("[ICanShowYouTheWorld] Shutdown complete");
        }
    }
}
