using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;
using Object = UnityEngine.Object;

//todo:
// 			BaseAI.AggravateAllInArea(Player.m_localPlayer.transform.position, 20f, BaseAI.AggravatedReason.Damage); + shout! Chat.instance.BroadcastMessage("Monsters become aggravated!");
// 			Minimap.instance.ExploreAll();
//          Minimap.instance.Reset();
// 

namespace ICanShowYouTheWorld
{
    public class NotACheater : MonoBehaviour
    {
        private static GameObject cheatObject;

        public static void Run()
        {
            UnifiedPopup.Push(new WarningPopup("ICanShowYouTheWorld", "Loaded mod v0.220.5-2!" // todo: Get this from __VERSION__, set by setversion.sh based on git tags
                , delegate
            {
                UnifiedPopup.Pop();
            }));

            // Instanciate a new GameObject instance attaching script component (class) 
            cheatObject = new GameObject();
            //            cheatObject.AddComponent<DiscoverThings>(); //old
            // Initialize CheatController
            var controller = cheatObject.AddComponent<CheatController>();
            UnifiedPopup.Push(new WarningPopup(
                "ICanShowYouTheWorld",
                "CheatController loaded successfully!",
                () => UnifiedPopup.Pop()
            ));

            // Initialize UIManager
            var ui = cheatObject.AddComponent<UIManager>();
            UnifiedPopup.Push(new WarningPopup(
                "ICanShowYouTheWorld",
                "UIManager loaded successfully!",
                () => UnifiedPopup.Pop()
            ));

            // Avoid object destroyed when loading a level
            GameObject.DontDestroyOnLoad(cheatObject);
        }
    }

    public class CommandBinding
    {
        public KeyCode Key;          // the hotkey
        public string Description;  // what appears in the UI
        public Action Execute;      // what to run on keypress
        public Func<bool> GetState;    // how to know if it’s “on” (optional)
    }

    public static class CommandRegistry
    {
        // The one-and-only list of all commands:
        public static readonly List<CommandBinding> All = new List<CommandBinding>();
    }

    // Central controller: sets up input mappings and triggers periodic cheats
    // Central controller: registers hotkeys and drives per-frame updates
    public class CheatController : MonoBehaviour
    {
        private InputManager inputManager;

        void Awake()
        {
            inputManager = new InputManager();

            //TODO: We need two windows. one for modes and one for commands.
            // Build and register all your commands in one place:
            var commands = new[]
            {
                new CommandBinding {
                    Key         = KeyCode.F1,
                    Description = "Toggle Cheat Window",
                    Execute     = () => UIManager.Instance.ToggleVisible(),
                    GetState    = () => true
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad0,
                    Description = "God Mode", // Also toggles Renewal / resets forsaken power timer
                    Execute     = CheatCommands.ToggleGodMode,
                    GetState    = () => CheatCommands.GodMode
                },
                // --- Important Buffs
                new CommandBinding {
                    Key         = KeyCode.Keypad1,
                    Description = "Guardian Gift",
                    Execute     = CheatCommands.ToggleGuardianGift,
                    GetState    = () => CheatCommands.GiftActive
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad2,
                    Description = "AoE Renewal",
                    Execute     = CheatCommands.ToggleAoeRenewal,
                    GetState    = () => CheatCommands.AOERenewalActive
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad3,
                    Description = "Cloak of Flames",
                    Execute     = CheatCommands.ToggleCloakOfFlames,
                    GetState    = () => CheatCommands.CloakActive
                },
                // ---- pets ? ----
                new CommandBinding {
                    Key         = KeyCode.Keypad4,
                    Description = "Combat pet",
                    Execute     = CheatCommands.SpawnCombatPet
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad5,
                    Description = "Tame/Buff pets",
                    Execute     = CheatCommands.TameAll
                },
                // --- aoe regen
                new CommandBinding {
                    Key         = KeyCode.PageUp,
                    Description = "AOE Power++",
                    Execute     = CheatCommands.IncreaseAoePower,
                    GetState    = null
                },
                new CommandBinding {
                    Key         = KeyCode.PageDown,
                    Description = "AOE Power--",
                    Execute     = CheatCommands.DecreaseAoePower,
                    GetState    = null
                },
                // ---------------------------------
                new CommandBinding {
                    Key         = KeyCode.UpArrow,
                    Description = "Heal All",
                    Execute     = CheatCommands.CastHealAOE
                },
                new CommandBinding {
                    Key         = KeyCode.RightArrow,
                    Description = "Speed++",
                    Execute     = CheatCommands.SpeedUp
                },
                new CommandBinding {
                    Key         = KeyCode.LeftArrow,
                    Description = "Speed--",
                    Execute     = CheatCommands.SpeedDown
                },
                new CommandBinding {
                    Key         = KeyCode.KeypadPlus,
                    Description = "Damage++",
                    Execute     = CheatCommands.IncreaseDamageCounter
                },
                new CommandBinding {
                    Key         = KeyCode.KeypadMinus,
                    Description = "Damage--",
                    Execute     = CheatCommands.DecreaseDamageCounter
                },
                // ---- Teleport ----
                new CommandBinding {
                    Key         = KeyCode.Home,
                    Description = "Gate",
                    Execute     = CheatCommands.TeleportHome
                },
                new CommandBinding {
                    Key         = KeyCode.Insert,
                    Description = "Teleport",
                    Execute     = CheatCommands.TeleportSolo
                },
                // -----
                new CommandBinding {
                    Key         = KeyCode.Numlock,
                    Description = "Kill 'em all",
                    Execute     = CheatCommands.KillAllMonsters,
                },
                new CommandBinding {
                    Key         = KeyCode.KeypadPeriod,
                    Description = "Next Prefab",
                    Execute     = CheatCommands.CyclePrefab,
                },
                new CommandBinding {
                    Key         = KeyCode.KeypadEnter,
                    Description = "Spawn Prefab",
                    Execute     = CheatCommands.SpawnSelectedPrefab
                },

                new CommandBinding {
                    Key         = KeyCode.ScrollLock,
                    Description = "Next Utility",
                    Execute     = CheatCommands.CycleUtility,
                    GetState    = null
                },
                // Execute the current utility (e.g. KeypadEnter)
                new CommandBinding {
                    Key         = KeyCode.Pause,
                    Description = "Run Utility",
                    Execute     = CheatCommands.ExecuteUtility,
                    GetState    = null
                },

            // extra
            // CheatCommands.TeleportMass
            // inputManager.Register(KeyCode.End, CheatCommands.TeleportSafe);
            // inputManager.Register(KeyCode.KeypadPlus, CheatCommands.CastDmgAOE);
            };

            foreach (var cmd in commands)
            {
                // register in the global registry...
                CommandRegistry.All.Add(cmd);

                // ...and hook into input handling
                inputManager.Register(cmd.Key, cmd.Execute);
            }            
        }

        void Update()
        {
            inputManager.HandleInput();
            CheatCommands.HandlePeriodic();
        }
    }
}
