using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;
using Object = UnityEngine.Object;

namespace ICanShowYouTheWorld
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        private bool visible;
        const float TW = 300, TH = 250f;
        const float modeWidth = 325f;
        const float modeHeight = 550f;

        //private Rect trackWindow = new Rect(250, Screen.height - 250, 250, 150);
        private Rect trackWindow = new Rect(
            250,
            Screen.height - TH - 20f,
            TW,
            TH
        );

        private Rect modeWindow = new Rect(
            Screen.width - modeWidth,  // x
            Screen.height - modeHeight - 240f, // Margin from bottom
            modeWidth,                  // width
            modeHeight                  // height
        );
        // Pets panel, just to the right of tracking
        private Rect petWindow = new Rect(
            20f,
            300f,
            200f, TH
        );
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
                GUILayout.MinWidth(TW),
                GUILayout.MinHeight(TH)
            );
            modeWindow = GUILayout.Window(
                1,
                modeWindow,
                DrawModes,
                "Modes",
                GUILayout.MinWidth(modeWidth)
            );
            // ID = 2 for Pets
            petWindow = GUILayout.Window(
                2, petWindow, DrawPets, "Group",
                GUILayout.Width(200f), GUILayout.Height(TH)
            );
        }

        void DrawTracking(int id)
        {
            // draw opaque background
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Box(new Rect(0, 0, trackWindow.width, trackWindow.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            var player = Player.m_localPlayer;
            var list = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 100f, list);

            // sort by distance ascending
            list.Sort((a, b) => {
                float da = Utils.DistanceXZ(a.transform.position, player.transform.position);
                float db = Utils.DistanceXZ(b.transform.position, player.transform.position);
                return da.CompareTo(db);
            });

            // column widths (tweak as needed)
            const float nameW = 150f;
            const float distW = 75f;
            const float hpW = 100f;

            var oldColor = GUI.contentColor;
            foreach (var c in list)
            {
                if (c.IsPlayer() || c.IsTamed()) continue;

                float dist = Utils.DistanceXZ(c.transform.position, player.transform.position);
                float hpPct = c.GetHealthPercentage() * 100f;

                GUILayout.BeginHorizontal();

                // Name in white
                GUI.contentColor = Color.white;
                GUILayout.Label(c.GetHoverName(), GUILayout.Width(nameW));

                // Distance in cyan
                GUI.contentColor = Color.cyan;
                GUILayout.Label($"{dist:0.0}m", GUILayout.Width(distW));

                // Health in green (>75%) or red
                GUI.contentColor = hpPct >= 75f ? Color.green : Color.red;
                GUILayout.Label($"{hpPct:0.0}% ({Mathf.RoundToInt(c.GetHealth())})", GUILayout.Width(hpW));

                GUILayout.EndHorizontal();
            }
            GUI.contentColor = oldColor;

            GUI.DragWindow();
        }
        void DrawPets(int id)
        {
            // Opaque background
            GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
            GUI.Box(new Rect(0, 0, petWindow.width, petWindow.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            // --- Controls row ---
//            GUILayout.BeginHorizontal();
//            if (GUILayout.Button("Buff Tamed", GUILayout.ExpandWidth(true)))
//               CheatCommands.BuffTamed();
//            if (GUILayout.Button("Follow Me", GUILayout.ExpandWidth(true)))
//                CheatCommands.TameAll(); //target = me
//            if (GUILayout.Button("Stay", GUILayout.ExpandWidth(true)))
//                CheatCommands.TameAll(true); //clear
//            GUILayout.EndHorizontal();
//            GUILayout.Space(5f);

            var player = Player.m_localPlayer;
            var pets = new List<Character>();
            Character.GetCharactersInRange(player.transform.position, 50f, pets); //todo: helper. same with tame.

            const float nameW = 100f, hpW = 100f;
            var old = GUI.contentColor;
            foreach (var c in pets)
            {
                if (!c.IsTamed() && !c.IsPlayer()) continue;
                float dist = Utils.DistanceXZ(c.transform.position, player.transform.position);
                float hpPct = c.GetHealthPercentage() * 100f;

                GUILayout.BeginHorizontal();
                GUI.contentColor = Color.white;
                GUILayout.Label(c.GetHoverName(), GUILayout.Width(nameW));
                GUI.contentColor = hpPct >= 75f ? Color.green : Color.red;
                GUILayout.Label($"{hpPct:0.0}% ({Mathf.RoundToInt(c.GetHealth())})", GUILayout.Width(hpW));
                GUILayout.EndHorizontal();
            }
            GUI.contentColor = old;
            GUI.DragWindow();
        }
        void DrawModes(int id)
        {
            // 1) Opaque dark backdrop
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Box(new Rect(0, 0, modeWindow.width, modeWindow.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            // prepare a bold style for highlights
            var highlightStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                fontSize = 13
            };

            // 2.A) Prefab label
            GUILayout.BeginHorizontal();
            GUI.contentColor = Color.white;
            GUILayout.Label($"Prefab: {CheatCommands.CurrentPrefab}", highlightStyle, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var oldColor = GUI.contentColor;

            // small gap
            GUILayout.Space(5);

            // Utility header
            GUILayout.BeginHorizontal();
            GUI.contentColor = Color.white;
            GUILayout.Label($"Utility: {CheatCommands.CurrentUtilityName}", highlightStyle, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            // precompute description style
            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.grey }
            };
            // column widths
            const float descW = 160f;  // room for description
            const float keyW = 120f;   // room for "(KeyName)"

            // 3) Damage Counters row
            {
                GUILayout.BeginHorizontal();
                GUI.contentColor = Color.white;
                GUILayout.Label("Damage Counters", descStyle, GUILayout.Width(descW));
                GUI.contentColor = Color.yellow;
                GUILayout.Label(CheatCommands.DamageCounter.ToString(), GUILayout.Width(keyW));
                GUILayout.EndHorizontal();
            }

            // 4) Run Speed row
            {
                GUILayout.BeginHorizontal();
                GUI.contentColor = Color.white;
                GUILayout.Label("Run Speed", descStyle, GUILayout.Width(descW));
                GUI.contentColor = Color.yellow;
                float speed = Player.m_localPlayer.m_runSpeed;
                GUILayout.Label($"{speed:0.0}", GUILayout.Width(keyW));
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUI.contentColor = Color.white;
            GUILayout.Label("AOE Power", GUILayout.Width(160f));
            GUI.contentColor = Color.cyan;
            GUILayout.Label($"{CheatCommands.AoePower:0} / {CheatCommands.AoePower * 1.5:0}", GUILayout.Width(85f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 2) A little spacing
            GUILayout.Space(10f);

            GUI.contentColor = oldColor;

            foreach (var cmd in CommandRegistry.All)
            {
                bool hasState = cmd.GetState != null;
                bool isOn = hasState && cmd.GetState();

                GUILayout.BeginHorizontal();

                // 1) Description in grey bold
                GUILayout.Label(cmd.Description, descStyle, GUILayout.Width(descW));

                // 2) Key in yellow when off, green when on
                var keyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = isOn ? Color.green : Color.yellow }
                };
                GUILayout.Label($"({cmd.Key})", keyStyle, GUILayout.Width(keyW));

                // 3) push everything else (if any) to the right
                GUILayout.FlexibleSpace();

                GUILayout.EndHorizontal();
            }

            GUI.DragWindow();
        }
    }
}