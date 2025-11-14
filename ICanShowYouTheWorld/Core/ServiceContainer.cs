using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld.Core
{
    /// <summary>
    /// Simple dependency injection container for managing service instances.
    /// Provides centralized service registration and retrieval.
    /// </summary>
    public class ServiceContainer
    {
        private static ServiceContainer _instance;
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();
        private readonly object _lock = new object();

        /// <summary>
        /// Get the singleton instance of the service container.
        /// </summary>
        public static ServiceContainer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ServiceContainer();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Private constructor for singleton pattern.
        /// </summary>
        private ServiceContainer() { }

        /// <summary>
        /// Reset the singleton instance. Useful for testing or mod reloading.
        /// </summary>
        public static void Reset()
        {
            _instance = null;
        }

        /// <summary>
        /// Register a service with a concrete instance.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to register.</typeparam>
        /// <param name="instance">The concrete instance to register.</param>
        public void RegisterInstance<TInterface>(TInterface instance) where TInterface : class
        {
            lock (_lock)
            {
                var type = typeof(TInterface);

                if (_services.ContainsKey(type))
                {
                    Debug.LogWarning($"[ServiceContainer] Service {type.Name} is already registered. Overwriting.");
                }

                _services[type] = instance;

                // Remove factory if it exists
                if (_factories.ContainsKey(type))
                {
                    _factories.Remove(type);
                }

                Debug.Log($"[ServiceContainer] Registered instance: {type.Name}");
            }
        }

        /// <summary>
        /// Register a service with a factory function (lazy instantiation).
        /// The service will be created the first time it's requested.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to register.</typeparam>
        /// <param name="factory">Factory function that creates the service instance.</param>
        public void RegisterFactory<TInterface>(Func<TInterface> factory) where TInterface : class
        {
            lock (_lock)
            {
                var type = typeof(TInterface);

                if (_factories.ContainsKey(type) || _services.ContainsKey(type))
                {
                    Debug.LogWarning($"[ServiceContainer] Service {type.Name} is already registered. Overwriting.");
                }

                _factories[type] = () => factory();

                Debug.Log($"[ServiceContainer] Registered factory: {type.Name}");
            }
        }

        /// <summary>
        /// Register a singleton service by type. Creates instance immediately.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to register.</typeparam>
        /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
        public void RegisterSingleton<TInterface, TImplementation>()
            where TImplementation : class, TInterface, new()
            where TInterface : class
        {
            lock (_lock)
            {
                var type = typeof(TInterface);

                if (_services.ContainsKey(type))
                {
                    Debug.LogWarning($"[ServiceContainer] Service {type.Name} is already registered. Overwriting.");
                }

                var instance = new TImplementation();
                _services[type] = instance;

                Debug.Log($"[ServiceContainer] Registered singleton: {type.Name} -> {typeof(TImplementation).Name}");
            }
        }

        /// <summary>
        /// Register a transient service (new instance created each time).
        /// </summary>
        /// <typeparam name="TInterface">The interface type to register.</typeparam>
        /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
        public void RegisterTransient<TInterface, TImplementation>()
            where TImplementation : class, TInterface, new()
            where TInterface : class
        {
            RegisterFactory<TInterface>(() => new TImplementation());
        }

        /// <summary>
        /// Get a registered service by interface type.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to retrieve.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if service is not registered.</exception>
        public TInterface Get<TInterface>() where TInterface : class
        {
            lock (_lock)
            {
                var type = typeof(TInterface);

                // Check if instance exists
                if (_services.TryGetValue(type, out var instance))
                {
                    return (TInterface)instance;
                }

                // Check if factory exists
                if (_factories.TryGetValue(type, out var factory))
                {
                    var newInstance = (TInterface)factory();

                    // Cache the instance (singleton behavior for factories)
                    _services[type] = newInstance;

                    Debug.Log($"[ServiceContainer] Created instance from factory: {type.Name}");
                    return newInstance;
                }

                throw new InvalidOperationException(
                    $"Service {type.Name} is not registered. " +
                    $"Use RegisterInstance, RegisterFactory, or RegisterSingleton first.");
            }
        }

        /// <summary>
        /// Try to get a registered service. Returns null if not found.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to retrieve.</typeparam>
        /// <returns>The service instance, or null if not registered.</returns>
        public TInterface TryGet<TInterface>() where TInterface : class
        {
            try
            {
                return Get<TInterface>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to check.</typeparam>
        /// <returns>True if the service is registered, false otherwise.</returns>
        public bool IsRegistered<TInterface>() where TInterface : class
        {
            lock (_lock)
            {
                var type = typeof(TInterface);
                return _services.ContainsKey(type) || _factories.ContainsKey(type);
            }
        }

        /// <summary>
        /// Unregister a service by interface type.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to unregister.</typeparam>
        /// <returns>True if the service was unregistered, false if it wasn't registered.</returns>
        public bool Unregister<TInterface>() where TInterface : class
        {
            lock (_lock)
            {
                var type = typeof(TInterface);
                bool removed = false;

                if (_services.Remove(type))
                {
                    removed = true;
                    Debug.Log($"[ServiceContainer] Unregistered service: {type.Name}");
                }

                if (_factories.Remove(type))
                {
                    removed = true;
                    Debug.Log($"[ServiceContainer] Unregistered factory: {type.Name}");
                }

                return removed;
            }
        }

        /// <summary>
        /// Clear all registered services and factories.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                int serviceCount = _services.Count;
                int factoryCount = _factories.Count;

                _services.Clear();
                _factories.Clear();

                Debug.Log($"[ServiceContainer] Cleared {serviceCount} services and {factoryCount} factories");
            }
        }

        /// <summary>
        /// Get a list of all registered service types (for debugging).
        /// </summary>
        /// <returns>List of registered type names.</returns>
        public List<string> GetRegisteredServices()
        {
            lock (_lock)
            {
                var services = new List<string>();

                foreach (var type in _services.Keys)
                {
                    services.Add($"{type.Name} (instance)");
                }

                foreach (var type in _factories.Keys)
                {
                    if (!_services.ContainsKey(type))
                    {
                        services.Add($"{type.Name} (factory)");
                    }
                }

                return services;
            }
        }

        /// <summary>
        /// Print all registered services to the console (for debugging).
        /// </summary>
        public void DebugPrintServices()
        {
            lock (_lock)
            {
                Debug.Log("[ServiceContainer] === Registered Services ===");

                foreach (var kvp in _services)
                {
                    Debug.Log($"  {kvp.Key.Name} -> {kvp.Value.GetType().Name} (instance)");
                }

                foreach (var kvp in _factories)
                {
                    if (!_services.ContainsKey(kvp.Key))
                    {
                        Debug.Log($"  {kvp.Key.Name} (factory, not yet created)");
                    }
                }

                Debug.Log("[ServiceContainer] ===========================");
            }
        }
    }

    /// <summary>
    /// Extension methods for ServiceContainer to make registration more fluent.
    /// </summary>
    public static class ServiceContainerExtensions
    {
        /// <summary>
        /// Register and return the container for fluent chaining.
        /// </summary>
        public static ServiceContainer With<TInterface>(this ServiceContainer container, TInterface instance)
            where TInterface : class
        {
            container.RegisterInstance(instance);
            return container;
        }

        /// <summary>
        /// Register singleton and return the container for fluent chaining.
        /// </summary>
        public static ServiceContainer WithSingleton<TInterface, TImplementation>(this ServiceContainer container)
            where TImplementation : class, TInterface, new()
            where TInterface : class
        {
            container.RegisterSingleton<TInterface, TImplementation>();
            return container;
        }

        /// <summary>
        /// Register factory and return the container for fluent chaining.
        /// </summary>
        public static ServiceContainer WithFactory<TInterface>(this ServiceContainer container, Func<TInterface> factory)
            where TInterface : class
        {
            container.RegisterFactory(factory);
            return container;
        }
    }
}
