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
            UnifiedPopup.Push(new WarningPopup("Hackers", "Loaded (0.213.4)! Now start a game.", delegate
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

    public class DiscoverThings : MonoBehaviour
    {
        private void Awake()
        {
            Console.instance.Print("Awake..");
        }

        private void Start()
        {
            Console.instance.Print("Start..");
        }

        private void Update()
        {
            // Port to coordinates specified by mouse cursor
            // INS.
            //
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                Vector3 position = ScreenToWorldPoint(Input.mousePosition);
                Chat.instance.SendPing(position);

                Console.instance.AddString("Teleport", "Heading to " + position.ToString("0.0"), (int)Talker.Type.Whisper);

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
                    Console.instance.AddString("Teleport", "Teleporting to " + dst.ToString("0.0"), Talker.Type.Shout);

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
                //Minimap.instance.
                Console.instance.Print("Reading PinData");

                List<Minimap.PinData> ourPins = Minimap.m_pins;
                foreach (Minimap.PinData pin in ourPins)
                {
                    //TODO: Add some helpers for teleporting (incl. logging)
                    Console.instance.Print("Pin = " + pin.m_name);
                    if(pin.m_name.Equals("safe"))
                    {
                        Console.instance.AddString("Teleport", "Teleporting to " + pin.m_pos.ToString("0.0"), Talker.Type.Shout);

                        // Perform distant teleport to dst
                        //
                        Player.m_localPlayer.TeleportTo(
                            pin.m_pos,
                            Player.m_localPlayer.transform.rotation,
                            true);
                    }
                    else
                    {
                        Console.instance.AddString("No safe spot found. Add a pin called 'safe'.");
                    }
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
