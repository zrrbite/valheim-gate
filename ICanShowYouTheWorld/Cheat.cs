using System;
using System.Collections.Generic;
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
//            GUILayout.Label("Monster:", new GUILayoutOption[0]);
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

        private void Update()
        {
            //Check gui
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Console.instance.Print("Gui!");
                visible = !visible;
            }

            Player player = Player.m_localPlayer;

            // TODO; Populate some player variables

            // Invigorate!
            //
            if (Input.GetKeyDown( KeyCode.F3))
            {
                // Resources
                player.AddStamina   ( player.GetMaxStamina() - player.GetStamina() );
                player.Heal         ( player.GetMaxHealth()  - player.GetHealth()  );
                player.AddEitr      ( player.GetMaxEitr()    - player.GetEitr()    );

                // Status
                //player.ClearHardDeath();
                player.GetSEMan().RemoveAllStatusEffects(); //This also removes rested. Does it remove buffs?
                player.GetSEMan().AddStatusEffect("Rested", resetTime: true, 10, 10);

//                Player.m_localPlayer.StartEmote("dance");

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


            // Tame animals
            //
            if (Input.GetKeyDown(KeyCode.F7))
            {
                Tameable.TameAllInArea(player.transform.position, 10.0f);
                //Tameable.TameAllInArea(((Component)Player.m_localPlayer).get_transform().get_position(), 20f);

            }

            // Rested Status
            //
            if (Input.GetKeyDown(KeyCode.F8))
            {
                player.GetSEMan().AddStatusEffect("Rested", resetTime: true, 10, 10); //skilllevel=0, ...
            }


            // Kill all monsters in radius 5f
            //
            if (Input.GetKeyDown(KeyCode.F9))
            {
                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, 10f, list);

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

            // 
            //
            if (Input.GetKeyDown(KeyCode.F10))
            {

            }
            // Find boss-stones
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
