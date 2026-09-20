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
        private static bool _initialized;

        // A popup waiting for somewhere to appear. UnifiedPopup.instance is assigned in its
        // OnEnable, so a Push from the entry point can land before the component is live and be
        // swallowed into a log error; CheatController.Update drains this on the first frame
        // UnifiedPopup.IsAvailable() says yes.
        //
        // FAILURES ONLY, since 2026-09-19 (owner: "I guess we dont need to show the popup except
        // if something fails when the mod is loaded"). A dialog to dismiss on every launch is a
        // toll for something that worked, and the menu's own version line now says the mod is
        // loaded without asking for a click. A failure still gets one, because that is the case
        // nobody should be able to miss.
        internal static string PendingPopup;

        public static void Run()
        {
            //DumpAllRPCsToFile();

            // Called from FejdStartup.Start as well as OnCredits, so the second and later
            // calls are ordinary: every return to the main menu reloads the start scene and
            // runs Start again. A popup here would fire on every quit-to-menu; a log line
            // is what this is worth.
            if (_initialized)
            {
                UnityEngine.Debug.Log("[ICanShowYouTheWorld] Already initialized — entry point called again, ignoring.");
                return;
            }
            _initialized = true;

            // Everything below runs inside the game's own startup now. An exception escaping
            // here would take FejdStartup.Start with it and leave the player at a dead main
            // menu, so nothing is allowed out: a mod that cannot load is the mod's problem.
            try
            {
                // Initialize the new service-based architecture
                Core.ModBootstrap.Initialize();

                // 1) Get version string
                string version = ModVersion.VERSION;

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

                // 5) Report it where reporting belongs: the log, and the menu's version line.
                // No popup on success — see PendingPopup.
                UnityEngine.Debug.Log($"[ICanShowYouTheWorld] {string.Join(" | ", msgLines)}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[ICanShowYouTheWorld] Initialization FAILED: {e}");

                // The failure is the case that has to be impossible to miss. Pushed directly when
                // the popup system is already live, and QUEUED when it is not — but the queue is
                // drained by CheatController, which a failed initialisation may never have created,
                // so the direct attempt comes first and the queue is only the fallback.
                try
                {
                    string notice = $"Mod failed to load:\n{e.Message}";

                    if (UnifiedPopup.IsAvailable())
                        UnifiedPopup.Push(new WarningPopup(
                            "ICanShowYouTheWorld",
                            notice,
                            () => UnifiedPopup.Pop()
                        ));
                    else
                        PendingPopup = notice;
                }
                catch { /* the game is more important than the notice */ }
            }
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

        // Cached so Update() doesn't hit the service container every frame.
        private Services.IRunService runService;

        void Awake()
        {
            inputManager = new InputManager();

            // Capture commands without state in a listview (utils?)
            //var utilGO = new GameObject("UtilityListView");
            //GameObject.DontDestroyOnLoad(utilGO);
            //var utilView = utilGO.AddComponent<UtilityListView>();

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
                    Key         = KeyCode.End,
                    Description = "Run Mode window",
                    Execute     = () => UIManager.Instance.ToggleRunWindow(),
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
                    Description = "HoT Cheal",
                    Execute = CheatCommands.ToggleRenewal,
                    GetState    = () => CheatCommands.RenewalActive
                },
                // -------- COF, immunity ---------------
                new CommandBinding {
                    Key         = KeyCode.Keypad4,
                    Description = "Cloak of Flames",
                    Execute     = CheatCommands.ToggleCloakOfFlames,
                    GetState    = () => CheatCommands.CloakActive
                },
                // Immunity
                new CommandBinding {
                    Key         = KeyCode.Keypad5,
                    Description = "Immunity",
                    Execute     = CheatCommands.ToggleImmunity,
                    GetState    = () => CheatCommands.immunityActive
                },
                

 /*               // ---- Plant things -----
  *               
  *             new CommandBinding {
                    Key         = KeyCode.Keypad4,
                    Description = "cycle -1",
                    Execute     = () => PlantingTools.CycleSeed(-1)
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad5,
                    Description = "Cycle +1",
                    Execute     = () => PlantingTools.CycleSeed(+1)
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad6,
                    Description = "Plant",
                    Execute     = PlantingTools.PlantSelectedGrid
                },

                // ---- pets  ----
                new CommandBinding {
                    Key         = KeyCode.Keypad7,
                    Description = "Combat pet",
                    Execute     = CheatCommands.SpawnCombatPet
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad4,
                    Description = "Pets follow",
                    Execute     = () => CheatCommands.TameAll()
                },
 */
                new CommandBinding {
                    Key         = KeyCode.Keypad7,
                    Description = "Buff pets (normalize)",
                    Execute     = () => CheatCommands.BuffTamed(false) //PetBuff.BuffAllPets(false)
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad8,
                    Description = "Reset pets",
                    Execute     = PetBuff.ResetPetDmg // 1. SetBaselinePetDmg  2. PetBuff.ResetPetBuffs
                },
               // new CommandBinding {
               //     Key         = KeyCode.Keypad8,
               //     Description = "Tame targeted",
               //     Execute     = CheatCommands.TameTargeted
               // },
               new CommandBinding {
                    Key         = KeyCode.Keypad9,
                    Description = "Tame r, stay",
                    Execute     = () => CheatCommands.TameAll(true)
                },
                // --- aoe regen
                new CommandBinding {
                    Key         = KeyCode.KeypadPlus,
                    Description = "AOE Power++",
                    Execute     = CheatCommands.IncreaseAoePower,
                    GetState    = null
                },
                new CommandBinding {
                    Key         = KeyCode.KeypadMinus,
                    Description = "AOE Power--",
                    Execute     = CheatCommands.DecreaseAoePower,
                    GetState    = null
                },
                new CommandBinding {
                    Key         = KeyCode.KeypadMultiply,
                    Description = "Replenish",
                    Execute     = CheatCommands.ReplenishStacks,
                    GetState    = null
                },
                // ---------------------------------
                //new CommandBinding {
                //    Key         = KeyCode.UpArrow,
                //    Description = "Heal All",
                //    Execute     = CheatCommands.CastHealAOE
                //},
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
                    Key         = KeyCode.UpArrow,
                    Description = "Damage++",
                    Execute     = CheatCommands.IncreaseDamageCounter
                },
                new CommandBinding {
                    Key         = KeyCode.DownArrow,
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
                    Key         = KeyCode.Delete,
                    Description = "Prev Prefab",
                    Execute     = CheatCommands.CyclePrefabBackward,
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
                new CommandBinding {
                    Key         = KeyCode.PageDown,
                    Description = "Prev Utility",
                    Execute     = CheatCommands.CycleUtilityBackward,
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
            // Heal all
            };

            foreach (var cmd in commands)
            {
                // A saga-only build does not REGISTER the GM bindings. Not gated, not hidden:
                // absent, because the owner's requirement was that nobody else can reach them
                // ("I dont want anyone to be able to reach the GM mod but me"). Skipping
                // registration also empties CommandRegistry.All, which is what the GM window draws
                // from - so there is nothing to list and nothing to press.
                //
                // Two keys survive in every flavour, and neither is a cheat: End opens the saga,
                // and F1 reveals the run HUD and the stash during a run (see UIManager.OnGUI,
                // which only draws the GM windows when this build has them).
                //
                // What is NOT switched off is CheatCommands itself. Run Mode's boons ride that
                // pipeline through WithLegacyGodModeBracket, so the layer has to live in both
                // flavours; this removes its doors, never its floor.
                bool isModeKey = cmd.Key == KeyCode.End || cmd.Key == KeyCode.F1;
                if (!ModVersion.GmEnabled && !isModeKey) continue;

                // register in the global registry...
                CommandRegistry.All.Add(cmd);

                // ...and hook into input handling
                inputManager.Register(cmd.Key, cmd.Execute);
            }

            // GM-mode commands are dead while a run is live — this both enforces the mode's
            // fairness and resolves the Keypad1-7 collision with boon offer/activate keys in
            // RunService.Tick. Resolved lazily/cached: the service container may not be ready
            // yet at Awake time, and this runs on every keypress.
            InputManager gatedInputManager = inputManager;
            Services.IRunService gateRunService = null;
            gatedInputManager.Gate = key =>
            {
                // Never let a resolution error swallow input: HandleInput loops over every
                // mapped key, so a throw here would kill F1/End along with everything else.
                // Fail open (allow) on any exception.
                try
                {
                    if (gateRunService == null && Core.ModBootstrap.IsInitialized)
                    {
                        gateRunService = Core.ModBootstrap.GetService<Services.IRunService>();
                    }

                    if (gateRunService == null || !gateRunService.IsRunActive) return true; // GM mode: everything allowed
                    return key == KeyCode.F1 || key == KeyCode.End;                        // run mode: UI toggles only
                }
                catch
                {
                    return true;
                }
            };
        }

        void Update()
        {
            DrainPendingPopup();
            MenuBadge.Tick();


            inputManager.HandleInput();
            CheatCommands.HandlePeriodic();

            if (runService == null && Core.ModBootstrap.IsInitialized)
            {
                runService = Core.ModBootstrap.GetService<Services.IRunService>();
            }

            runService?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Show the activation popup the moment there is a popup system to show it in.
        /// Two reference reads per frame until it fires, then nothing.
        /// </summary>
        private static void DrainPendingPopup()
        {
            if (NotACheater.PendingPopup == null) return;

            try
            {
                if (!UnifiedPopup.IsAvailable()) return;

                string msg = NotACheater.PendingPopup;
                NotACheater.PendingPopup = null;
                UnifiedPopup.Push(new WarningPopup(
                    "ICanShowYouTheWorld",
                    msg,
                    () => UnifiedPopup.Pop()
                ));
            }
            catch (Exception e)
            {
                // Never retry forever over a broken popup — the version is also in the
                // lobby header, the run HUD and under the loading-screen title card.
                NotACheater.PendingPopup = null;
                UnityEngine.Debug.LogWarning($"[ICanShowYouTheWorld] Could not show the activation popup: {e.Message}");
            }
        }
    }
}
