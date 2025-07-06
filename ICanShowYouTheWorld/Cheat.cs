using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;

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
            UnifiedPopup.Push(new WarningPopup("ICanShowYouTheWorld", "Loaded mod v0.220.5!"
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
                    Description = "God Mode",
                    Execute     = CheatCommands.ToggleGodMode,
                    GetState    = () => CheatCommands.GodMode
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad2,
                    Description = "Renewal",
                    Execute     = CheatCommands.ToggleRenewal,
                    GetState    = () => CheatCommands.RenewalActive
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad3,
                    Description = "AoE Renewal",
                    Execute     = CheatCommands.ToggleAoeRenewal,
                    GetState    = () => CheatCommands.AOERenewalActive
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad4,
                    Description = "Cloak of Flames",
                    Execute     = CheatCommands.ToggleCloakOfFlames,
                    GetState    = () => CheatCommands.CloakActive
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad7,
                    Description = "Ghost Mode",
                    Execute     = CheatCommands.ToggleGhostMode,
                    GetState    = () => CheatCommands.GhostMode
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad1,
                    Description = "Guardian Gift",
                    Execute     = CheatCommands.GuardianGift
                    //,GetState    = () => CheatCommands.GiftActive
                },
                new CommandBinding {
                    Key         = KeyCode.UpArrow,
                    Description = "Invigorate",
                    Execute     = CheatCommands.Invigorate
                },
                new CommandBinding {
                    Key         = KeyCode.DownArrow,
                    Description = "Kill em' all!",
                    Execute     = CheatCommands.KillAllMonsters
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad8,
                    Description = "Combat pet",
                    Execute     = CheatCommands.SpawnCombatPet
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad9,
                    Description = "Tame All",
                    Execute     = CheatCommands.TameAll
                },
                new CommandBinding {
                    Key         = KeyCode.PageUp,
                    Description = "++Speed",
                    Execute     = CheatCommands.SpeedUp
                },
                new CommandBinding {
                    Key         = KeyCode.PageDown,
                    Description = "--Speed",
                    Execute     = CheatCommands.SpeedDown
                },
                new CommandBinding {
                    Key         = KeyCode.RightArrow,
                    Description = "++Damage",
                    Execute     = CheatCommands.IncreaseDamageCounter
                },
                new CommandBinding {
                    Key         = KeyCode.LeftArrow,
                    Description = "--Damage",
                    Execute     = CheatCommands.DecreaseDamageCounter
                },
                //Teleport
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
                new CommandBinding {
                    Key         = KeyCode.End,
                    Description = "Mass teleport",
                    Execute     = CheatCommands.TeleportMass
                },
                new CommandBinding {
                    Key         = KeyCode.Keypad6,
                    Description = "Replenish Stacks",
                    Execute     = CheatCommands.ReplenishStacks,
                },
                new CommandBinding {
                    Key         = KeyCode.F6,
                    Description = "Guardian Power",
                    Execute     = CheatCommands.ToggleGuardianPower,
                },

           // inputManager.Register(KeyCode.End, CheatCommands.TeleportSafe);
        // Bosses & exploration
       // /inputManager.Register(KeyCode.F10, CheatCommands.RevealBosses);
          //  inputManager.Register(KeyCode.F7, CheatCommands.ExploreAll);
       /* 



        // Healing & damage
        inputManager.Register(KeyCode.UpArrow, CheatCommands.Invigorate);
        inputManager.Register(KeyCode.LeftArrow, CheatCommands.DecreaseDamageCounter);
        inputManager.Register(KeyCode.RightArrow, CheatCommands.IncreaseDamageCounter);
        inputManager.Register(KeyCode.DownArrow, CheatCommands.KillAllMonsters);

        // Teleports
        inputManager.Register(KeyCode.Insert, CheatCommands.TeleportSolo);
        inputManager.Register(KeyCode.Delete, CheatCommands.TeleportMass);
        inputManager.Register(KeyCode.Home, CheatCommands.TeleportHome);
        inputManager.Register(KeyCode.End, CheatCommands.TeleportSafe);
        // Movement speed
        inputManager.Register(KeyCode.PageUp, CheatCommands.SpeedUp);
        inputManager.Register(KeyCode.PageDown, CheatCommands.SpeedDown);

        // ---------------------------------
        // Keypad 
        inputManager.Register(KeyCode.Keypad0, CheatCommands.ToggleGodMode); // Toggle Modes: God, Builder, Beast Master
        //1-3

        inputManager.Register(KeyCode.Keypad1, CheatCommands.GuardianGift);
        inputManager.Register(KeyCode.Keypad2, CheatCommands.ToggleRenewal);
        inputManager.Register(KeyCode.Keypad3, CheatCommands.ToggleAoeRenewal);

        //4-6
        inputManager.Register(KeyCode.Keypad4, CheatCommands.ToggleCloakOfFlames);
        //5
        inputManager.Register(KeyCode.Keypad6, CheatCommands.ReplenishStacks);

        //7-9
        inputManager.Register(KeyCode.Keypad7, CheatCommands.ToggleGhostMode);
        inputManager.Register(KeyCode.Keypad8, CheatCommands.SpawnCombatPet);
        inputManager.Register(KeyCode.Keypad9, CheatCommands.TameAll);

        // extra

        inputManager.Register(KeyCode.KeypadEnter, CheatCommands.CastHealAOE);
        inputManager.Register(KeyCode.KeypadPlus, CheatCommands.CastDmgAOE);
        inputManager.Register(KeyCode.KeypadMinus, CheatCommands.CastHealAOE);
        inputManager.Register(KeyCode.KeypadDivide, CheatCommands.ToggleGhostMode);
        inputManager.Register(KeyCode.KeypadMultiply, CheatCommands.ToggleGhostMode);
        inputManager.Register(KeyCode.KeypadPeriod, CheatCommands.ToggleGhostMode);                 
             */
        };

            foreach (var cmd in commands)
            {
                // register in the global registry…
                CommandRegistry.All.Add(cmd);

                // …and hook into input handling
                inputManager.Register(cmd.Key, cmd.Execute);
            }            
        }

        void Update()
        {
            inputManager.HandleInput();
            CheatCommands.HandlePeriodic();
        }
    }

    // Maps keys to actions
    public class InputManager
    {
        private readonly Dictionary<KeyCode, Action> mappings = new Dictionary<KeyCode, Action>();

        public void Register(KeyCode key, Action action)
        {
            mappings[key] = action;
        }

        public void HandleInput()
        {
            foreach (var kv in mappings)
            {
                if (Input.GetKeyDown(kv.Key))
                    kv.Value.Invoke();
            }
        }
    }

    // Periodic task infrastructure
    class PeriodicTask
    {
        public int Interval;
        public Action Action;
        public int LastFrame;
    }

    static class PeriodicManager
    {
        private static readonly List<PeriodicTask> tasks = new List<PeriodicTask>();
        public static void Register(int interval, Action action)
        {
            tasks.Add(new PeriodicTask { Interval = interval, Action = action, LastFrame = 0 });
        }
        public static void HandlePeriodic()
        {
            int now = Time.frameCount;
            foreach (var t in tasks)
            {
                if (now - t.LastFrame >= t.Interval)
                {
                    t.Action();
                    t.LastFrame = now;
                }
            }
        }
    }

    // Static cheat commands and shared state
    public static class CheatCommands
    {
        // States
        public static bool GodMode { get; private set; }
        public static bool RenewalActive { get; private set; }
        public static bool AOERenewalActive { get; private set; }
        public static bool GhostMode { get; private set; }
        public static bool CloakActive { get; private set; }
        public static bool MelodicActive { get; private set; }
        public static bool GiftActive { get; private set; }

        private static int guardianIndex = 0;
        public static int DamageCounter { get; private set; }
        public static string CurrentGuardianName => guardians[guardianIndex];
        private static readonly string[] guardians = { "GP_Eikthyr", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Fader" };
        // private static readonly string[] combatPets = { "Wolf", "DvergerMageSupport" }; //AskSvin
        private static readonly string[] combatPets = { "Skeleton_Friendly", "Asksvin" };
        private static readonly string[] petNames = { "Bob", "Ralf", "Liam", "Olivia", "Elijah" /*...*/ };

        static CheatCommands()
        {
            // register periodic callbacks - todo: can do this better
            PeriodicManager.Register(50, () => { if (RenewalActive) Invigorate(); });
            PeriodicManager.Register(50, () => { if (AOERenewalActive) AoeRegen(); });
            PeriodicManager.Register(150, () => { if (MelodicActive) SlowMonsters(); });
            PeriodicManager.Register(75, () => { if (CloakActive) DamageAoE(); });
        }

        public static void HandlePeriodic() => PeriodicManager.HandlePeriodic();

        // Permission check
        private static bool RequireGodMode(string ability)
        {
            if (!GodMode)
            {
                Show($"{ability} requires God Mode");
                return false;
            }
            return true;
        }

        // ---------------------------------------------
        // Periodic things - abstract this
        // ---------------------------------------------
        public static void ToggleRenewal()
        {
            RenewalActive = !RenewalActive;
            Show($"Renewal {(RenewalActive ? "Enabled" : "Disabled")}");
        }

        public static void ToggleAoeRenewal()
        {
            if (!RequireGodMode("AoE Renewal")) return;
            AOERenewalActive = !AOERenewalActive;
            Show($"AoE Renewal {(AOERenewalActive ? "Enabled" : "Disabled")}");
        }

        public static void ToggleCloakOfFlames()
        {
            if (!RequireGodMode("CoF")) return;
            CloakActive = !CloakActive;
            Show($"Cloak of Flames {CloakActive}");
        }

        public static void ToggleMelodicBinding()
        {
            if (!RequireGodMode("Snare")) return;
            MelodicActive = !MelodicActive;
            Show($"Melodic Binding {MelodicActive}");
        }
        // ---------------------------------------------

        public static void ToggleGodMode()
        {
            GodMode = !GodMode;
            Player.m_localPlayer.SetGodMode(GodMode);
            Player.m_localPlayer.SetNoPlacementCost(value: GodMode);
            Show($"God Mode {(GodMode ? "ON" : "OFF")}");
        }

        public static void ToggleGuardianPower()
        {
            Player.m_localPlayer.SetGuardianPower(guardians[guardianIndex]);
            Show($"Guardian Power: {guardians[guardianIndex]}");
            guardianIndex = (guardianIndex + 1) % guardians.Length;
        }

        public static void IncreaseDamageCounter()
        {
            DamageCounter++;
            Show("Damage Counter: " + DamageCounter);
            ApplySuperWeapon();
        }
        public static void DecreaseDamageCounter()
        {
            DamageCounter--;
            Show("Damage Counter: " + DamageCounter);
            ApplySuperWeapon();
        }

        public static void RevealBosses()
        {
            if (!RequireGodMode("Reveal Bosses")) return;
            foreach (var b in BossData.All) BossData.Reveal(b);
            Show("Bosses revealed");
        }

        public static void ExploreAll()
        {
            if (!RequireGodMode("Explore")) return;
            Minimap.instance.ExploreAll();
            Show("Map fully explored");
        }

        public static void SpeedUp() => SetSpeed(Player.m_localPlayer.m_runSpeed + 1);
        public static void SpeedDown() => SetSpeed(Player.m_localPlayer.m_runSpeed - 1);

        public static void ToggleGhostMode()
        {
            if (!RequireGodMode("Ghost")) return;
            GhostMode = !GhostMode;
            Player.m_localPlayer.SetGhostMode(GhostMode);
            Show($"Ghost Mode {GhostMode}");
        }

        public static void SpawnCombatPet()
        {
            if (!RequireGodMode("Spawn Combat Pet")) return;
            var rnd = new System.Random();
            string prefab = combatPets[rnd.Next(combatPets.Length)];
            GameObject p = ZNetScene.instance.GetPrefab(prefab);
            if (p == null) { Show($"Missing prefab: {prefab}"); return; }

            Vector3 pos = Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f;
            var inst = UnityEngine.Object.Instantiate(p, pos, Quaternion.identity);
            var ch = inst.GetComponent<Character>();
            ch.SetLevel(3);
            ch.GetComponent<Character>().SetMaxHealth(10000);
            ch.GetComponent<Character>().SetHealth(10000);
            ch.GetComponent<MonsterAI>().SetFollowTarget(Player.m_localPlayer.gameObject);
            ch.m_name = petNames[rnd.Next(petNames.Length)];
            //tame it
            Tameable.TameAllInArea(Player.m_localPlayer.transform.position, 30.0f);

            Show($"Spawned pet: {ch.m_name}");
        }

        public static void TameAll()
        {
            if (!RequireGodMode("Tame all")) return;
            Tameable.TameAllInArea(Player.m_localPlayer.transform.position, 30f);
            Show("All nearby tamed");
        }

        public static void ReplenishStacks()
        {
            if (!RequireGodMode("Replenish")) return;
            foreach (var item in Player.m_localPlayer.GetInventory().GetAllItems())
            {
                if (item.m_shared.m_maxStackSize > 1)
                {
                    item.m_stack = item.m_shared.m_maxStackSize;
                    item.m_shared.m_equipDuration = 1000f;
                }
            }

            Show("Stacks replenished");
        }

        // dmg only goes up, never dpwn haha
        public static void ApplySuperWeapon()
        {
            // Apply current damage counter to equipped weapons
            foreach (var item in Player.m_localPlayer.GetInventory().GetEquippedItems())
            {
                if (!item.IsWeapon()) continue;
                // Get base damage types
                var baseDamages = item.GetDamage();
                var updated = new HitData.DamageTypes
                {
                    m_slash = DamageCounter * 10,
                    m_blunt = DamageCounter * 10,
                    m_pierce = DamageCounter * 10,
                    m_frost =  DamageCounter * 10,
                    m_lightning = DamageCounter * 10,
                    m_poison = DamageCounter * 10,
                    m_spirit = DamageCounter * 10
                };
                item.m_shared.m_damages = updated;
            }
            Show($"Applied {DamageCounter} damage counters to weapons");
        }

        // Guardian's Gift: major buff including Renewal
        public static void GuardianGift()
        {
            GiftActive = true; // can't disable
            if (!RequireGodMode("Guardian's Gift")) return;
            ToggleRenewal();
            var p = Player.m_localPlayer;
            // Boost stats
            float regen = 100f, fallDmg = 0.1f, noise = 1f;
            p.GetSEMan().ModifyHealthRegen(ref regen);
            p.GetSEMan().ModifyFallDamage(1, ref fallDmg);
            p.GetSEMan().ModifyNoise(1, ref noise);
            p.m_blockStaminaDrain = 0.1f;
            p.m_runStaminaDrain = 0.1f;
            p.m_staminaRegen = 50f;
            p.m_staminaRegenDelay = 0.5f;
            p.m_eiterRegen = 50f;
            p.m_eitrRegenDelay = 0.1f;
            p.m_maxCarryWeight = 99999f;
            // Durability
            foreach (var item in p.GetInventory().GetEquippedItems())
            {
                item.m_shared.m_durabilityDrain = 0.1f;
                item.m_shared.m_maxDurability = 10000f;
                item.m_durability = 10000f;
            }
            List<Player.Food> foods = p.GetFoods();
            foreach (var food in foods)
            {
                food.m_time = 10000f;
            }
            Show("Guardian's Gift activated");
        }

        public static void Invigorate()
        {
            var p = Player.m_localPlayer;
            p.Heal(p.GetMaxHealth() - p.GetHealth(), true);
            p.AddStamina(p.GetMaxStamina());
            p.AddEitr(p.GetMaxEitr() - p.GetEitr());

            // Remove bad effects
            //
            // TODO: Does this only remove bad ones?
            List<StatusEffect> effects = p.GetSEMan().GetStatusEffects();
            foreach (StatusEffect st in effects)
            {
                p.GetSEMan().RemoveStatusEffect(st);
            }

            // Add beneficial effects
//          p.GetSEMan().AddStatusEffect(new SE_Rested(), resetTime: true, 10, 10); //this will add lvl1: 8mins
//          p.GetSEMan().AddStatusEffect(new SE_Shield(), resetTime: true, 10, 10);
        }

        public static void AoeRegen()
        {
            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 30.0f, list);

            foreach (Character entity in list)
            {
                if ((entity.IsPlayer() || entity.IsTamed()) && entity.GetHoverName() != Player.m_localPlayer.GetHoverName() && entity.GetHealthPercentage() < 0.75f)
                {
                    Show(entity.GetHoverName() + " at " + Math.Floor(entity.GetHealthPercentage() * 100) + "%. Healing.");
                    entity.Heal(25.0f, false); // don't show it.
                }
            }
        }

        public static void CastHealAOE()
        {
            if (!RequireGodMode("AoE heal")) return;
            var prefab = ZNetScene.instance.GetPrefab("DvergerStaffHeal_aoe");
            if (prefab == null) { Show("Missing heal prefab"); return; }
            UnityEngine.Object.Instantiate(prefab,
                Player.m_localPlayer.transform.position + Vector3.up,
                Quaternion.identity);
            Show("Heal AOE cast");
        }

        public static void CastDmgAOE()
        {
            GameObject prefab2 = ZNetScene.instance.GetPrefab("aoe_nova");

            if (!prefab2)
            {
                Show("Missing object aoe_nova");
            }
            else
            {
                Vector3 vector = UnityEngine.Random.insideUnitSphere;

                Show("Spawning Nova!");
                GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 4f + Vector3.up + vector, Quaternion.identity);
                ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
            }
        }

        public static void KillAllMonsters()
        {
            if (!RequireGodMode("Kill all monsters")) return;
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 20f, list);
            int killed = 0;
            foreach (var c in list)
                if (!c.IsPlayer() && !c.IsTamed())
                {
                    c.Damage(new HitData { m_damage = { m_damage = 1e10f } });
                    killed++;
                }
            Show($"Monsters killed: {killed}");
        }

        public static void TeleportSolo()
        {
            if (!RequireGodMode("Teleport")) return;
            // use the old minimap-based conversion
            var pos = Utils.ScreenToWorldPoint(Input.mousePosition);
            Player.m_localPlayer.TeleportTo(pos, Player.m_localPlayer.transform.rotation, true);
            Show($"Teleported to {pos}");
        }

        public static void TeleportMass()
        {
            if (!RequireGodMode("Mass Teleport")) return;
            var pos = Utils.ScreenToWorldPoint(Input.mousePosition);
            foreach (var pl in PlayerUtility.GetNearbyPlayers(50f))
                Chat.instance.TeleportPlayer(pl.GetZDOID().UserID, pos, Quaternion.identity, true);
            Show("Mass teleport executed");
        }

        public static void TeleportHome()
        {
            if (!RequireGodMode("Gate")) return;
            var dst = TeleportUtils.GetSpawnPoint();
            Player.m_localPlayer.TeleportTo(dst, Quaternion.identity, true);
            Show("Teleported home");
        }

        public static void TeleportSafe()
        {
            if (!RequireGodMode("Succor")) return;
            var pins = Minimap.m_pins;
            foreach (var pin in pins)
                if (pin.m_name.Equals("safe", StringComparison.OrdinalIgnoreCase))
                {
                    var dst = pin.m_pos + UnityEngine.Random.insideUnitSphere * 5f;
                    Player.m_localPlayer.TeleportTo(dst, Quaternion.identity, true);
                    Show("Teleported to safe spot");
                    return;
                }
            Show("No safe pin found");
        }

        // Helpers
        private static void SlowMonsters()
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 30f, list);
            foreach (var c in list)
                if (c.IsMonsterFaction(10))
                    c.m_runSpeed = c.m_speed = 1f;
        }

        private static void DamageAoE()
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 5f, list);
            foreach (var c in list)
                if (!c.IsPlayer() && c.IsMonsterFaction(10))
                    c.Damage(new HitData { m_damage = { m_damage = 75f } });
        }

        private static void SetSpeed(float speed)
        {
            var p = Player.m_localPlayer;
            p.m_runSpeed = p.m_walkSpeed = speed;
            Show($"Speed set: {speed}");
        }

        private static void Show(string msg)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, msg);
            Console.instance.Print(msg);
        }
    }

    // UI manager for tracking and modes windows
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        private bool visible;
        private Rect trackWindow = new Rect(150, Screen.height - 250, 250, 150);
        const float modeWidth = 350f;
        const float modeHeight = 550f;
        private Rect modeWindow = new Rect(
            Screen.width - modeWidth,  // x
            Screen.height - modeHeight - 10f, // y (10px margin from bottom, for example)
            modeWidth,                  // width
            modeHeight                  // height
        );
        private Rect actionWindow = new Rect(Screen.width - modeWidth, 460, 300, 200);

        void Awake() => Instance = this;

        public void ToggleVisible() => visible = !visible;

        void OnGUI()
        {
            // stash old color
            var oldColor = GUI.contentColor;

            // if cheats are visible ⇒ green, else keep the default
            GUI.contentColor = visible
                ? Color.green
                : oldColor;

//            GUI.Label(new Rect(10, 3, 200, 25), $"Cheats Active: {visible}");

            // restore
            GUI.contentColor = oldColor;

            if (!visible) return;

            //trackWindow = GUILayout.Window(0, trackWindow, DrawTracking, "Tracking");
            trackWindow = GUILayout.Window(
                0,
                trackWindow,
                DrawTracking,
                "Tracking",
                GUILayout.Width(300)
            );
            modeWindow = GUILayout.Window(
                1,
                modeWindow,
                DrawModes,
                "Modes",
                GUILayout.MinWidth(modeWidth)
            );
        }

        void DrawTracking(int id)
        {
            var player = Player.m_localPlayer;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 50f, list);

            foreach (var c in list)
            {
                if (!c.IsPlayer())
                {
                    float dist = Utils.DistanceXZ(c.transform.position, player.transform.position);
                    float hpPct = c.GetHealthPercentage() * 100f;
                    GUILayout.Label($"{c.GetHoverName()}: {dist:0.0}m ({hpPct:0.0}% HP)");
                }
            }

            GUI.DragWindow();
        }

        void DrawModes(int id)
        {
            var oldColor = GUI.contentColor;
            const float labelW = 180f;

            foreach (var cmd in CommandRegistry.All)
            {
                bool hasState = cmd.GetState != null;
                bool isOn = hasState && cmd.GetState();

                GUILayout.BeginHorizontal();

                // 1) description in white
                GUI.contentColor = Color.white;
                GUILayout.Label(cmd.Description, GUILayout.ExpandWidth(false));

                // 2) key in cyan, right after description
                GUI.contentColor = Color.cyan;
                GUILayout.Label($"({cmd.Key})", GUILayout.ExpandWidth(false));

                // 3) push value to the right
                GUILayout.FlexibleSpace();

                // 4) ON/OFF only if you have a state-getter
                if (hasState)
                {
                    GUI.contentColor = isOn ? Color.green : Color.red;
                    GUILayout.Label(isOn ? "ON" : "OFF", GUILayout.ExpandWidth(false));
                }

                GUILayout.EndHorizontal();
            }

            GUI.contentColor = oldColor;
            GUI.DragWindow();
        }
    }

    // Boss configuration and reveal logic
    public static class BossData
    {
        public static readonly (string prefab, string name)[] All = {
            ("Eikthyrnir","Eikthyr"),("GDKing","The Elder"),("Bonemass","Bonemass"),
            ("Dragonqueen","Moder"),("GoblinKing","Yagluth"),("FaderLocation","Fader")
        };
        public static void Reveal((string prefab, string name) b)
        {
            Game.instance.DiscoverClosestLocation(
                b.prefab,
                Player.m_localPlayer.transform.position,
                b.name,
                (int)Minimap.PinType.Boss);
        }
    }

    // Utility helpers for teleporting and input conversion
    static class TeleportUtils
    {
        public static Vector3 GetSpawnPoint()
        {
            var profile = Game.instance.GetPlayerProfile();
            return profile.HaveCustomSpawnPoint() ? profile.GetCustomSpawnPoint() : profile.GetHomePoint();
        }
    }

    static class Utils
    {
        public static Vector3 MouseToWorldPoint()
        {
            var mp = Input.mousePosition;
            // convert via Minimap; simplified here
            return Camera.main.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 10f));
        }

        // Converts screen/mouse position to world via the minimap logic
        public static Vector3 ScreenToWorldPoint(Vector3 mousePos)
        {
            Vector2 screenPoint = mousePos;
            RectTransform rectTransform = Minimap.instance.m_mapImageLarge.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out var localPoint))
            {
                Vector2 vector = Rect.PointToNormalized(rectTransform.rect, localPoint);
                Rect uvRect = Minimap.instance.m_mapImageLarge.uvRect;
                float mx = uvRect.xMin + vector.x * uvRect.width;
                float my = uvRect.yMin + vector.y * uvRect.height;
                return MapPointToWorld(mx, my);
            }
            return Vector3.zero;
        }

        private static Vector3 MapPointToWorld(float mx, float my)
        {
            int half = Minimap.instance.m_textureSize / 2;
            mx = (mx * Minimap.instance.m_textureSize - half) * Minimap.instance.m_pixelSize;
            my = (my * Minimap.instance.m_textureSize - half) * Minimap.instance.m_pixelSize;
            return new Vector3(mx, 0f, my);
        }

        // Distance in XZ plane between two points
        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }

    static class PlayerUtility
    {
        public static IEnumerable<Player> GetNearbyPlayers(float radius)
        {
            var list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, radius, list);
            foreach (var c in list)
                if (c.IsPlayer()) yield return c as Player;
        }
    }

    // -------------------- OLD ----------------------------------------------------------

    //TODO:
    // Load these commands from command line (some known command) instead of about menu?
    public class DiscoverThings : MonoBehaviour
    {
        private InputManager inputManager;


        // Main player reference. Not really needed. Just access Player.m_localPlayer...
        Player player;

        // Player states
        public static bool godMode = false;
        public static bool playerMode = false; // semi-normal player
        public static bool buildMode = false; // no placement cost.
        public static bool gmMode = false; // = invis. Can spawn things.
 
        public static bool noBuildCost = false;
        public static int godPower = 0;
        public static bool godWeapon = false;
        public static bool renewal = false;       //heal friends  
        public static bool cloakOfFlames = false; //fire pbaoe
        public static bool melodicBinding = false; //Slow movement + atk speed
        public static bool ghostMode = false;
        public static bool RandomEvent = false;

        // Bosses. Affected by restore-state.
        public static bool eikthyr_killed = false;
        public static bool theelder_killed = false;
        public static bool bonemass_killed = false;
        public static bool moder_killed = false;
        public static bool yagluth_killed = false;
        public static bool queen_killed = false;
        public static bool fader_killed = false;


        // Counters
        UInt16 counter = 0;
        UInt16 damageCounters = 10;

        // UI
        private Rect MainWindow;
        private Rect StatusWindow;
        public bool visible = false;

        // Time (register time when loading into a game, and then at various stages, like first boss Eikthyr).
        DateTime time = System.DateTime.Now;
        Stopwatch timer = new Stopwatch();
      
        private void Awake()
        {
            Console.instance.Print("Awake..");

            // Configure inputman
            inputManager = new InputManager();
            inputManager.Register(KeyCode.LeftArrow, () => CheatCommands.ToggleGodMode());

            // Use static function. Pass in things like Player, etc.
            Helpers.Test();
        }

        private void Start()
        {
            Console.instance.Print("Start..");

            MainWindow = new Rect(150.0f, Screen.height - 250f, 250f, 150f);

            float center_x = (Screen.width  / 2) - (250 / 2);
            float center_y = (Screen.height / 2) - (320 / 2);

            StatusWindow = new Rect(Screen.width - 220f, Screen.height - 500f, 220f, 350f);

            timer.Start();
            Player.m_localPlayer.m_onDamaged += somefunc;
        }

        private float w1 = 140f;
        private float w2 = 40f;
        private void AddHorizontalGridLine(string text, bool on = false, bool notext = false)
        {
            GUILayout.BeginHorizontal();
            GUI.contentColor = Color.white;
            GUILayout.Label(text, GUILayout.Width(w1));
            GUI.contentColor = on ? Color.green : Color.red;
            GUILayout.Label(notext? "" : on.ToString(), GUILayout.Width(w2));
            GUILayout.EndHorizontal();
        }

        // Links:
        // https://answers.unity.com/questions/702499/what-is-a-guilayoutoption.html
        // https://answers.unity.com/questions/12108/something-like-guitable-or-a-grid.html 
        public void RenderModesUI(int id)
        {
            //To try: BeginArea /EndArea ?
            //float w1 = 120f;
            //float w2 = 40f;

            //todo: use player.getGodMode() instead of relying on internal variable.

            // ---------------------
            // States:
            // 
            //   Invis -> Spawner: Spawn x, y, z, tame
            //   God -> Builder: Replenish Stacks. Kill all. Show bosses. Explore all. AoE heal. Teleport.
            //  
            //  Always enabled (Player - arrow keys):
            // 
            //    Boost weapon
            //    Run speed +/- (up down)
            //    Cof
            //    Melodic binding
            //    Change power
            //
            // Where to put keys? Keep them in the same groups. But only allow them if certain mopde is enabled?
            //
            //
            // TODO: First thing to do is move every function to CheatCommands and hook them into InputManager config.
            // 
            // --------------------------
            AddHorizontalGridLine("Runspeed (F3/F4)");
            AddHorizontalGridLine("Stats + Renewal song (F8)",      renewal);
            AddHorizontalGridLine("God/No Cost",               Player.m_localPlayer.InGodMode());
            AddHorizontalGridLine("Show Bosses (F10)");
            AddHorizontalGridLine("Weapon++ (F11)",                 godWeapon);
            AddHorizontalGridLine("Replenish stacks (F12)");
            AddHorizontalGridLine("Invigorate/Kill (Up/Down)");
            AddHorizontalGridLine("aoe Heal/God (Left/Right)");
            AddHorizontalGridLine("Ghost (9)", Player.m_localPlayer.InGhostMode());
            AddHorizontalGridLine("Cloak Of Flames (0)", cloakOfFlames);
            AddHorizontalGridLine("Melodic binding (B)",            melodicBinding);
            AddHorizontalGridLine("Port (Ins, Del, Home, End)");
            // Spawn
            AddHorizontalGridLine("(Re)tame (PageUp)");
            AddHorizontalGridLine("Sp. pet (Pause)");
            AddHorizontalGridLine("Sp. Dvergr (Backsp)");
            AddHorizontalGridLine("Sp. Seekers (Z)");
            AddHorizontalGridLine("Sp. from rad. (LAlt)");

            GUI.DragWindow();
        }

        public void RenderUI(int id)
        {
            // Tracking UI
            GUILayout.Space(1f);

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, list);

            //Unsorted
            foreach (Character item in list)
            {
                if (!item.IsPlayer() && !item.IsTamed())
                {
                    float distance = Utils.DistanceXZ(item.transform.position, Player.m_localPlayer.transform.localPosition);
                    GUILayout.Label(item.GetHoverName().ToString() + ": " + distance.ToString("0.0") + "m (lvl " + item.GetLevel() + " - " + Math.Floor(item.GetHealth()) + " hp /" + Math.Floor((item.GetHealthPercentage()*100)) + "%)", new GUILayoutOption[0]);

                }
            }
            list = Character.GetAllCharacters();
            foreach (Character item in list)
            {
                if (!item.IsPlayer() && !item.IsTamed())
                {
//                    item.gameObject.GetComponent<Character>()?.

                }
            }

            GUI.DragWindow();
        }

        public void somefunc(float dmg, Character c)
        {
            Player.m_localPlayer.Message(
                 MessageHud.MessageType.Center,
                 c.m_name.ToString() + " took " + dmg.ToString() + " dmg.");
        }

        bool saved = true; //don't save
        private void OnGUI()
        {
            int state = 0;
            PlayerProfile.PlayerStats player_stats = Game.instance.GetPlayerProfile().m_playerStats;
            Dictionary<PlayerStatType, float> stats = player_stats.m_stats;

            float builds = stats[PlayerStatType.Builds];
            float deaths = stats[PlayerStatType.Deaths];
            float crafts = stats[PlayerStatType.CraftsOrUpgrades];
            float bosses = stats[PlayerStatType.BossKills];
            String keys = String.Join(", ", Player.m_localPlayer.GetUniqueKeys());

            // Get state: Bosskills.
            float newstate = Game.instance.GetPlayerProfile().m_playerStats.m_stats.GetValueOrDefault(PlayerStatType.PlayerKills);

            // GUI.Label(new Rect(10, 3, 1000, 80), "Everheim v.0.1.  Death: " + deaths + "  Crafts: " + crafts + "  Builds: " + builds + "  Bosses: " + bosses + " State: " + state + "  time: " + timer.Elapsed.ToString(@"m\:ss\.fff") + "  Boss keys: " + keys);
            GUI.Label(new Rect(10, 3, 1000, 80), " State: " + newstate + "  time: " + timer.Elapsed.ToString(@"m\:ss\.fff") );

            if (!visible)
                return;


            MainWindow = GUILayout.Window(0, MainWindow, new GUI.WindowFunction(RenderUI), "Tracking", new GUILayoutOption[0]);
            StatusWindow = GUILayout.Window(1, StatusWindow, new GUI.WindowFunction(RenderModesUI), "Modes", new GUILayoutOption[0]);

            //TODO: Add another window here to show state of configuration (gm mode, etc)

            /*
            if(!saved)
            { 
                ShowULMsg("Saving state: " + state);
                // Temp: just try to save something here.
                int fungi = 0x1;
                int boost = 0x2;
                int spell_companion = 0x4;
                int spell_tameall = 0x8;
                int spell_killall = 0x10; //0x20, 0x40, 0x60 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000

                int active = fungi | boost | spell_companion | spell_killall | spell_tameall;
                bool fungi_equipped = (active & fungi >> 3) != 0;
                //Test if we can affect stats across game sessions. Are they saved on Save()? Then reload this state when new session starts.
                Game.instance.GetPlayerProfile().m_playerStats.m_kills = active;
                Game.instance.GetPlayerProfile().Save();

                saved = true;
            }*/
        }

        // All the logging
        // TODO: Move to helper.
        private void ShowULMsg(string text)
        {
            Player.m_localPlayer.Message(
                MessageHud.MessageType.TopLeft,
                text);

            Player.m_localPlayer.Message(
                MessageHud.MessageType.Center,
                text);

            Console.instance.Print(text);
        }

        //todo: move all these to header / Helper.
        readonly List<Tuple<string, string>> bossNames = new List<Tuple<string, string>>
        {
            Tuple.Create<string, string>("Eikthyrnir","Eikthyr"),
            Tuple.Create<string, string>("GDKing","The Elder"),
            Tuple.Create<string, string>("Bonemass","Bonemass"),
            Tuple.Create<string, string>("Dragonqueen","Moder"),
            Tuple.Create<string, string>("SeekerQueen","Seeker Queen"),
            Tuple.Create<string, string>("Seekerqueen","Seeker Queen"),
            Tuple.Create<string, string>("Mistlands_DvergrBossEntrance1","Seeker Queen"),
            Tuple.Create<string, string>("GoblinKing","Yagluth"),
            Tuple.Create<string, string>("FaderLocation","Fader"),
            Tuple.Create<string, string>("BogWitch","Bog Witch"),
            Tuple.Create<string, string>("BogWitch_camp","Bog Witch")
        };

        readonly List<Tuple<string, string>> dvergr_prefabs = new List<Tuple<string, string>>
        {
            //Seeker soldier? Etc.
 //           Tuple.Create<string, string>("Seeker", "Seeker"),
            Tuple.Create<string, string>("Dvergr mage",             "DvergerMage"),
            Tuple.Create<string, string>("Dvergr mage (fire)",      "DvergerMageFire"),
            Tuple.Create<string, string>("Dvergr mage (ice)",       "DvergerMageIce"),
            Tuple.Create<string, string>("Dvergr mage (support)",   "DvergerMageSupport"),
            Tuple.Create<string, string>("Dvergr mage (fire)",      "DvergerMageFire"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
        };

        readonly List<Tuple<string, string>> seeker_prefabs = new List<Tuple<string, string>>
        {
            //Seeker soldier? Etc.
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker",                  "Seeker"),
            Tuple.Create<string, string>("Seeker Soldier",          "SeekerBrute"),
        };
        //todo: for infested mine
        readonly List<Tuple<string, string>> infested_prefabs = new List<Tuple<string, string>>
        {
            Tuple.Create<string, string>("Tick",             "Tick"),
            Tuple.Create<string, string>("Seeker",           "Seeker"),
            Tuple.Create<string, string>("Dvergr mage (ice)",       "DvergerMageIce"),
            Tuple.Create<string, string>("Dvergr mage (support)",   "DvergerMageSupport"),
            Tuple.Create<string, string>("Dvergr mage (fire)",      "DvergerMageFire"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
            Tuple.Create<string, string>("Dvergr",                  "Dverger"),
        };

        List<string> all_pets = new List<string> { "Boar", "Wolf", "Lox", "Hen", "Skeleton_Friendly" };
        List<string> combat_pets = new List<string> { "Wolf", "DvergerMageSupport" };

        List<string> pet_names = new List<string> {
                    "Bob",
                    "Ralf",
                    "Liam",
                    "Olivia",
                    "Elijah",
                    "James",
                    "William",
                    "Benjamin",
                    "Lucas",
                    "Henry",
                    "Theodore",
                    "Emma",
                    "Charlotte",
                    "Amelia",
                    "Sophia",
                    "Isabella",
                    "Mia",
                    "Evelyn",
                    "Harper",
                    "Fluffy",
                    "Max",
                    "Sugar",
                    "Cash",
                    "Dusty",
                    "Spirit",
                    "Chief",
                    "Sunshine",
                    "Cisco",
                    "Dakota",
                    "Dash",
                    "Magic",
                    "Rebel",
                    "Willow",
                    "Lucky",
                    "Gypsy",
                    "Scout",
                    "Cinnamon",
                    "Toffee",
                    "Ebony",
                    "Onyx",
                    "Chocolate",
                    "Rusty",
                    "Copper",
                    "Brownie",
                    "Noir",
                    "Pepper",
                    "Napoleon",
                    "Dolly",
                    "Lucy",
                    "Princess",
                    "Annie",
                    "Sheriff",
                    "Buckeye",
                    "Merlin",
                    "Amigo"
                };



        //TODO: Sort these functions and also turn some into helpers
        //      - e.g. mass teleport should be a helper so it can be done to
        //      mouse cursor OR safe spot
        //      Consider some sort of state machine?

        // todo: Create a map of key to functionname, to shorten the "Update" function.

        // --------------------------------------------------------
        // F1: UI
        // F2: 
        // F3: Run faster
        // F4: Run slower
        // F5:
        // F6: God power
        // F7: Exp[lore ALL!
        // F8: Boost/regen
        // F9:
        // F10: Find bosses
        // F11: WEAPON++
        // F12: Replenish stacks
        //
        // Up/Down: Invigorate / 
        // Left:  AOE Heal
        // Right: God mode
        // 
        // LAlt: Clone
        //
        // ---- My favorites ------------
        // Runspeed
        // Clone
        // Boost
        // Replenish stacks
        //
        //todo:  foreach input in inputs.
        //       if(Input.GetKeyDown(key.item1)
        //            execute key.item2

        private void Update()
        {
            // Only these two lines
            inputManager.HandleInput();
            CheatCommands.HandlePeriodic();


            // ----------------------------------
            // TICK
            // ----------------------------------
            counter++; // wrap is fine. Use Stopwatch instead?

            // --------------------------------
            // Refresh player todo:  not required, just access Player.
            //
            player = Player.m_localPlayer;

            // ----------------------------------------------
            // F1: ShowUI
            // ----------------------------------------------
            //
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ShowULMsg("UI Active (Track, Modes)");
                visible = !visible; //Bad name
            }

            // ----------------------------------------------
            // F10: Reveal bosses
            // ----------------------------------------------
            //
            if (Input.GetKeyDown(KeyCode.F10))
            {
                FindBosses();
            }

            // ----------------------------------------------
            // Invigorate!
            // ----------------------------------------------
            //  Heal, remove debuffs, add rested
            if (Input.GetKeyDown( KeyCode.F3))
            {
                Invigorate();
                ShowULMsg("Invigorated!");
            }
            // F7: Explore ALL
            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (godMode)
                {
                    Minimap.instance.ExploreAll();
                }
            }
            
            // -----------------------------------
            // Toggle GM mode
            //
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                godMode = !godMode;
                Player.m_localPlayer.SetGodMode(godMode);
                player.SetGodMode(godMode);
                player.m_boss = godMode; // ??

                //no build cost
                Player.m_localPlayer.SetNoPlacementCost(value: godMode);
                
                ShowULMsg(godMode ? "You're a god!" : "You're just a player.");
            }

            // --------------------------------------------
            // Toggle Guardian power
            //
            if (Input.GetKeyDown(KeyCode.F6))
            {
                string[] gods = { "GP_Eikthyr", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Fader" };
                Player.m_localPlayer.SetGuardianPower(gods[godPower]);

                if ( godPower == gods.Length-1 )
                {
                    godPower = 0;
                }
                else
                {
                    godPower++;
                }
            }

            // ----------------------------------------------------------------------------------------
            // Spawn pets.
            // TODO: Spawn a trio. Timon (boar), Misha (bear), some wolf
            // TODO: Spawn a bit off the side.
            //
            int pet_counter = 0;
            if (Input.GetKeyDown(KeyCode.Pause))
            {
                if (godMode)
                {
                    //player.ToggleDebugFly();

                    int count = 1;

                    DateTime now = DateTime.Now;
                    Random rand = new Random();

                    int pet_index = rand.Next(combat_pets.Count);
                    int name_index = rand.Next(pet_names.Count);

                    GameObject prefab2 = ZNetScene.instance.GetPrefab(combat_pets[pet_index]);

                    if (!prefab2)
                    {
                        ShowULMsg("Missing object" + combat_pets[pet_index] + " (" + pet_index + ")");
                    }
                    else
                    {
                        Vector3 vector = UnityEngine.Random.insideUnitSphere * ((count == 1) ? 0f : 0.5f);

                        ShowULMsg("Spawning fast pet: " + combat_pets[pet_index] + " (" + pet_index + ", " + pet_names[name_index] + ")");
                        GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f + Vector3.up + vector, Quaternion.identity);
                        ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                        gameObject2.GetComponent<Character>()?.SetLevel(3); //1 = 0, 2 = 1, 3 = 2 stars
                        gameObject2.GetComponent<Character>().m_name = pet_names[name_index];
                        gameObject2.GetComponent<Character>().SetMaxHealth(10000);
                        gameObject2.GetComponent<Character>().SetHealth(10000);
                        gameObject2.GetComponent<MonsterAI>().SetFollowTarget(player.gameObject);
                        //tame it - if possible
                        Tameable.TameAllInArea(player.transform.position, 30.0f);

                        //Set Speed
                        List<Character> list = new List<Character>();
                        Character.GetCharactersInRange(player.transform.position, 30.0f, list);

                        // Set speed
                        foreach (Character item in list)
                        {
                            if (!item.IsMonsterFaction(10) && item != player) // This includes pets
                            {
                                item.m_runSpeed = 14;
                                item.m_speed = 14;
                            }
                        }
                    }

                    pet_counter++;
                }
                else
                {
                    ShowULMsg("Not a god");
                }
            }

            // Test other player modification
            //
