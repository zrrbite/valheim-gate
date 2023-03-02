using System.Collections.Generic;
using System;
using UnityEngine;

namespace ICanShowYouTheWorld
{
    public class NotACheater : MonoBehaviour
    {
        private static GameObject cheatObject;

        public static void Run()
        {
            UnifiedPopup.Push(new WarningPopup("EverHeim", "Loaded 0.213.4!"
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

        private Rect MainWindow;
        public bool visible = false;

        private void Awake()
        {
            Console.instance.Print("Awake..");
        }

        private void Start()
        {
            Console.instance.Print("Start..");

            MainWindow = new Rect(10f, 10f, 250f, 150f);
        }

        float lastDist = 0;
        float currDist = 0;
        GUIStyle style;
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
                    GUILayout.Label(item.GetHoverName().ToString() + ": " + distance.ToString("0.0") + "m (" + item.GetLevel() + " - " + Math.Floor(item.GetHealth()) + "/" + Math.Floor((item.GetHealthPercentage()*100)) + "%)", new GUILayoutOption[0]);
                }
            }
            GUI.DragWindow();
        }

        private void OnGUI()
        {
            int kills = Game.instance.GetPlayerProfile().m_playerStats.m_kills;
            int deaths = Game.instance.GetPlayerProfile().m_playerStats.m_deaths;
            int crafts = Game.instance.GetPlayerProfile().m_playerStats.m_crafts;
            int builds = Game.instance.GetPlayerProfile().m_playerStats.m_builds;

            GUI.Label(new Rect(10, 5, 200, 60), "Everheim v.0.1");
            GUI.Label(new Rect(10, 50, 300, 60), "kills: " + kills + " Deaths: " + deaths + " crafts:" + crafts + " builds:" + builds);

            if (!visible)
                return;

            MainWindow = GUILayout.Window(0, MainWindow, new GUI.WindowFunction(RenderUI), "Tracking", new GUILayoutOption[0]);

            //TODO: Add another window here to show state of configuration (gm mode, etc)
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

        readonly List<Tuple<string, string>> bossNames = new List<Tuple<string, string>>
        {
            Tuple.Create<string, string>("Eikthyrnir","Eikthyr"),
            Tuple.Create<string, string>("GDKing","The Elder"),
            Tuple.Create<string, string>("Bonemass","Bonemass"),
            Tuple.Create<string, string>("Dragonqueen","Moder"),
            Tuple.Create<string, string>("SeekerQueen","Seeker Queen"),
            Tuple.Create<string, string>("Seekerqueen","Seeker Queen"),
            Tuple.Create<string, string>("Hive","Seeker Queen"),
            Tuple.Create<string, string>("Queen","Queen"),
            Tuple.Create<string, string>("GoblinKing","Yagluth"),
            Tuple.Create<string, string>("Hive","Seeker Queen")
        };

        // Counters
        UInt16 counter = 0;

        // States
        bool fungiTunic = false;


        //TODO: Sort these functions and also turn some into helpers
        //      - e.g. mass teleport should be a helper so it can be done to
        //      mouse cursor OR safe spot
        //      Consider some sort of state machine?

        private void Update()
        {
            //Reveal bosses
            //
            if (Input.GetKeyDown(KeyCode.F10))
            {
                foreach(var name in bossNames)
                {
                    ShowULMsg("Revealing: " + name.Item2);
                    Game.instance.DiscoverClosestLocation(
                        name.Item1,
                        Player.m_localPlayer.transform.position,
                        name.Item2,
                        (int)Minimap.PinType.Boss);

                }
            }

            // Tracking UI
            //
            if (Input.GetKeyDown(KeyCode.F7))
            {
                visible = !visible;
            }

            Player player = Player.m_localPlayer;

            // TODO; Populate some player variables
            // TODO: Remove cooldowns (pots, guardian power, ...)

            // Invigorate!
            //  Heal, remove debuffs, add rested
            if (Input.GetKeyDown( KeyCode.F1))
            {
                //todo: There's a whole bunch of "tolerate" variables

                // Resources
                //
                player.AddStamina   ( player.GetMaxStamina() - player.GetStamina() );
                player.Heal         ( player.GetMaxHealth()  - player.GetHealth()  );
                player.AddEitr      ( player.GetMaxEitr()    - player.GetEitr()    );

                // Status

                // Remove bad effects
                //
                player.GetSEMan().RemoveStatusEffect("Spirit");
                player.GetSEMan().RemoveStatusEffect("Poison");
                player.GetSEMan().RemoveStatusEffect("Frost");
                player.GetSEMan().RemoveStatusEffect("Lightning");
                player.GetSEMan().RemoveStatusEffect("Burning");

                // Add beneficial effects
                //
                player.GetSEMan().AddStatusEffect("Rested", resetTime: true, 10, 10); //this will add lvl1: 8mins

                //
                // + ... ?
                ShowULMsg("Invigorated!");
            }

            // Toggle GM mode
            //
            if (Input.GetKeyDown(KeyCode.F9))
            {
                godMode = !godMode;
                player.SetGodMode(godMode);

                //no build cost
                Player.m_localPlayer.SetNoPlacementCost(value: godMode);
                
                ShowULMsg(godMode ? "You're a GM." : "You're a player.");
            }

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

            // Toggle flying
            //
            if (Input.GetKeyDown(KeyCode.Pause))
            {
                //player.ToggleDebugFly();

                GameObject prefab2 = ZNetScene.instance.GetPrefab("lox");


            }

            // Tame animals
            //
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                Tameable.TameAllInArea(player.transform.position, 30.0f);
                //Tameable.TameAllInArea(((Component)Player.m_localPlayer).get_transform().get_position(), 20f);

                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, list);

                //Unsorted
                foreach (Character item in list)
                {
                    // Make all tame animals in area 2-star
                    if (item.IsTamed())
                    {
                        item.SetLevel(3);
                    }

                    if(item.IsPlayer() && ((Player)item).GetPlayerName() != player.GetPlayerName())
                    {
                        item.SetHealth(25);
                    }
                }

                //If nothing found spawn a two star lox friend

            }

