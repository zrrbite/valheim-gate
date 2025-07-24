using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;
using Object = UnityEngine.Object;
using System.Reflection;
using System.Text;
using System.IO;
using Debug = System.Diagnostics.Debug;

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
        private static bool _initialized;

        public static void Run()
        {
            //DumpAllRPCsToFile();

            // If we've already run, notify the user and bail out
            if (_initialized)
            {
                UnifiedPopup.Push(new WarningPopup(
                    "ICanShowYouTheWorld",
                    "Mod is already initialized!",
                    () => UnifiedPopup.Pop()
                ));
                return;
            }
            _initialized = true;

            // 1) Get  version string
            string version = "0.220.5-4"; //ModVersion.VERSION;

            // 2) Create the cheat GameObject
            var cheatObject = new GameObject("ICanShowYouTheWorld");
            GameObject.DontDestroyOnLoad(cheatObject);

            // 3) Try adding each component, record successes
            var loaded = new List<string>();
            TryAddAndRecord<CheatController>(cheatObject, loaded);
            TryAddAndRecord<UIManager>(cheatObject, loaded);

            // 4) Build the final message: version + list
            var msgLines = new List<string>
            {
                $"Loaded mod v{version}!"
            };
            msgLines.AddRange(loaded);

            string msg = string.Join("\n", msgLines);

            // 5) Show single popup with results
            UnifiedPopup.Push(new WarningPopup(
                "ICanShowYouTheWorld",
                msg,
                () => UnifiedPopup.Pop()
            ));
        }

        public static void DumpAllRPCsToFile()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var asm = typeof(ZNetView).Assembly;
                foreach (var type in asm.GetTypes())
                {
                    foreach (var mi in type.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (mi.Name.StartsWith("RPC_", StringComparison.Ordinal))
                            sb.AppendLine($"{type.FullName}.{mi.Name}");
                    }
                }

                // write to persistent data path
                string folder = Application.persistentDataPath;
                string file = Path.Combine(folder, "rpc_dump.txt");
                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);

                Debug.Print($"[DebugUtils] RPC dump saved to: {file}");
                Player.m_localPlayer?.Message(
                    MessageHud.MessageType.Center,
                    $"✅ RPC list written to:\n{file}"
                );
            }
            catch (Exception ex)
            {
                Debug.Print($"[DebugUtils] Failed to dump RPCs: {ex}");
                Player.m_localPlayer?.Message(
                    MessageHud.MessageType.Center,
                    $"❌ RPC dump failed: {ex.Message}"
                );
            }
        }

        private static void TryAddAndRecord<T>(GameObject go, List<string> log)
       where T : Component
        {
            try
            {
                go.AddComponent<T>();
                log.Add($"• {typeof(T).Name}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to add {typeof(T).Name}: {e}");
            }
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
                //new CommandBinding {
                //    Key         = KeyCode.Keypad7,
                //    Description = "Combat pet",
                //    Execute     = CheatCommands.SpawnCombatPet
                //},
                new CommandBinding {
                    Key         = KeyCode.Keypad4,
                    Description = "Pets follow",
                    Execute     = () => CheatCommands.TameAll()
                },
               new CommandBinding {
                    Key         = KeyCode.Keypad5,
                    Description = "Pets stay",
                    Execute     = () => CheatCommands.TameAll(true)
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad6,
                    Description = "Buff pets",
                    Execute     = CheatCommands.BuffTamed
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad7,
                    Description = "Tame targeted",
                    Execute     = CheatCommands.TameTargeted
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad9,
                    Description = "Toggle Barrier", //stagger barrier
                    Execute     = () => CheatCommands.ToggleBarrierAoE()
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
                }

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
