﻿using System.Collections.Generic;
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
                    GUILayout.Label(item.GetHoverName().ToString() + ": " + distance.ToString("0.0"), new GUILayoutOption[0]);
                }
            }
            GUI.DragWindow();


            /*
                        Character ClosestEnemy = BaseAI.FindClosestEnemy(Player.m_localPlayer, Player.m_localPlayer.transform.position, 50);

                        currDist = Utils.DistanceXZ(Player.m_localPlayer.transform.position, ClosestEnemy.transform.position);

                        //Draw red if dist grows
                        style.normal.textColor = lastDist > currDist? Color.red: Color.green;
                        GUILayout.BeginHorizontal();

                        //GUI.Label
                        GUILayout.Label(
                            ClosestEnemy.GetHoverName().ToString() + "(" +
                            Utils.DistanceXZ(Player.m_localPlayer.transform.position, ClosestEnemy.transform.position).ToString("0.0") + ")", style, new GUILayoutOption[0]);

                        GUILayout.Space(20);

                        // Get direction
                        //            Vector3 enemyLook = ClosestEnemy.GetLookDir();
                        //            Vector3 myLook = Player.m_localPlayer.GetLookDir();

                        lastDist = currDist;
                        GUI.DragWindow();
            */

        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 5, 200, 60), "Everheim v.0.1");
           
            if (!visible)
                return;

            MainWindow = GUILayout.Window(0, MainWindow, new GUI.WindowFunction(RenderUI), "Tracking", new GUILayoutOption[0]);
        }

        private void ShowULMsg(string text)
        {
            Player.m_localPlayer.Message(
                MessageHud.MessageType.TopLeft,
                text);
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
        bool crazyRegen = false;


        private void Update()
        {
            //Reveal bosses
            if (Input.GetKeyDown(KeyCode.F1))
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

            //Check gui
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Console.instance.Print("Gui!");
                visible = !visible;
            }

            Player player = Player.m_localPlayer;

            // TODO; Populate some player variables
            // TODO: Remove cooldowns (pots, guardian power, ...)

            // Invigorate!
            //
            if (Input.GetKeyDown( KeyCode.F3))
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
                player.GetSEMan().RemoveStatusEffect("Encumbered"); // xD
                player.GetSEMan().RemoveStatusEffect("Burning");
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

            // Toggle god mode
            //
            if (Input.GetKeyDown(KeyCode.F4))
            {
                godMode = !godMode;
                player.SetGodMode(godMode);
                ShowULMsg(godMode ? "Agitated!" : "Calming down...");
            }

            // Toggle God power
            //
            if (Input.GetKeyDown(KeyCode.F6))
            {
                //GP_Eikthyr, GP_TheElder, GP_BoneMass, GP_Moder
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
            if (Input.GetKeyDown(KeyCode.F7))
            {
                player.ToggleDebugFly();
            }

            // Tame animals
            //
            if (Input.GetKeyDown(KeyCode.Print))
            {
                Tameable.TameAllInArea(player.transform.position, 30.0f);
                //Tameable.TameAllInArea(((Component)Player.m_localPlayer).get_transform().get_position(), 20f);

            }

            // Boost!
            //
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ShowULMsg("Feeling good.");

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
            }

            // Crazy health regen when below 75% health
            //
            if (Input.GetKeyDown(KeyCode.F9))
            {
                crazyRegen = !crazyRegen;
                counter = 0;
                ShowULMsg("Crazy regen: " + crazyRegen.ToString());
            }
            
            if (crazyRegen)
            {
                counter++;

                if (counter % 150 == 0)
                {
                    if (player.GetHealthPercentage() < 0.75f)
                    {
                        ShowULMsg("Player at " + player.GetHealthPercentage() + ". Healing 12.5f.");
                        player.Heal(12.5f, false);
                    }
                }
            }
            ////////////////////
            ///

            // Kill all monsters in radius 15f. Move this to somewhere else!
            //
            if (Input.GetKeyDown(KeyCode.Pause))
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

            // Toggle No build cost
            //
            if (Input.GetKeyDown(KeyCode.F10))
            {
                noBuildCost = !noBuildCost;
                Player.m_localPlayer.SetNoPlacementCost(value: noBuildCost);
                ShowULMsg(noBuildCost ? "Free!" : "At a premium.");

            }

            if (Input.GetKeyDown(KeyCode.PageUp))
            {

                player.m_runSpeed += 2;
                player.m_swimSpeed += 2;
                player.m_acceleration += 1;
                player.m_crouchSpeed += 2;
                player.m_walkSpeed += 2;
                player.m_jumpForce += 1;
                ShowULMsg("Faster: " + player.m_runSpeed);

                // Summon all peers - Just the first you find.
                //

                /*
                foreach (ZNetPeer peer in ZNet.instance.GetPeers())
                {
                    // If not me
                    if (peer.m_playerName != Player.m_localPlayer.GetPlayerName())
                    {
                        ShowULMsg("Summoning: " + peer.m_playerName);
                        Chat.instance.TeleportPlayer(peer.m_uid, Player.m_localPlayer.transform.position, Player.m_localPlayer.transform.rotation, distantTeleport: true);
                        player.GetSEMan().AddStatusEffect("Burning", resetTime: true, 10, 10);
                        return;
                    }
                }*/
            }

            

            //Summon all characters.
            //
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                player.m_runSpeed -= 2;
                player.m_acceleration -= 1;
                player.m_swimSpeed -= 2;
                player.m_crouchSpeed -= 2;
                player.m_walkSpeed -= 2;
                player.m_jumpForce -= 1f;
                ShowULMsg("Slower: " + player.m_runSpeed);
            }

            // More:Find boss-stones
            //


            // Port to coordinates specified by mouse cursor
            // INS.
            //
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);

                //Chat.instance.SendPing(position);
                //Console.instance.AddString("Teleport", "Heading to " + position.ToString("0.0"), (int)Talker.Type.Whisper);

                if (Player.m_localPlayer)
                {
                    Vector3 vector = new Vector3(position.x, Player.m_localPlayer.transform.position.y, position.z);
                    Heightmap.GetHeight(vector, out var height);
                    vector.y = Math.Max(0f, height);
                    Player.m_localPlayer.TeleportTo(vector, Player.m_localPlayer.transform.rotation, distantTeleport: true);
                }
            }

            // Port to coordinates specified by mouse cursor
            // INS.
            //
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);

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

                    //Teleport myself too?
                    //Player.m_localPlayer.TeleportTo(vector, Player.m_localPlayer.transform.rotation, distantTeleport: true);
                }
            }
            // Gate to somewhere safe.
            // HOME
            //
            // Prefer:
            //  1. Custom Spawn Point (Bed)
            //  2. Home (stones?)
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
