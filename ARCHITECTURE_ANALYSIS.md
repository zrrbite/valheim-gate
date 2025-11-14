# Architecture Analysis & Recommendations

## Executive Summary

This document provides an architectural analysis of the ICanShowYouTheWorld Valheim mod and suggests improvements for maintainability, testability, and extensibility.

## Current Architecture Issues

### 1. God Object Anti-Pattern
**Problem**: `CheatCommands.cs` is **2,699 lines** containing dozens of static methods and fields. This violates the Single Responsibility Principle.

**Location**: `ICanShowYouTheWorld/CheatCommands.cs`

**Impact**:
- Difficult to navigate and maintain
- High risk of merge conflicts
- Changes in one area can break unrelated features
- Impossible to test in isolation

### 2. Excessive Static State
**Problem**: Heavy reliance on static classes and methods throughout the codebase:
- `CheatCommands` (all static)
- `CommandRegistry` (static list)
- `CheatVisualizer` (all static)
- `PetBuff` (all static)
- `PlantingTools` (all static)
- `CleanupUtils` (all static)
- `BossData` (all static)
- `DamageHelpers` (all static)

**Impact**:
- Global mutable state makes debugging difficult
- Cannot mock or stub for testing
- Tight coupling between components
- No control over instance lifecycle
- Memory leaks (static references never GC'd)

### 3. Tight Coupling
**Problem**: Components directly reference each other's internals:
```csharp
// UIManager directly accesses CheatCommands static fields
GUILayout.Label($"Prefab: {CheatCommands.CurrentPrefab}");
GUILayout.Label(CheatCommands.DamageCounter.ToString());

// CheatController calls CheatCommands directly
Execute = CheatCommands.ToggleGodMode
```

**Impact**:
- Changes ripple through the codebase
- Cannot swap implementations
- Difficult to refactor

### 4. Mixed Responsibilities
**Problem**: Classes doing too many unrelated things:
- `NotACheater` handles both initialization AND RPC dumping
- `CheatCommands` handles teleportation, combat, pets, buffs, farming, spawning, etc.
- `UIManager` contains both UI logic AND visualization logic (`CheatVisualizer`)

**Impact**:
- Violates Single Responsibility Principle
- Hard to find specific functionality
- Difficult to reuse code

### 5. No Configuration System
**Problem**: Magic numbers scattered throughout:
```csharp
const float TW = 300, TH = 250f;
string version = "0.221.5-2"; // Hardcoded!
private const float GROUP_MULT = 1.2f;
public static float TrashRadius = 1f;
```

**Impact**:
- Cannot adjust settings without recompiling
- User cannot configure mod behavior
- Difficult to tune balance

### 6. Poor Testability
**Problem**: Static methods and tight coupling make unit testing nearly impossible.

**Impact**:
- Bugs caught only through manual testing
- Refactoring is risky
- Regression testing is difficult

### 7. No Abstraction Layer
**Problem**: Direct calls to Valheim API throughout the code with no abstraction:
```csharp
Player.m_localPlayer.transform.position
Character.GetCharactersInRange(...)
```

**Impact**:
- Cannot mock game API for testing
- Valheim API changes break code in many places
- Cannot simulate game scenarios

## Recommended Architecture

### High-Level Structure
```
ICanShowYouTheWorld/
├── Core/                    # Core mod infrastructure
│   ├── ModBootstrap.cs      # Entry point (replaces NotACheater)
│   ├── ServiceContainer.cs  # Simple DI container
│   └── Configuration.cs     # Config management
├── Services/                # Business logic layer
│   ├── ITeleportService.cs
│   ├── ICombatService.cs
│   ├── IPetService.cs
│   ├── IBuffService.cs
│   └── ISpawnService.cs
├── Commands/                # Command pattern implementations
│   ├── ICommand.cs
│   ├── CommandRegistry.cs
│   ├── TeleportCommand.cs
│   ├── GodModeCommand.cs
│   └── ...
├── UI/                      # User interface
│   ├── UIManager.cs
│   ├── Windows/
│   │   ├── TrackingWindow.cs
│   │   ├── ModesWindow.cs
│   │   └── PetsWindow.cs
│   └── Visualization/
│       └── RingVisualizer.cs
├── Game/                    # Game API abstraction
│   ├── IGameAPI.cs
│   └── ValheimGameAPI.cs
└── Utilities/               # Shared utilities
    ├── GeometryUtils.cs
    └── DamageCalculator.cs
```

## Detailed Recommendations

### 1. Break Up CheatCommands

**Before** (2,699 lines):
```csharp
public static class CheatCommands
{
    // 50+ fields
    public static bool GodMode;
    public static float AoePower;
    // ...

    // 100+ methods
    public static void ToggleGodMode() { }
    public static void TeleportHome() { }
    public static void BuffTamed() { }
    public static void SpawnSelectedPrefab() { }
    // ... another 2,600 lines
}
```

**After** (separate services):
```csharp
// Services/ITeleportService.cs
public interface ITeleportService
{
    Vector3 BindLocation { get; set; }
    void TeleportToBindLocation();
    void TeleportToMapCursor();
    void TeleportAllToPlayer();
}

// Services/ICombatService.cs
public interface ICombatService
{
    bool IsGodModeActive { get; }
    int DamageMultiplier { get; set; }
    void ToggleGodMode();
    void IncreaseDamage();
    void DecreaseDamage();
}

// Services/IPetService.cs
public interface IPetService
{
    void BuffAllPets(bool incrementLevel = false);
    void TameNearby(bool clearTarget = false);
    void ResetPetDamage();
}

// Services/IBuffService.cs
public interface IBuffService
{
    bool IsGuardianGiftActive { get; }
    bool IsRenewalActive { get; }
    void ToggleGuardianGift();
    void ToggleRenewal();
    void HandlePeriodicBuffs();
}

// Services/ISpawnService.cs
public interface ISpawnService
{
    string CurrentPrefab { get; }
    void CyclePrefab(int direction = 1);
    void SpawnPrefab(string prefabName, Vector3 position);
}
```

**Benefits**:
- Each service has a clear, focused responsibility
- Can test each service independently
- Easy to find and modify specific functionality
- Can swap implementations (e.g., MockPetService for testing)

### 2. Introduce Service Container / Dependency Injection

**Implementation**:
```csharp
// Core/ServiceContainer.cs
public class ServiceContainer
{
    private static ServiceContainer _instance;
    public static ServiceContainer Instance => _instance ??= new ServiceContainer();

    private readonly Dictionary<Type, object> _services = new();

    public void Register<TInterface, TImplementation>()
        where TImplementation : TInterface, new()
    {
        _services[typeof(TInterface)] = new TImplementation();
    }

    public void Register<TInterface>(TInterface instance)
    {
        _services[typeof(TInterface)] = instance;
    }

    public T Get<T>()
    {
        return (T)_services[typeof(T)];
    }
}

// Core/ModBootstrap.cs
public class ModBootstrap : MonoBehaviour
{
    void Awake()
    {
        // Register all services
        var container = ServiceContainer.Instance;
        container.Register<IConfiguration>(new Configuration());
        container.Register<IGameAPI, ValheimGameAPI>();
        container.Register<ITeleportService, TeleportService>();
        container.Register<ICombatService, CombatService>();
        container.Register<IPetService, PetService>();
        container.Register<IBuffService, BuffService>();
        container.Register<ISpawnService, SpawnService>();

        // Set up UI
        var uiManager = gameObject.AddComponent<UIManager>();
        container.Register<IUIManager>(uiManager);

        // Set up command system
        var commandRegistry = new CommandRegistry(container);
        commandRegistry.RegisterAllCommands();

        // Set up input handling
        var inputManager = new InputManager(commandRegistry);
        container.Register<IInputManager>(inputManager);
    }
}
```

**Benefits**:
- Centralized dependency management
- Easy to swap implementations
- Clear initialization order
- Supports testing with mocks

### 3. Implement Command Pattern Properly

**Before**:
```csharp
new CommandBinding {
    Key = KeyCode.Keypad0,
    Description = "God Mode",
    Execute = CheatCommands.ToggleGodMode,
    GetState = () => CheatCommands.GodMode
}
```

**After**:
```csharp
// Commands/ICommand.cs
public interface ICommand
{
    KeyCode KeyBinding { get; }
    string Description { get; }
    bool IsActive { get; }
    void Execute();
    bool CanExecute();
}

// Commands/GodModeCommand.cs
public class GodModeCommand : ICommand
{
    private readonly ICombatService _combatService;

    public GodModeCommand(ICombatService combatService)
    {
        _combatService = combatService;
    }

    public KeyCode KeyBinding => KeyCode.Keypad0;
    public string Description => "God Mode";
    public bool IsActive => _combatService.IsGodModeActive;

    public void Execute()
    {
        _combatService.ToggleGodMode();
    }

    public bool CanExecute()
    {
        return Player.m_localPlayer != null;
    }
}

// Commands/CommandRegistry.cs
public class CommandRegistry
{
    private readonly List<ICommand> _commands = new();
    private readonly ServiceContainer _services;

    public CommandRegistry(ServiceContainer services)
    {
        _services = services;
    }

    public void RegisterAllCommands()
    {
        var combat = _services.Get<ICombatService>();
        var teleport = _services.Get<ITeleportService>();
        var pet = _services.Get<IPetService>();

        Register(new GodModeCommand(combat));
        Register(new TeleportHomeCommand(teleport));
        Register(new BuffPetsCommand(pet));
        // ... etc
    }

    public void Register(ICommand command)
    {
        _commands.Add(command);
    }

    public IReadOnlyList<ICommand> GetAll() => _commands.AsReadOnly();
}
```

**Benefits**:
- Each command is a separate, testable class
- Commands can have complex logic and validation
- Easy to add new commands
- Clear dependencies

### 4. Extract Configuration System

**Implementation**:
```csharp
// Core/Configuration.cs
public interface IConfiguration
{
    float PetBuffRadius { get; }
    float PetBuffMultiplier { get; }
    float TeleportSafeFallDistance { get; }
    int MaxPrefabIndex { get; }
    bool EnableDebugMode { get; }
    void Load();
    void Save();
}

public class Configuration : IConfiguration
{
    // Default values
    public float PetBuffRadius { get; set; } = 10f;
    public float PetBuffMultiplier { get; set; } = 1.2f;
    public float TeleportSafeFallDistance { get; set; } = 5f;
    public int MaxPrefabIndex { get; set; } = 100;
    public bool EnableDebugMode { get; set; } = false;

    public void Load()
    {
        // Load from JSON file if exists
        string path = Path.Combine(Application.persistentDataPath, "ICanShowYouTheWorld.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, this);
        }
    }

    public void Save()
    {
        string path = Path.Combine(Application.persistentDataPath, "ICanShowYouTheWorld.json");
        string json = JsonUtility.ToJson(this, prettyPrint: true);
        File.WriteAllText(path, json);
    }
}

// Usage in services
public class PetService : IPetService
{
    private readonly IConfiguration _config;

    public PetService(IConfiguration config)
    {
        _config = config;
    }

    public void BuffAllPets(bool incrementLevel = false)
    {
        var radius = _config.PetBuffRadius;  // Configurable!
        var multiplier = _config.PetBuffMultiplier;  // Configurable!
        // ... use config values
    }
}
```

**Benefits**:
- Users can configure without recompiling
- Easy to tune and balance
- Supports profiles (e.g., Easy, Normal, Hard)
- Clear documentation of all settings

### 5. Add Game API Abstraction Layer

**Implementation**:
```csharp
// Game/IGameAPI.cs
public interface IGameAPI
{
    Player LocalPlayer { get; }
    bool IsLocalPlayerValid { get; }
    IEnumerable<Character> GetCharactersInRange(Vector3 position, float radius);
    Vector3 GetMapCursorWorldPosition();
    void TeleportPlayer(Vector3 position);
    void ShowMessage(string text, MessageType type = MessageType.Center);
}

// Game/ValheimGameAPI.cs
public class ValheimGameAPI : IGameAPI
{
    public Player LocalPlayer => Player.m_localPlayer;
    public bool IsLocalPlayerValid => Player.m_localPlayer != null;

    public IEnumerable<Character> GetCharactersInRange(Vector3 position, float radius)
    {
        var list = new List<Character>();
        Character.GetCharactersInRange(position, radius, list);
        return list;
    }

    public Vector3 GetMapCursorWorldPosition()
    {
        // Extract map cursor logic here
        var mousePos = Input.mousePosition;
        // ... complex calculation
        return worldPos;
    }

    public void TeleportPlayer(Vector3 position)
    {
        Player.m_localPlayer.transform.position = position;
    }

    public void ShowMessage(string text, MessageType type = MessageType.Center)
    {
        Player.m_localPlayer?.Message((MessageHud.MessageType)type, text);
    }
}

// Usage in services
public class TeleportService : ITeleportService
{
    private readonly IGameAPI _game;
    private readonly IConfiguration _config;

    public TeleportService(IGameAPI game, IConfiguration config)
    {
        _game = game;
        _config = config;
    }

    public void TeleportToMapCursor()
    {
        if (!_game.IsLocalPlayerValid)
        {
            return;
        }

        Vector3 target = _game.GetMapCursorWorldPosition();
        _game.TeleportPlayer(target);
        _game.ShowMessage("Teleported!");
    }
}
```

**Benefits**:
- Can create MockGameAPI for testing
- Isolates Valheim API changes
- Makes porting to other games easier
- Clear interface to game functionality

### 6. Separate UI Components

**Before**: Single UIManager with 3 window drawing methods

**After**: Separate window classes
```csharp
// UI/IWindow.cs
public interface IWindow
{
    bool IsVisible { get; set; }
    Rect WindowRect { get; set; }
    void Draw(int windowId);
}

// UI/Windows/TrackingWindow.cs
public class TrackingWindow : IWindow
{
    private readonly IGameAPI _game;
    public Rect WindowRect { get; set; }
    public bool IsVisible { get; set; }

    public TrackingWindow(IGameAPI game)
    {
        _game = game;
        WindowRect = new Rect(250, Screen.height - 270, 300, 250);
    }

    public void Draw(int windowId)
    {
        if (!_game.IsLocalPlayerValid) return;

        var characters = _game.GetCharactersInRange(
            _game.LocalPlayer.transform.position, 100f);

        foreach (var character in characters.OrderBy(c =>
            Vector3.Distance(c.transform.position, _game.LocalPlayer.transform.position)))
        {
            if (character.IsPlayer() || character.IsTamed()) continue;
            DrawCharacterInfo(character);
        }

        GUI.DragWindow();
    }

    private void DrawCharacterInfo(Character character)
    {
        // Drawing logic here
    }
}

// UI/UIManager.cs (simplified)
public class UIManager : MonoBehaviour
{
    private readonly List<IWindow> _windows = new();
    private bool _visible;

    public void Initialize(ServiceContainer services)
    {
        var game = services.Get<IGameAPI>();
        var combat = services.Get<ICombatService>();
        var pet = services.Get<IPetService>();

        _windows.Add(new TrackingWindow(game));
        _windows.Add(new ModesWindow(combat, services.Get<CommandRegistry>()));
        _windows.Add(new PetsWindow(game, pet));
    }

    public void ToggleVisible()
    {
        _visible = !_visible;
        foreach (var window in _windows)
        {
            window.IsVisible = _visible;
        }
    }

    void OnGUI()
    {
        for (int i = 0; i < _windows.Count; i++)
        {
            var window = _windows[i];
            if (!window.IsVisible) continue;

            window.WindowRect = GUILayout.Window(
                i,
                window.WindowRect,
                window.Draw,
                window.GetType().Name.Replace("Window", "")
            );
        }
    }
}
```

**Benefits**:
- Each window is independently maintainable
- Can enable/disable windows individually
- Easy to add new windows
- Better organization

### 7. Add Event System for Loose Coupling

**Implementation**:
```csharp
// Core/EventBus.cs
public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
        {
            _subscribers[type] = new List<Delegate>();
        }
        _subscribers[type].Add(handler);
    }

    public void Publish<T>(T evt)
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers)
            {
                ((Action<T>)handler)(evt);
            }
        }
    }
}

// Events/ModEvents.cs
public class GodModeToggled
{
    public bool IsActive { get; set; }
}

public class TeleportCompleted
{
    public Vector3 Destination { get; set; }
}

public class PetBuffApplied
{
    public int PetsAffected { get; set; }
}

// Usage
public class CombatService : ICombatService
{
    private readonly EventBus _events;
    private bool _godModeActive;

    public void ToggleGodMode()
    {
        _godModeActive = !_godModeActive;

        // Publish event
        _events.Publish(new GodModeToggled { IsActive = _godModeActive });
    }
}

// UI can subscribe
public class ModesWindow : IWindow
{
    public ModesWindow(EventBus events)
    {
        events.Subscribe<GodModeToggled>(OnGodModeToggled);
    }

    private void OnGodModeToggled(GodModeToggled evt)
    {
        // Update UI state
    }
}
```

**Benefits**:
- Services don't need to know about UI
- Easy to add logging, analytics, etc.
- Supports multiple subscribers
- Decouples components

## Migration Strategy

### Phase 1: Add Abstractions (Non-Breaking)
1. Create service interfaces alongside existing static classes
2. Add ServiceContainer
3. Create Configuration system
4. Test that existing code still works

### Phase 2: Extract Services (One at a Time)
1. Start with smallest: `TeleportService`
2. Move logic from `CheatCommands.Teleport*` methods
3. Update commands to use service
4. Test thoroughly
5. Repeat for each feature area

### Phase 3: Update UI (Gradual)
1. Extract TrackingWindow first (simplest)
2. Update UIManager to use windows
3. Extract remaining windows
4. Test UI functionality

### Phase 4: Clean Up (Final)
1. Remove old static methods
2. Delete unused code
3. Update documentation
4. Add unit tests

### Phase 5: Add Tests
1. Create MockGameAPI
2. Write tests for each service
3. Write tests for commands
4. Add integration tests

## Priority Recommendations

### High Priority (Do First)
1. **Extract Configuration** - Biggest user benefit, easy win
2. **Add Service Container** - Foundation for other improvements
3. **Break up CheatCommands** - Most impactful for maintainability

### Medium Priority
4. **Separate UI Windows** - Improves UI organization
5. **Add Game API Abstraction** - Enables testing

### Low Priority (Nice to Have)
6. **Add Event System** - Helpful but not critical
7. **Write Unit Tests** - After architecture is stable

## Conclusion

The current architecture works but has significant technical debt. The recommended changes will:
- Make the code more maintainable
- Enable unit testing
- Improve extensibility
- Allow user configuration
- Reduce coupling

The migration can be done gradually without breaking existing functionality. Start with configuration and service extraction for the biggest impact.