/*            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, list);

                foreach(var pl in list)
                {
                    if(pl != player)
                    { 
                        List<Player.Food> foods = pl.GetComponent<Player>().GetFoods();
                        foreach(Player.Food food in foods)
                        {
                            food.m_time = 10000f;
                        }
                    }
                }

                ShowULMsg("Testing changing player food");
            }*/

            // ----------------------------------------------------------------
            // 9: Ghostmode
            //
            if (Input.GetKeyDown(KeyCode.Alpha9)/* && !Console.IsVisible()*/)
            {
                ghostMode = !ghostMode;
                player.SetGhostMode(ghostMode);
                ShowULMsg("GhostMode: " + ghostMode);
            }

            // ---------------------------------------------------
            // Spawn Event
            // ---------------------------------------------------
            /*            if(Input.GetKeyDown(KeyCode.Alpha0))
                        {
                            RandomEvent = !RandomEvent;

                            if(!RandomEvent)
                            {
                                RandEventSystem.instance.ResetRandomEvent();
                                ShowULMsg("Stopping random event");
                            }
                            else
                            {
                                RandEventSystem.instance.StartRandomEvent();
                                ShowULMsg("Spawning random event");
                            }
                        }
            */


            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                cloakOfFlames = !cloakOfFlames;
                ShowULMsg("CloakOfFlames: " + cloakOfFlames);
            }

            if (Input.GetKeyDown(KeyCode.B) && !Console.IsVisible())
            {
                melodicBinding = !melodicBinding;
                ShowULMsg("MelodicBinding: " + melodicBinding);
            }

            // TODO: Only allow spawning in GhostMode(). Use that as an "alt" key. Or use "State" to toggle.
            //       Print out current command list on screen.

            // ----------------------------
            // Z: Spawn Seeker
            // 
            if (player.InGhostMode() && Input.GetKeyDown(KeyCode.Z)/* && !Console.IsVisible()*/)
            {
                int[] chance = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2 };
                Random rand = new Random();
                Tuple<string, string> prefab = seeker_prefabs[rand.Next(0, seeker_prefabs.Count)];
                GameObject prefab2 = ZNetScene.instance.GetPrefab(prefab.Item2);
                Console.instance.Print("Got: " + prefab.Item2);
                if (!prefab2)
                {
                    ShowULMsg("Missing object: " + prefab2.name);
                }
                else
                {
                    Vector3 vector = UnityEngine.Random.insideUnitSphere; //10 times unit vector in my direction
                    GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 10f + Vector3.up + vector, Quaternion.identity);
                    ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                    gameObject2.GetComponent<Character>()?.SetLevel(chance[rand.Next(0, chance.Length+1)]); //Set level 0-1
                    ShowULMsg("Spawning " + prefab2.name);
                }

            }

            // ---------------------------------------------------
            // Backspace: "Spawn aggrevated" Dvergr (in ghost mode). Move to utility that takes "friend,foe".
            //
            //todo: Turn this into a function that takes a prefab list and a chance list.
            if (player.InGhostMode() && Input.GetKeyDown(KeyCode.Backspace) && !Console.IsVisible())
            {
                int[] chance = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2 };
                Random rand = new Random();
                Tuple<string,string> prefab = dvergr_prefabs[rand.Next(0, dvergr_prefabs.Count)];
                GameObject prefab2 = ZNetScene.instance.GetPrefab(prefab.Item2);
                Console.instance.Print("Got: " + prefab.Item2);
                if (!prefab2)
                {
                    ShowULMsg("Missing object: " + prefab2.name);
                }
                else
                {
                    Vector3 vector = UnityEngine.Random.insideUnitSphere; //10 times unit vector in my direction
                    GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 10f + Vector3.up + vector, Quaternion.identity);
                    ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                    gameObject2.GetComponent<Character>()?.SetLevel(chance[rand.Next(0, 15)]); //Set level 0-1
                    gameObject2.GetComponent<MonsterAI>()?.SetAggravated(true, BaseAI.AggravatedReason.Theif);
                    ShowULMsg("Spawning " + prefab2.name);
                }
            }

            // ---------------------------------------------------------------------------------
            // Clone++, some distance away
            // todo: Do something with biome dependent spawning. player.m_currentBiome
            // todo: e,g, in mistlands - we could be spawning a monster into a dverger camp! :(
            // todo: Could we just have the /spawn command?
            // todo: only works for something like "seeker" where hovername = prefabname. e.g. only monsters. hare, wolf, seeker, gjall, tick. Need a tiple that matches the hovername to the prefab name.
            //       does not work for something like "derger mage".
            // todo:  maybe do [0,0,0,0,0,0,1 as a way to do a weighted randomization.
            // 
            if (Input.GetKeyDown(KeyCode.LeftAlt))
            {
                int[] chance = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2 };
                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, list);
                list.Remove(player);
                Console.instance.Print("Found " + list.Count);

                Random rand = new Random();
                Character c = list[rand.Next(0, list.Count)];
                //                GameObject prefab2 = ZNetScene.instance.GetPrefab("Seeker");
                GameObject prefab2 = ZNetScene.instance.GetPrefab(c.GetHoverName());

                if (!prefab2)
                {
                    ShowULMsg("Missing object: " + prefab2.name);
                }
                else
                {
                    Vector3 vector = UnityEngine.Random.insideUnitSphere; //10 times unit vector in my direction
                    GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 10f + Vector3.up + vector, Quaternion.identity);
                    ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                    gameObject2.GetComponent<Character>()?.SetLevel(chance[rand.Next(0, 15)]); //Set level 0-1
                    ShowULMsg("Spawning " + prefab2.name);
                }
            }

            // Play around with payment
            // The goal is to get enough money to buy something from a vendor to win the game. Maybe from Hildir?
            if (Input.GetKeyDown(KeyCode.Keypad7))
            {
                ShowULMsg("Keypad 1 - Give Coin, AncientSeed, Withered bone, Totems, Eggs.");

                GameObject coins = ZNetScene.instance.GetPrefab("Coins");
                GameObject seed = ZNetScene.instance.GetPrefab("AncientSeed");
                GameObject bone = ZNetScene.instance.GetPrefab("WitheredBone");
                GameObject totem = ZNetScene.instance.GetPrefab("GoblinTotem");
                GameObject egg = ZNetScene.instance.GetPrefab("DragonEgg");
                GameObject dvergrkey = ZNetScene.instance.GetPrefab("DvergrKey");
                GameObject firesword = ZNetScene.instance.GetPrefab("SwordDyrnwyn");

                // BOSS ITEMS
                //player.GetInventory().AddItem(seed, 5);
                //player.GetInventory().AddItem(bone, 5);
                //player.GetInventory().AddItem(totem, 5);

                //Eggs have to be given one at a time. 
                //player.GetInventory().AddItem(egg, 1);
                //player.GetInventory().AddItem(egg, 1);
                //player.GetInventory().AddItem(egg, 1);

                // Count current cash
                List<ItemDrop.ItemData> valuables = null;
                valuables = player.GetInventory().GetAllItems();

                int coins_amount = 0;
                int price = 20;
                foreach (ItemDrop.ItemData item in valuables)
                {
                    if (item.m_shared.m_name.Equals("$item_coins"))
                    {
                        //ShowULMsg(item.ToString() + " " + item.GetValue());
                        coins_amount = item.m_stack;
                    }
                }

                // Test: Pay 20 Gold
                // player.GetInventory().RemoveItem(coins_item_drop.m_itemData, 20); // does not work?
                ItemDrop coins_item_drop = coins.GetComponent<ItemDrop>();
                player.GetInventory().RemoveItem(coins_item_drop.m_itemData);
                player.GetInventory().AddItem(coins, coins_amount - price); // should leave us with 180
                ShowULMsg("We have " + coins_amount + ". Payting 20 gold!");

                //Crafted items = sealbreaker (queen) and bells (fader) = cost money.

                //foreach (String trophy in player.GetTrophies())
                //    ShowULMsg(trophy); //todo: this will list ALL the trophies that we've gotten. Search prefab list. Might be better than "unique keys".

                // todo: Make this a state machine instead

                List<ItemDrop.ItemData> items = player.GetInventory().GetAllItems();
                foreach (ItemDrop.ItemData item in items) //not very efficient
                {
                    // Reveal boss one by one?
                    if (!eikthyr_killed && item.m_shared.m_name.Equals("$item_trophy_eikthyr")) // Keep in list somewhere
                    {
                        ShowULMsg("You killed Eikthyr!"); //Set state as "bounty paid"
                        eikthyr_killed = true;

                        Console.instance.Print("You killed Eikthyr! You win a prize.");

                        // Get state
                        float state = Game.instance.GetPlayerProfile().m_playerStats.m_stats.GetValueOrDefault(PlayerStatType.PlayerKills);

                        // Save state
                        Game.instance.GetPlayerProfile().m_playerStats.m_stats.IncrementOrSet(PlayerStatType.PlayerKills, 1);
                        Game.instance.GetPlayerProfile().Save();
                        Console.instance.Print("State: " + Game.instance.GetPlayerProfile().m_playerStats.m_stats.GetValueOrDefault(PlayerStatType.PlayerKills));

                        //player.GetInventory().AddItem(coins, 200); //Dont need money.
                        //player.GetInventory().AddItem(seed, 5);
                        //todo: Add loot. and Message that you've received loot. Or, on trophy pickup spawn items.

                        // *********************************
                        // REWARDS
                        // *********************************

                        // Sword
                        player.GetInventory().AddItem(firesword, 1); // augment it? need to run through it again?
                        //ItemDrop.ItemData item

                        // Reveal next boss
                        ShowULMsg("Revealing: The Elder!");
                        Game.instance.DiscoverClosestLocation(
                            "GDKing",
                            Player.m_localPlayer.transform.position,
                            "The Elder",
                            (int)Minimap.PinType.Boss);

                        // Add some item to inventory. Change name and augment it.
                        // todo: If we're loading a world, restore rewards based on state.

                        // Add + to regen
                    }

                    //ShowULMsg(item.m_shared.m_name); //What's the name of elders head?
                    if (!theelder_killed && item.m_shared.m_name.Equals("$item_trophy_elder")) // Keep in list somewhere
                    {
                        // Save state
                        int state = 0x01;
                        state = 0x01 | 0x02;

                        Game.instance.GetPlayerProfile().m_playerStats.m_stats.IncrementOrSet(PlayerStatType.PlayerKills, state);

                        Game.instance.GetPlayerProfile().Save();
                        ShowULMsg("State: " + Game.instance.GetPlayerProfile().m_playerStats.m_stats.GetValueOrDefault(PlayerStatType.PlayerKills));

                        ShowULMsg("You got the elder head!"); //Set state as "bounty paid"
//                        player.GetInventory().AddItem(coins, 500);
                        player.GetInventory().AddItem(bone, 10);

                        theelder_killed = true;
                    }
                    /*
                                        if (item.m_shared.m_name.Equals("$item_trophy_bonemass")) // Keep in list somewhere
                                        {
                                            ShowULMsg("You got tbe Bonemass .. thing! Reward: Money + items for next boss"); //Set state as "bounty paid"
                                            player.GetInventory().AddItem(coins, 1000);
                                            player.GetInventory().AddItem(egg, 1);
                                            player.GetInventory().AddItem(egg, 1);
                                            player.GetInventory().AddItem(egg, 1);
                                        }
                                        if (item.m_shared.m_name.Equals("$item_trophy_dragonqueen")) // Keep in list somewhere
                                        {
                                            ShowULMsg("You got tbe Dragon .. thing! Reward: Money + items for next boss"); //Set state as "bounty paid"
                                            player.GetInventory().AddItem(coins, 1000);
                                            player.GetInventory().AddItem(totem, 5);
                                        }
                                        if (item.m_shared.m_name.Equals("$item_trophy_goblinking")) // Keep in list somewhere
                                        {
                                            ShowULMsg("You got tbe Goblin king! Reward: Money + items for next boss"); //Set state as "bounty paid"
                                            player.GetInventory().AddItem(coins, 2000);
                                            player.GetInventory().AddItem(dvergrkey, 5);
                                        }
                                        if (item.m_shared.m_name.Equals("$item_trophy_fader"))
                                        {
                                            ShowULMsg("You've completed the game. Time: " + timer.Elapsed.ToString(@"m\:ss\.fff"));
                     */

                    // $item_trophy_bonemass
                    // $item_trophy_goblinking
                    // $item_trophy_dragonqueen
                    // $item_trophy_seekerqueen
                    // $item_trophy_fader
                }
            }
            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                ShowULMsg("Keypad 2");
            }
            if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                ShowULMsg("Keypad 3");
            }
            if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                ShowULMsg("Keypad 4");
            }
            if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                ShowULMsg("Keypad 5");
            }
            if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                ShowULMsg("Keypad 6");
            }
            if (Input.GetKeyDown(KeyCode.Keypad7))
            {
                ShowULMsg("Keypad 7");
            }
            if (Input.GetKeyDown(KeyCode.Keypad8))
            {
                ShowULMsg("Keypad 8");
            }
            if (Input.GetKeyDown(KeyCode.Keypad9))
            {
                ShowULMsg("Keypad 9");
            }
            // ---------------------------------------------------
            // PGUP: Tame animals, set their level and make them follow you
            //
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                if (godMode)
                {
                    ShowULMsg("Taming/re-taming in r30");
                    Console.instance.Print("Taming/re-taming in r30");
                    Tameable.TameAllInArea(player.transform.position, 30.0f);
                    //Tameable.TameAllInArea(((Component)Player.m_localPlayer).get_transform().get_position(), 20f);

                    List<Character> list = new List<Character>();
                    Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, list);

                    //todo: Do tamed animals lose their followtarget when you "zone" ?
                    //todo: Can you _only_ set follow target if the monster is a skeleton? Or should we seperate it?
                    foreach (Character item in list)
                    {
                        // Make all tame animals in area 2-star
                        if (item.IsTamed())
                        {
                            //item.SetLevel(3);
                            //set follow target (re-tame)'
                            item.gameObject.GetComponent<MonsterAI>().SetFollowTarget(player.gameObject);
                            Console.instance.Print("Setting explicit follow target");

                        }
                    }

                    //If nothing found spawn a two star lox friend?
                }
                else
                {
                    ShowULMsg("Not a god");
                }
            }

            // -----------------------------------------------------------------------
            // F12: Replenish stacks
            // todo: Consider Adding items to an allowList table. 
            if(Input.GetKeyDown(KeyCode.F12))
            {
                if (godMode)
                {
                    //Boost stack sizes - todo: just do one loop for all items?
                    List<ItemDrop.ItemData> all_items = player.GetInventory().GetAllItems();

                    foreach (ItemDrop.ItemData item in all_items)
                    {
                        //TODO: Set durability here instead
                        //If stackable, refill
                        if (item.m_shared.m_maxStackSize > 1)
                        {
                            //item.m_shared.m_maxStackSize = 100;
                            item.m_stack = item.m_shared.m_maxStackSize;
                            item.m_shared.m_equipDuration = 500f;
                        }
                    }
                }
                else
                {
                    ShowULMsg("Not a god");
                }
            }

            // ------------------------------------------------------
            // F8: Boost
            // Renewal song (and scret Boost that cant be undone).
            // todo: split this func
            if (Input.GetKeyDown(KeyCode.F8))
            {
                // Health regen
                float regenMult = 100; // this helped.
                float fallDmg = 0.1f;
                float noise = 1;

                // ------------------------
                // Enable renewal
                // ----------------------------
                renewal = !renewal;

                //todo: put this on a key? also needs puke! .ClearFood() - maybe alternate.
                List<Player.Food> foods = player.GetFoods();
                foreach(var food in foods)
                {
                    food.m_time = 10000f;
                }

                // Try to alter abilities of magic weapons

                player.GetSEMan().ModifyHealthRegen(ref regenMult); //Seems that regen multiplier is applied to number of food items.                
                player.GetSEMan().ModifyFallDamage(1, ref fallDmg); //todo: doesnt entirely work
                player.GetSEMan().ModifyNoise(1f, ref noise); //Be very, very quiet. I'm hunting rabbits!

                // Note: Changing the MAX for health, sta and eitr does not work, because there's a server side check to iterate all foods and set
                // the values based on that. You can see it change for a sec, then change back.
//                player.SetMaxEitr(300f, true); 
//                player.AddEitr(player.GetMaxEitr() - player.GetEitr());

                player.m_blockStaminaDrain  = 0.1f;
                player.m_jumpStaminaUsage   = 5f;
                player.m_staminaRegen       = 50f; //5f default
                player.m_runStaminaDrain    = 0.1f;
                player.m_staminaRegenDelay  = 0.5f; //1f default
                player.m_eiterRegen         = 50f;
                player.m_eitrRegenDelay     = 0.1f;
                player.m_sneakStaminaDrain  = 0.1f;
                player.m_jumpStaminaUsage   = 1.0f;
                //player.m_baseHP = 400; //??
                //player.m_baseStamina = 400;

                // Tolerate everything
                player.m_tolerateFire = true;
                player.m_tolerateTar = true;
                player.m_tolerateSmoke = true;
                player.m_tolerateWater = true;
                
                //Max weight
                player.m_maxCarryWeight = 99999.0f;

                // Print list of equipped items
                List<ItemDrop.ItemData> items = player.GetInventory().GetEquippedItems();

                // Augment equipped items - Its setting durability to 10000 for non equipped items still, for some reason.
                // Cycle through settings?
                foreach (ItemDrop.ItemData item in items)
                {
                    item.m_shared.m_durabilityDrain = 0.1f; //no dura drain

                    //Set max dura on everything. todo: dont just do this on equipped items.
                    item.m_shared.m_maxDurability = 10000f;
                    item.m_durability = 10000f;

                    if (!item.IsWeapon())
                    {
                        item.m_shared.m_armor = 60f;
                    }
                }

                // TODO: Check for boss items this way.
                // player.GetInventory().HaveItem("");

                Game.instance.GetPlayerProfile().Save(); // Does this save items?
                ShowULMsg("Renewal song: " + renewal.ToString() + "! (and stats augmented.");
            }

            // --------------------------------------
            // SLOW!
            if (melodicBinding && counter % 150 == 0)
            {
                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 30.0f, list);

                foreach (Character item in list)
                {
                    if (item.IsMonsterFaction(10)) //   time?
                    {
                        item.m_speed = 1f;
                        item.m_runSpeed = 1f;
                        item.m_turnSpeed = 1f;
                        Console.instance.Print("Slowing: " + item.m_name);
                    }
                }
            }

            // -------------------------------------------------------
            // PB AOE DMG SONG
            //
            if (cloakOfFlames && !player.InGhostMode() && counter % 75 == 0)
            {
                List<Character> list = new List<Character>();
                //set burning (lets hope you're resistant!)

                player.GetSEMan().AddStatusEffect(new SE_Burning(), resetTime: true, 10, 10);

                list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 5f, list);

                foreach (Character item in list)
                {
                    //Cloak of flames
                    if (!item.IsPlayer() && item.IsMonsterFaction(10)) //10 ?
                    {
                        Console.instance.Print("Cloak of flames ticked");
                        item.Damage(new HitData
                        {
                            m_damage =
                                                    {
                                                        m_damage = 75f
                                                    }
                        });
                    }
                }
            }

            // -----------------------------
            // HEAL song
            // -------------------
            // is enabled then heal yourself
            if (counter % 50 == 0)
            {
                //ShowULMsg(player.GetHealthPercentage().ToString());
                //Console.instance.Print(player.GetHealthPercentage().ToString());

                if (renewal && player.GetHealthPercentage() < 1.0f)
                {
                    Invigorate();

                }
/* // Soft healing timer
                if (counter % 15  == 0)
                {
                    if (player.GetHealthPercentage() < 0.75f)
                    {
                        //ShowULMsg("Player at " + Math.Floor(player.GetHealthPercentage() * 100) + "%. Healing.");
                        player.Heal(50f, false);
                        player.Heal(50f, true);
                    }

                    // Take from monsters and give to players or pets in large range.
                    //
                    List<Character> list = new List<Character>();
                    Character.GetCharactersInRange(player.transform.position, 30.0f, list);

                    foreach (Character item in list)
                    {
                        if (( item.IsPlayer() || item.IsTamed())  && item.GetHoverName() != player.GetHoverName() && item.GetHealthPercentage() < 0.75f)
                        {
                            ShowULMsg(item.GetHoverName() + " at " + Math.Floor(player.GetHealthPercentage() * 100) + "%. Healing.");
                            item.Heal(25.0f, false); // don't show it.
                            //item.m_runSpeed = 1000;
                        }
                    }
                }

                // Faster, Imminent danger timer
                if (counter % 25 == 0)
                {
                    if (player.GetHealthPercentage() < 0.4f)
                    {
                        ShowULMsg("LOW HEALTH! CHEAL");
                        player.Heal((player.GetMaxHealth()) - player.GetHealth(), false);
                    }
                }

                */
            }

            // -------------------------------------------------
            // UP: Add damage counter
            // ------------------------------------
            // This could be function called by both +/-
            // todo: Maybe boost healing / damage shield / etc too?
            if (false /*Input.GetKeyDown(KeyCode.UpArrow) && !Console.IsVisible()*/)
            {
                damageCounters++;

                // Print list of equipped items
                List<ItemDrop.ItemData> items = player.GetInventory().GetEquippedItems();

                // Augment equipped weapon
                foreach (ItemDrop.ItemData item in items)
                {
                    item.m_shared.m_durabilityDrain = 0.1f; //no dura drain
                    if (item.IsWeapon())
                    {
                        HitData.DamageTypes updated = new HitData.DamageTypes();
                        HitData.DamageTypes current = item.GetDamage();

                        updated.m_blunt = current.m_blunt > 0 ? damageCounters * 10 : 0;
                        updated.m_frost = current.m_frost > 0 ? damageCounters * 10 : 0;
                        updated.m_lightning = current.m_lightning > 0 ? damageCounters * 10 : 0;
                        updated.m_pierce = current.m_pierce > 0 ? damageCounters * 10 : 0;
                        updated.m_poison = current.m_poison > 0 ? damageCounters * 10 : 0;
                        updated.m_slash = current.m_slash > 0 ? damageCounters * 10 : 0;
                        updated.m_spirit = current.m_spirit > 0 ? damageCounters * 10 : 0;
                        
                        item.m_shared.m_damages = updated;
                        ShowULMsg("Augmenting " + item.m_shared.m_name + "with " + damageCounters + " damage counters.");
                    }
                }
            }

            /* TEST multikey */

            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyUp(KeyCode.Alpha1))
                {
                    ShowULMsg("LCtrl + 1");
                    
                }
                if (Input.GetKeyUp(KeyCode.Alpha2))
                {
                    ShowULMsg("LCtrl + 2");

                }
            }

            // -------------------------------------------------
            // Remove active damage counter.
            // ----------------------------------------------------
            if (false /*Input.GetKeyDown(KeyCode.DownArrow) && !Console.IsVisible()*/)
            {
                if(damageCounters > 1)
                {
                    damageCounters--;
                }
                ApplyDamageCounterToCurrentWeapon(damageCounters);
            }

            // ---------------------------------------
            // Apply Damage counters
            // ---------------------------------------
            if (Input.GetKeyDown(KeyCode.F11))
            {
                ApplyDamageCounterToCurrentWeapon(++damageCounters);
            }

            // ------------------------------------
            // LEFT: AoE HEAL (dvergr style)
            // --------------------------------------------------------
            //Note: I think DVERGR are immune to fire. But AOE_NOVA works, and since its "spawned" it doesnt aggro
            if (false /*Input.GetKeyDown(KeyCode.LeftArrow)*/)
            {
                GameObject prefab2 = ZNetScene.instance.GetPrefab("DvergerStaffHeal_aoe");

                if (!prefab2)
                {
                    ShowULMsg("Missing object heal");
                }
                else
                {
                    Vector3 vector = UnityEngine.Random.insideUnitSphere;

                    ShowULMsg("Spawning Heal");
                    GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f + Vector3.up + vector, Quaternion.identity);
                    ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                }
            }

            // -------------------------------
            // RIGHT: Wacky dmg AOE.
            //All the arrow keys are free!
            if (false /*Input.GetKeyDown(KeyCode.RightArrow*/)
            {
                GameObject prefab2 = ZNetScene.instance.GetPrefab("aoe_nova");

                if (!prefab2)
                {
                    ShowULMsg("Missing object aoe_nova");
                }
                else
                {
                    Vector3 vector = UnityEngine.Random.insideUnitSphere;

                    ShowULMsg("Spawning Nova!");
                    GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f + Vector3.up + vector, Quaternion.identity);
                    ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                }
            }

            // ----------------------------------------------------------------------
            // down:  Kill all monsters in radius 20f. Move this to somewhere else!
            //
            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (godMode)
                {
                    List<Character> list = new List<Character>();
                    Character.GetCharactersInRange(player.transform.position, 20f, list);

                    int amount = 0;

                    foreach (Character item in list)
                    {
                        if (!item.IsPlayer() && !item.IsTamed())
                        {
                            player.StartEmote("headbang");
                            item.Damage(new HitData
                            {
                                m_damage =
                        {
                            m_damage = 1E+10f
                        }
                            });
                            amount++;
                        }
                    }
                    ShowULMsg("Killing monsters in range:" + amount);
                }
                else
                {
                    ShowULMsg("Not a god.");
                }

            }

            // -------------------------------------------------
            // Equipped weapon becomes Super weapon
            // -----------------------------------------------
            // todo: ability to revert back
            // todo:  if up 10 times, just go big?
            //
            if (false)
            {
                // Print list of equipped items
                List<ItemDrop.ItemData> items = player.GetInventory().GetEquippedItems();
                string items_str = "";

                // Augment equipped items
                foreach (ItemDrop.ItemData item in items)
                {
                    item.m_shared.m_durabilityDrain = 0.1f; //no dura drain

                    if (item.IsWeapon())
                    {
                        HitData.DamageTypes ouch = new HitData.DamageTypes()
                        {
                            m_slash = 200f,
                            m_blunt = 200f,
                            m_pierce = 200f,
                            m_fire = 200f,
                            m_spirit = 200f,
                            m_frost = 200f,
                            m_lightning = 200f,
                            m_poison = 200f,
                            m_chop = 1000f,
                            m_pickaxe = 1000f, //new
                            m_damage = 1000f //new
                        };

                        item.m_shared.m_damages = ouch;
                        item.m_shared.m_durabilityDrain = 0.1f;
                        item.m_shared.m_maxDurability = 10000f;
                        item.m_durability = 10000f;
                        //new
                        item.m_shared.m_movementModifier = 5; // x 100%
                        item.m_shared.m_swimStaminaModifier = 5;
                        item.m_shared.m_weight = 0.1f;

                        item.m_shared.m_attackForce = 100;
                        //item.m_shared.m_blockEffect = // Play around with new effects.

                        item.m_shared.m_name = "My Weapon"; //todo: Can we spawn things on the ground? This actually triggers a "New Item" game popup
                        item.m_customData.Add("hej", "12");
                        item.m_customData.Add("Name", "Cloak of Flames");

                        items_str += "Augmented " + item.m_shared.m_name + " with lots of damage!\n";
                    }
                }
                godWeapon = true;
                ShowULMsg(items_str);
            }

            // ----------------------------------------
            // F3: Go faster Maybe CTRL + U/D
            //
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                float speed = player.m_runSpeed + 1;
                SetSpeed(speed);
            }

            // -------------------------------------------------
            // F4: Go slower
            //
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                float speed = player.m_runSpeed - 1;
                SetSpeed(speed);
            }

            

            // ----------------------------------------------
            // Solo teleport
            // ------------------------------------------------
            // Port to coordinates specified by mouse cursor
            // INS.
            //
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);
                ShowULMsg("Trying to teleport...");
                Chat.instance.SendPing(position);

