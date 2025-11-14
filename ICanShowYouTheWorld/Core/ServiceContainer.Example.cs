/*
 * ServiceContainer Usage Examples
 *
 * This file demonstrates how to use the ServiceContainer for dependency injection.
 * It's not compiled - just for documentation purposes.
 */

#if EXAMPLE_CODE

using ICanShowYouTheWorld.Core;
using UnityEngine;

namespace ICanShowYouTheWorld.Examples
{
    // Example interfaces and implementations
    public interface IExampleService
    {
        void DoSomething();
    }

    public class ExampleService : IExampleService
    {
        public void DoSomething()
        {
            Debug.Log("Doing something!");
        }
    }

    public class ServiceContainerExamples
    {
        // ===== Example 1: Basic Registration and Retrieval =====
        public void Example1_BasicUsage()
        {
            var container = ServiceContainer.Instance;

            // Register a service instance
            var service = new ExampleService();
            container.RegisterInstance<IExampleService>(service);

            // Retrieve the service
            var retrieved = container.Get<IExampleService>();
            retrieved.DoSomething(); // Output: "Doing something!"
        }

        // ===== Example 2: Singleton Registration =====
        public void Example2_Singleton()
        {
            var container = ServiceContainer.Instance;

            // Register a singleton (creates instance immediately)
            container.RegisterSingleton<IExampleService, ExampleService>();

            // Both calls return the same instance
            var service1 = container.Get<IExampleService>();
            var service2 = container.Get<IExampleService>();

            // service1 and service2 are the same object
        }

        // ===== Example 3: Factory Registration (Lazy Loading) =====
        public void Example3_Factory()
        {
            var container = ServiceContainer.Instance;

            // Register a factory - service not created yet
            container.RegisterFactory<IExampleService>(() => new ExampleService());

            // Service is created on first Get() call
            var service = container.Get<IExampleService>();

            // Subsequent calls return the same cached instance
            var service2 = container.Get<IExampleService>();
        }

        // ===== Example 4: Transient Registration (New Instance Each Time) =====
        public void Example4_Transient()
        {
            var container = ServiceContainer.Instance;

            // Register as transient
            container.RegisterTransient<IExampleService, ExampleService>();

            // Each call creates a new instance
            var service1 = container.Get<IExampleService>();
            var service2 = container.Get<IExampleService>();

            // service1 and service2 are different objects
        }

        // ===== Example 5: Fluent API =====
        public void Example5_FluentAPI()
        {
            var container = ServiceContainer.Instance;

            // Chain registrations
            container
                .With(new Configuration())
                .WithSingleton<IExampleService, ExampleService>()
                .WithFactory<IConfiguration>(() => new Configuration());
        }

        // ===== Example 6: Safe Retrieval (TryGet) =====
        public void Example6_SafeRetrieval()
        {
            var container = ServiceContainer.Instance;

            // TryGet returns null if service not registered
            var service = container.TryGet<IExampleService>();

            if (service != null)
            {
                service.DoSomething();
            }
            else
            {
                Debug.Log("Service not registered");
            }
        }

        // ===== Example 7: Check Registration =====
        public void Example7_CheckRegistration()
        {
            var container = ServiceContainer.Instance;

            if (container.IsRegistered<IExampleService>())
            {
                var service = container.Get<IExampleService>();
                service.DoSomething();
            }
            else
            {
                Debug.Log("Service not registered, registering now...");
                container.RegisterSingleton<IExampleService, ExampleService>();
            }
        }

        // ===== Example 8: Debugging =====
        public void Example8_Debugging()
        {
            var container = ServiceContainer.Instance;

            // Register some services
            container.RegisterInstance<IConfiguration>(new Configuration());
            container.RegisterSingleton<IExampleService, ExampleService>();

            // Print all registered services to console
            container.DebugPrintServices();

            // Get list of services as strings
            var services = container.GetRegisteredServices();
            foreach (var service in services)
            {
                Debug.Log($"Registered: {service}");
            }
        }

        // ===== Example 9: Real-World Mod Setup =====
        public void Example9_ModSetup()
        {
            var container = ServiceContainer.Instance;

            // 1. Register configuration (loads from JSON)
            var config = new Configuration();
            config.Load();
            container.RegisterInstance<IConfiguration>(config);

            // 2. Register game API abstraction
            container.RegisterSingleton<IGameAPI, ValheimGameAPI>();

            // 3. Register services that depend on config and game API
            container.RegisterFactory<ITeleportService>(() =>
            {
                var cfg = container.Get<IConfiguration>();
                var game = container.Get<IGameAPI>();
                return new TeleportService(game, cfg);
            });

            container.RegisterFactory<ICombatService>(() =>
            {
                var cfg = container.Get<IConfiguration>();
                var game = container.Get<IGameAPI>();
                return new CombatService(game, cfg);
            });

            // 4. Use services
            var teleport = container.Get<ITeleportService>();
            teleport.TeleportToBindLocation();
        }

        // ===== Example 10: Clean Up =====
        public void Example10_CleanUp()
        {
            var container = ServiceContainer.Instance;

            // Unregister a specific service
            container.Unregister<IExampleService>();

            // Clear all services
            container.Clear();

            // Reset the entire container (creates new instance)
            ServiceContainer.Reset();
        }
    }

    // ===== Example Interfaces (from the mod) =====
    public interface IGameAPI { }
    public interface ITeleportService
    {
        void TeleportToBindLocation();
    }
    public interface ICombatService { }
    public class ValheimGameAPI : IGameAPI { }
    public class TeleportService : ITeleportService
    {
        public TeleportService(IGameAPI game, IConfiguration config) { }
        public void TeleportToBindLocation() { }
    }
    public class CombatService : ICombatService
    {
        public CombatService(IGameAPI game, IConfiguration config) { }
    }
}

#endif
