using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;

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
            UnifiedPopup.Push(new WarningPopup("EverHeim", "Loaded Cheat 052524!"
                , delegate
            {
                UnifiedPopup.Pop();
            }));

            // Instanciate a new GameObject instance attaching script component (class) 
            cheatObject = new GameObject();
            cheatObject.AddComponent<DiscoverThings>();

            // Avoid object destroyed when loading a level
            GameObject.DontDestroyOnLoad(cheatObject);
        }
    }

    //TODO:
    // Load these commands from command line (some known command) instead of about menu?
    public class DiscoverThings : MonoBehaviour
    {
        public static bool godMode = false;
        public static bool noBuildCost = false;
        public static int godPower = 0;
        public static bool godWeapon = false;

        // Counters
        UInt16 counter = 0;
        UInt16 damageCounters = 5;

        // States
        public static bool renewal = false;       //heal friends  
        public static bool cloakOfFlames = false; //fire pbaoe
        public static bool melodicBinding = false; //Slow movement + atk speed
        public static bool ghostMode = false;
        public static bool RandomEvent = false;

        private Rect MainWindow;
        private Rect StatusWindow;
        public bool visible = false;

        private void Awake()
        {
            Console.instance.Print("Awake..");
        }

        private void Start()
        {
            Console.instance.Print("Start..");

            MainWindow = new Rect(150.0f, Screen.height - 250f, 250f, 150f);

            float center_x = (Screen.width  / 2) - (250 / 2);
            float center_y = (Screen.height / 2) - (320 / 2);

            StatusWindow = new Rect(Screen.width - 220f, Screen.height - 500f, 220f, 350f);
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

            //todo: Turn this into AddLine() helper
            //todo: use player.getGodMode() instead of relying on internal variable.
            AddHorizontalGridLine("Stats + Renewal song (F8)",      renewal);
            AddHorizontalGridLine("God/No Cost (F9)",               Player.m_localPlayer.InGodMode());
            AddHorizontalGridLine("Weapon++ (F11)",                 godWeapon);
            AddHorizontalGridLine("Boost/Worsen weapon (Up/Down)",  godWeapon);
            AddHorizontalGridLine("Ghost (9)",                      Player.m_localPlayer.InGhostMode());
            AddHorizontalGridLine("Cloak Of Flames (0)",            cloakOfFlames);
            AddHorizontalGridLine("Melodic binding (B)",            melodicBinding);
            AddHorizontalGridLine("Heal/remove (F1)");
            AddHorizontalGridLine("Runspeed (F3/F4)");
            AddHorizontalGridLine("Replenish stock (F12)");
            AddHorizontalGridLine("Heal/Nova (Left/Right)");
            AddHorizontalGridLine("Port (Ins, Del, Home, End)");
            AddHorizontalGridLine("(Re)tame/Kill All (PUp, PDn)");
            AddHorizontalGridLine("Sp. skeleton (Pause)");
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
            GUI.DragWindow();
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

            //int deaths = 0; // Game.instance.GetPlayerProfile().m_playerStats.m_deaths;
            //int crafts = 0; // Game.instance.GetPlayerProfile().m_playerStats.m_crafts;
            //int builds = 0; // Game.instance.GetPlayerProfile().m_playerStats.m_builds;

            GUI.Label(new Rect(10, 3, 1000, 70), "Everheim v.0.1.  Death: " + deaths + "  Crafts: " + crafts + "  Builds: " + builds + "  Bosses: " + bosses + " State: " + state);

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
        //
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

        //todo: move all these to header.
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

        List<string> pets = new List<string> { /*"Boar", "Wolf", "Lox", "Hen",*/ "Skeleton_Friendly" };
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
        // F7: Invigorate!
        // F8: Boost stats
        // F9:
        // F10: Find bosses
        // F11: WEAPON++
        // F12: Replenish stacks
        //
        // Up/Down: Weapon better or worse
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

        Player player;
        private void Update()
        {
            // ----------------------------------
            // TICK
            // ----------------------------------
            counter++; // wrap is fine.

            // --------------------------------
            // Refresh player
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
            if (Input.GetKeyDown( KeyCode.UpArrow)) //was F7
            {
                Invigorate();
                ShowULMsg("Invigorated!");
            }

            // -----------------------------------
            // Toggle GM mode
            //
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                godMode = !godMode;
                player.SetGodMode(godMode);
                player.m_boss = godMode;

                //no build cost
                Player.m_localPlayer.SetNoPlacementCost(value: godMode);
                
                ShowULMsg(godMode ? "You're a GM and a boss!" : "You're just a player.");
            }

            // --------------------------------------------
            // Toggle Guardian power
            //
            if (Input.GetKeyDown(KeyCode.F6))
            {
                //GP_Eikthyr, GP_TheElder, GP_BoneMass, GP_Moder (seeker queen?)
                string[] gods = { "GP_Eikthyr", "GP_Bonemass", "GP_Moder", "GP_Yagluth" };
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
            // Spawn pets. Currently skeleton, since thats the only thing that can be summoned legally.
            // TODO: Spawn a trio. Timon (boar), Misha (bear), some wolf
            // TODO: Spawn a bit off the side.
            //
            int pet_counter = 0;
            if (Input.GetKeyDown(KeyCode.Pause))
            {
                //player.ToggleDebugFly();

                int count = 1;

                DateTime now = DateTime.Now;
                Random rand = new Random();

                int pet_index = rand.Next(pets.Count);
                int name_index = rand.Next(pet_names.Count);

                GameObject prefab2 = ZNetScene.instance.GetPrefab(pets[pet_index]);

                if (!prefab2)
                {
                    ShowULMsg("Missing object" + pets[pet_index] + " (" + pet_index + ")");
                }
                else
                {
                    Vector3 vector = UnityEngine.Random.insideUnitSphere * ((count == 1) ? 0f : 0.5f);

                    ShowULMsg("Spawning pet: " + pets[pet_index] + " (" + pet_index + ", " + pet_names[name_index] + ")");
                    GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f + Vector3.up + vector, Quaternion.identity);
                    ItemDrop component4 = gameObject2.GetComponent<ItemDrop>();
                    gameObject2.GetComponent<Character>()?.SetLevel(2); //1 = 0, 2 = 1, 3 = 2 stars
                    gameObject2.GetComponent<Character>().m_name = pet_names[name_index];
                    gameObject2.GetComponent<Character>().SetMaxHealth(5000);
                    gameObject2.GetComponent<Character>().SetHealth(5000);
                    gameObject2.GetComponent<MonsterAI>().SetFollowTarget(player.gameObject);

                    //tame it - if possible
                    Tameable.TameAllInArea(player.transform.position, 30.0f);
                }

                pet_counter++;
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

            // ----------------------------
            // Z: Spawn Seeker
            // 
            if (Input.GetKeyDown(KeyCode.Z)/* && !Console.IsVisible()*/)
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
            // Backspace: Spawn Dvergr
            //
            //todo: Turn this into a function that takes a prefab list and a chance list.
            if (Input.GetKeyDown(KeyCode.Backspace) && !Console.IsVisible())
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

            // ---------------------------------------------------
            // PGUP: Tame animals, set their level and make them follow you
            //
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
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

            // -----------------------------------------------------------------------
            // F12: Replenish stacks
            //
            if(Input.GetKeyDown(KeyCode.F12))
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

            // ------------------------------------------------------
            // F8: Boost
            // Renewal song (and scret Boost that cant be undone).
            //
            if (Input.GetKeyDown(KeyCode.F8))
            {
                // Health regen
                float regenMult = 20; // this helped.
                float fallDmg = 0.2f;
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

                //try to alter abilities of magic weapons

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
                                                        m_damage = 30f
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
                //                ShowULMsg(player.GetHealthPercentage().ToString());
                Console.instance.Print(player.GetHealthPercentage().ToString());

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

            /*TEST multikey*/

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                if(Input.GetKeyDown(KeyCode.Alpha1))
                {
                    ShowULMsg("Ctrl + 1");
                }
                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    ShowULMsg("Ctrl + 1");
                }
            }
            // -------------------------------------------------
            // Down:  Remove active damage counter.
            // ----------------------------------------------------
            if (false /*Input.GetKeyDown(KeyCode.DownArrow) && !Console.IsVisible()*/)
            {
                if(damageCounters > 1)
                    damageCounters--;

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

            // ------------------------------------
            // LEFT: AoE HEAL (dvergr style)
            // --------------------------------------------------------
            //Note: I think DVERGR are immune to fire. But AOE_NOVA works, and since its "spawned" it doesnt aggro
            if (Input.GetKeyDown(KeyCode.LeftArrow))
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
            // RIGHT: Wacky Heal AOE.
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
            // PGDN:  Kill all monsters in radius 20f. Move this to somewhere else!
            //
            if (Input.GetKeyDown(KeyCode.DownArrow)) /*Page down*/
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
                ShowULMsg("Killing monsers in range:" + amount);
            }

            // -------------------------------------------------
            // Equipped weapon becomes Super weapon
            // -----------------------------------------------
            // todo: ability to revert back
            // todo:  if up 10 times, just go big?
            //
            if (Input.GetKeyDown(KeyCode.F11))
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
                            m_poison = 200f
                        };

                        item.m_shared.m_damages = ouch;
                        item.m_shared.m_durabilityDrain = 0.1f;
                        item.m_shared.m_maxDurability = 10000f;
                        item.m_durability = 10000f;

                        items_str += "Augmented " + item.m_shared.m_name + " with lots of damage!\n";
                    }
                }
                godWeapon = true;
                ShowULMsg(items_str);
            }

            // ----------------------------------------
            // F3: Go faster
            //
            if (Input.GetKeyDown(KeyCode.F3))
            {
                player.m_runSpeed += 1;
                player.m_swimSpeed += 1;
                player.m_acceleration += 1;
                player.m_crouchSpeed += 1;
                player.m_walkSpeed += 1;
                player.m_jumpForce += 0.5f;
                player.m_jumpForceForward += 0.5f;

                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 30.0f, list);

                //todo: set walk = snare?
                //Increase runspeed (and attack speed?) for EEEVERYONE. Pets, players, ...
                foreach (Character item in list)
                {
                    if(!item.IsMonsterFaction(10) && item!=player) //Does this include pets?
                    {
                        item.m_runSpeed += 1;
                        item.m_speed += 1;               

                        //Player.m_localPlayer.GetSkills().CheatRaiseSkill(args[1], value14);
                        ShowULMsg("Increased speed for " + item.GetHoverName() + " to " + item.m_runSpeed);
                    }
                }
                ShowULMsg("Faster: " + player.m_runSpeed);
            }

            // -------------------------------------------------
            // F4: Go slower
            //
            if (Input.GetKeyDown(KeyCode.F4))
            {
                player.m_runSpeed -= 1;
                player.m_acceleration -= 1;
                player.m_swimSpeed -= 1;
                player.m_crouchSpeed -= 1;
                player.m_walkSpeed -= 1;
                player.m_jumpForce -= 0.5f;
                player.m_jumpForceForward -= 0.5f;

                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 30.0f, list);

                foreach (Character item in list)
                {
                    if(!item.IsMonsterFaction(10) && item != player)
                    {
                        item.m_runSpeed -= 1;
                        item.m_speed -= 1;
                        ShowULMsg("Slowed speed for " + item.GetHoverName() + " to " + item.m_runSpeed);
                    }
                }

                ShowULMsg("Slower: " + player.m_runSpeed);
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

                if (Player.m_localPlayer)
                {
                    Chat.instance.SendPing(position);

                    //Vector3 vector = new Vector3(position.x, /*Player.m_localPlayer.transform.*/position.y, position.z);
                    //Heightmap.GetHeight(vector, out var height);
                    //vector.z = Math.Max(0f, height);

                    Console.instance.AddString("Solo teleport", "Heading to " + position.ToString("0.0"), (int)Talker.Type.Whisper);
                    Player.m_localPlayer.TeleportTo(position/*vector*/, Player.m_localPlayer.transform.rotation, distantTeleport: true);
                }
            }

            // ----------------------------------------------
            // Mass teleport
            //
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);
                Chat.instance.SendPing(position);
                Console.instance.AddString("Mass Teleport", "Heading to " + position.ToString("0.0"), (int)Talker.Type.Whisper);

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
                    if(pin.m_name.Equals("safe"))
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

                        Console.instance.AddString("I'm out of here!", "Succor to " + pin.m_pos.ToString("0.0"), Talker.Type.Shout);

                        Player.m_localPlayer.TeleportTo(
                            pin.m_pos,
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
        private void ApplyDamageCounterToCurrentWeapon()
        { }

        private void SomethingTeleport()
        { }


        private void FindBosses()
        {
            foreach (var name in bossNames)
            {
                ShowULMsg("Revealing: " + name.Item2);
                Game.instance.DiscoverClosestLocation(
                    name.Item1,
                    Player.m_localPlayer.transform.position,
                    name.Item2,
                    (int)Minimap.PinType.Boss);

            }

            //Finally, just explore everything. Hope this doesn't årevent revealing bosses.
            Minimap.instance.ExploreAll();
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