            // Boost!
            //
            if (Input.GetKeyDown(KeyCode.F8))
            {
                // Health regen
                float regenMult = 5; // this helped.
                float fallDmg = 0.2f;
                float noise = 1;

                
                player.GetSEMan().ModifyHealthRegen(ref regenMult); //Seems that regen multiplier is applied to number of food items.                
                player.GetSEMan().ModifyFallDamage(1, ref fallDmg);
                player.GetSEMan().ModifyNoise(1f, ref noise); //Be very, very quiet. I'm hunting rabbits!

                // Stamina drain
                player.m_blockStaminaDrain  = 0.1f;
                player.m_jumpStaminaUsage   = 5f;
                player.m_staminaRegen       = 50f; //5f default
                player.m_runStaminaDrain    = 0.1f;
                player.m_staminaRegenDelay  = 0.5f; //1f default
                player.m_eiterRegen         = 50f;
                player.m_eitrRegenDelay     = 0.1f;
                player.m_sneakStaminaDrain  = 0.1f;
                player.m_jumpStaminaUsage   = 2.0f;

                // Tolerate everything
                player.m_tolerateFire = true;
                player.m_tolerateTar = true;
                player.m_tolerateSmoke = true;
                player.m_tolerateWater = true; // What does this mean exactly?
                
                // I'm now a boss?
//                player.m_boss = true;

                //Max weight
                player.m_maxCarryWeight = 10000.0f;

                // Print list of equipped items
                List<ItemDrop.ItemData> items = player.GetInventory().GetEquipedtems();
                string items_str = "";

                // Augment equipped items
                // Cycle through settings?
                foreach (ItemDrop.ItemData item in items)
                {                    
                    item.m_shared.m_durabilityDrain = 0.1f; //no dura drain

                    if (!item.IsWeapon())
                    {
                        item.m_shared.m_armor = 60f;
                        item.m_shared.m_durabilityDrain = 0.1f;
                        item.m_shared.m_maxDurability = 10000f;
                        item.m_durability = 10000f;
                        items_str += "Augmented " + item.m_shared.m_name + " with " + item.m_shared.m_armor + " armor\n";
                    }
                }
                ShowULMsg(items_str);

                // Fungi tunic
                fungiTunic = !fungiTunic;
                counter = 0;

                ShowULMsg("Boost stats. Fungi: " + fungiTunic.ToString());
            }