//                    Vector3 vector = new Vector3(position.x, position.y, position.z);
//                    Heightmap.GetHeight(vector, out var height);
//                   vector.z = Math.Max(0f, height);

                //Console.instance.AddString("Solo teleport", "Heading to " + position.ToString("0.0"), (int)Talker.Type.Whisper);
                Player.m_localPlayer.TeleportTo(position, Player.m_localPlayer.transform.rotation, distantTeleport: true);
            }

            // ----------------------------------------------
            // Mass teleport
            //
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);
                Chat.instance.SendPing(position);
                //Console.instance.AddString("Mass Teleport", "Heading to " + position.ToString("0.0"), (int)Talker.Type.Whisper);

                if (Player.m_localPlayer)
                {
                    List<Character> list = new List<Character>();
                    Character.GetCharactersInRange(player.transform.position, 50f, list);
                    ShowULMsg("Found " + list.Count + " characters within 50f meters!");

                    Vector3 mypos = ((Component)Player.m_localPlayer).transform.position;
                    Quaternion myrot = ((Component)Player.m_localPlayer).transform.rotation;

                    foreach (Character friend in list)
                    {
                        if (friend.IsPlayer())
                        {
                            ShowULMsg("Teleporting: " + friend.GetHoverName());

                            Chat.instance.TeleportPlayer(friend.GetZDOID().UserID,
                                position,
                                player.transform.rotation, //??
                                distantTeleport: true);
                        }

                    }
                }
            }

            // -------------------------------------
            // HOME: Gate - go to bindspot
            //
            if (Input.GetKeyDown(KeyCode.Home))
            {
                // Get Spawn point.
                //   Either Custom (bed) or home (stones?).
                //   TODO: Dont teleport if home is same as current loc (or within some)
                //
                Vector3 dst = GetSpawnPoint();
                if (!dst.Equals(Vector3.zero))
                {
                    // Lots of logging!
                    //
                    Player.m_localPlayer.Message(
                        MessageHud.MessageType.TopLeft,
                        "Teleporting home: " +
                        dst);
                    ShowULMsg("Teleporting to: " + dst);
//                    Console.instance.AddString("I'm out of here!", "Gate to " + dst.ToString("0.0"), Talker.Type.Shout);

                    // Perform distant teleport to dst
                    //
                    Player.m_localPlayer.TeleportTo(
                        dst,
                        Player.m_localPlayer.transform.rotation,
                        true);
                }
                else
                {
                    ShowULMsg("Could not find a good spot to port to!");
                }
            }

            // --------------------------------------
            // END: Succor
            // Port to "safe" pin on map
            // todo: take friends
            //
            if (Input.GetKeyDown(KeyCode.End))
            {
                bool safePinFound = false;
                int safeRadius = 5;
                List<Minimap.PinData> ourPins = Minimap.m_pins; //todo: we made this public somehow.

                foreach (Minimap.PinData pin in ourPins)
                {
                    //TODO: Add some helpers for teleporting (incl. logging). Also checks radius.
                    if(!pin.m_name.Equals("")) Console.instance.Print("Pin = " + pin.m_name);
                    if (pin.m_name.Equals("safe")) //"safe"//todo: This can be abused to teleport to a specific boss pin, but, might get stuck, so teleport a bit away? 
                    {
                        safePinFound = true;

                        // Perform distant teleport to dst. Distance check.
                        //
                        float distance = Utils.DistanceXZ(pin.m_pos, Player.m_localPlayer.transform.localPosition);
                        Console.instance.Print("Distance to where you want to go: " + distance);

                        if (distance < safeRadius)
                        {
                            Console.instance.AddString("You're already here!");
                            break;
                        }

                        //Console.instance.AddString("I'm out of here!", "Succor to " + pin.m_pos.ToString("0.0"), Talker.Type.Shout);

                        Player.m_localPlayer.TeleportTo(
                            pin.m_pos + UnityEngine.Random.insideUnitSphere * 5,
                            Player.m_localPlayer.transform.rotation,
                            true);

                        break; //exit on first safe pin
                    }
                }

                if(!safePinFound)
                {
                    Console.instance.AddString("No safe spot found. Add a pin called 'safe'.");
                }
            }
        }

        // ---------------------------------------------------------------------
        // Private functions
        //

        private void Invigorate()
        {
            Player.m_localPlayer.Message(
                MessageHud.MessageType.TopLeft,
                "Invigorating (<100% hp)");

            // CHeal on all player resources
            //
            player.AddStamina(player.GetMaxStamina() - player.GetStamina());
            player.Heal(player.GetMaxHealth() - player.GetHealth());
            player.AddEitr(player.GetMaxEitr() - player.GetEitr());

            // Status
            // ....

            // Remove bad effects
            //
            // TODO: Does this only remove bad ones?
            List<StatusEffect> effects = player.GetSEMan().GetStatusEffects();
            foreach (StatusEffect st in effects)
            {
                player.GetSEMan().RemoveStatusEffect(st);
            }

            // Add beneficial effects
            
            player.GetSEMan().AddStatusEffect(new SE_Rested(), resetTime: true, 10, 10); //this will add lvl1: 8mins
            player.GetSEMan().AddStatusEffect(new SE_Shield(), resetTime: true, 10, 10);
        }

        // Can be both positive and negative
        private void ApplyDamageCounterToCurrentWeapon(uint damage_counters)
        {
            // Print list of equipped items
            List<ItemDrop.ItemData> items = player.GetInventory().GetEquippedItems();

            // Augment equipped weapon
            foreach (ItemDrop.ItemData item in items)
            {
                item.m_shared.m_durabilityDrain = 0.1f; //no dura drain
                if (item.IsWeapon())
                {
                    HitData.DamageTypes updated = new HitData.DamageTypes();
                    HitData.DamageTypes current = item.GetDamage();

                    updated.m_blunt = current.m_blunt > 0 ? damage_counters * 10 : 0;
                    updated.m_frost = current.m_frost > 0 ? damage_counters * 10 : 0;
                    updated.m_lightning = current.m_lightning > 0 ? damage_counters * 10 : 0;
                    updated.m_pierce = current.m_pierce > 0 ? damage_counters * 10 : 0;
                    updated.m_poison = current.m_poison > 0 ? damage_counters * 10 : 0;
                    updated.m_slash = current.m_slash > 0 ? damage_counters * 10 : 0;
                    updated.m_spirit = current.m_spirit > 0 ? damage_counters * 10 : 0;

                    item.m_shared.m_damages = updated;
                    ShowULMsg("Augmenting " + item.m_shared.m_name + "with " + damageCounters + " damage counters.");
                }
            }
        }

        private void SomethingTeleport()
        {

        }

        private void FindBosses()
        {

            //todo: Do something here that randomizes what we look for?
            foreach (var name in bossNames)
            {
                ShowULMsg("Revealing: " + name.Item2);
                Game.instance.DiscoverClosestLocation(
                    name.Item1,
                    Player.m_localPlayer.transform.position,
                    name.Item2,
                    (int)Minimap.PinType.Boss);
                // Show map    = true
                // Discoverall = false
            }

            //Finally, just explore everything. TODO: Make this a separate key.
            //Minimap.instance.ExploreAll();
        }

        private void SetSpeed(float speed)
        {
            player.m_runSpeed = speed;
            player.m_swimSpeed = speed;
            player.m_acceleration = speed;
            player.m_crouchSpeed = speed;
            player.m_walkSpeed = speed;
            player.m_jumpForce = speed;
            player.m_jumpForceForward = speed;

            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 30.0f, list);

            //todo: set walk = snare?
            //Increase runspeed (and attack speed?) for EEEVERYONE. Pets, players, ...
            foreach (Character item in list)
            {
                if (!item.IsMonsterFaction(10) && item != player) //Does this include pets?
                {
                    item.m_runSpeed = speed;
                    item.m_speed = speed;

                    //Player.m_localPlayer.GetSkills().CheatRaiseSkill(args[1], value14);
                    ShowULMsg("Increased speed for " + item.GetHoverName() + " to " + item.m_runSpeed);
                }
            }
            ShowULMsg("New player speed: " + player.m_runSpeed);
        }

        private static Vector3 GetSpawnPoint()
        {
            PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
            if (playerProfile.HaveCustomSpawnPoint())
            {
                return playerProfile.GetCustomSpawnPoint();
            }
            return playerProfile.GetHomePoint();
        }

        //May require UnityEngine.UI.dll to be copied from deck.
        private static Vector3 ScreenToWorldPoint(Vector3 mousePos)
        {            
            Vector2 screenPoint = mousePos;
            RectTransform rectTransform = Minimap.instance.m_mapImageLarge.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out var localPoint))
            {
                Vector2 vector = Rect.PointToNormalized(rectTransform.rect, localPoint);
                Rect uvRect = Minimap.instance.m_mapImageLarge.uvRect;
                float mx = uvRect.xMin + vector.x * uvRect.width;
                float my = uvRect.yMin + vector.y * uvRect.height;
                return MapPointToWorld(mx, my);
             }
             return Vector3.zero;
            //return new Vector3();
        }

        private static Vector3 MapPointToWorld(float mx, float my)
        {
            int num = Minimap.instance.m_textureSize / 2;
            mx *= (float)Minimap.instance.m_textureSize;
            my *= (float)Minimap.instance.m_textureSize;
            mx -= (float)num;
            my -= (float)num;
            mx *= Minimap.instance.m_pixelSize;
            my *= Minimap.instance.m_pixelSize;
            return new Vector3(mx, 0f, my);
        }
    }
}