            //Fungi tunic - heal checks below 75, 50 and 25% (combined 45) ? 
            if (fungiTunic)
            {
                counter++;

                // Soft healing timer
                if (counter % 150 == 0)
                {
                    if (player.GetHealthPercentage() < 0.75f)
                    {
                        ShowULMsg("Player at " + Math.Floor(player.GetHealthPercentage()*100) + "%. Healing.");
                        player.Heal(12f, true);
                        player.Heal(5f, true);
                    }
                }

                // Imminent danger
                if(counter % 25 == 0)
                {
                    if (player.GetHealthPercentage() < 0.3f)
                    {
                        ShowULMsg("LOW HEALTH!");
                        player.Heal((player.GetMaxHealth() / 2) - player.GetHealth(), true);
                    }
                }
            }

            ////////////////////
            ///

            // Kill all monsters in radius 15f. Move this to somewhere else!
            //
            if (Input.GetKeyDown(KeyCode.PageDown))
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
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Characters in range:" + list.Count);
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Killing all the monsters:" + amount);
            }

            // Equipped weapon becomes Super weapon
            //
            if (Input.GetKeyDown(KeyCode.F11))
            {
                // Print list of equipped items
                List<ItemDrop.ItemData> items = player.GetInventory().GetEquipedtems();
                string items_str = "";

                // Augment equipped items
                foreach (ItemDrop.ItemData item in items)
                {
                    item.m_shared.m_durabilityDrain = 0.1f; //no dura drain

                    if (item.IsWeapon())
                    {
                        HitData.DamageTypes ouch = new HitData.DamageTypes()
                        {
                            m_slash = 100f,
                            m_blunt = 100f,
                            m_pierce = 100f,
                            m_fire = 100f,
                            m_spirit = 100f,
                            m_frost = 100f,
                            m_lightning = 100f,
                            m_poison = 100f
                        };

                        item.m_shared.m_damages = ouch;
                        item.m_shared.m_durabilityDrain = 0.1f;
                        item.m_shared.m_maxDurability = 10000f;
                        item.m_durability = 10000f;

                        items_str += "Augmented " + item.m_shared.m_name + " with lots of damage!\n";
                    }
                }
                ShowULMsg(items_str);
            }

            // Go faster
            //
            if (Input.GetKeyDown(KeyCode.F3))
            {
                player.m_runSpeed += 2;
                player.m_swimSpeed += 2;
                player.m_acceleration += 1;
                player.m_crouchSpeed += 2;
                player.m_walkSpeed += 2;
                player.m_jumpForce += 1;
                ShowULMsg("Faster: " + player.m_runSpeed);
            }

            // Go slower
            //
            if (Input.GetKeyDown(KeyCode.F4))
            {
                player.m_runSpeed -= 2;
                player.m_acceleration -= 1;
                player.m_swimSpeed -= 2;
                player.m_crouchSpeed -= 2;
                player.m_walkSpeed -= 2;
                player.m_jumpForce -= 1f;
                ShowULMsg("Slower: " + player.m_runSpeed);
            }

            // Port to coordinates specified by mouse cursor
            // INS.
            //
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);

                if (Player.m_localPlayer)
                {
                    Vector3 vector = new Vector3(position.x, Player.m_localPlayer.transform.position.y, position.z);
                    Heightmap.GetHeight(vector, out var height);
                    vector.y = Math.Max(0f, height);
                    Player.m_localPlayer.TeleportTo(vector, Player.m_localPlayer.transform.rotation, distantTeleport: true);
                }
            }

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

                            Chat.instance.TeleportPlayer(friend.GetZDOID().userID,
                                position,
                                player.transform.rotation, //??
                                distantTeleport: true);
                        }

                    }
                }
            }

            // Gate - go to bindspot
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
                    Console.instance.Print("Teleporting to: " + dst);
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
                    Player.m_localPlayer.Message(
                        MessageHud.MessageType.TopLeft,
                        "Could not find a good spot to port to!");
                }
            }



            //Port to "safe" pin on map
            // todo: take friends
            //
            if (Input.GetKeyDown(KeyCode.End))
            {
                bool safePinFound = false;
                int safeRadius = 5;
                List<Minimap.PinData> ourPins = Minimap.m_pins;

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

        // Private static functions
        //
        private static Vector3 GetSpawnPoint()
        {
            PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
            if (playerProfile.HaveCustomSpawnPoint())
            {
                return playerProfile.GetCustomSpawnPoint();
            }
            return playerProfile.GetHomePoint();
        }

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
