using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICanShowYouTheWorld
{
    public class UtilityListView : MonoBehaviour
    {
        public static UtilityListView Instance { get; private set; }
        public bool Visible { get; private set; }

        private Rect _win = new Rect(60, 60, 320, 280);
        private readonly int _winId = 9021;

        private readonly List<CommandBinding> _items = new List<CommandBinding>();
        private int _selected;
        private Vector2 _scroll;

        // --- NEW: fixed row height so we can compute scroll math
        const float RowH = 22f;

        void Awake()
        {
            Instance = this;
            Refresh();
        }

        public void Refresh()
        {
            _items.Clear();
            // Utilities = commands WITHOUT a state getter
            foreach (var b in CommandRegistry.All)
                if (b.Execute != null && b.GetState == null)
                    _items.Add(b);

            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _items.Count - 1));
        }

        public void Toggle() => Visible = !Visible;

        void Update()
        {
            if (!Visible) return;

            if (Input.GetKeyDown(KeyCode.LeftAlt)) Move(-1);
            if (Input.GetKeyDown(KeyCode.RightAlt)) Move(+1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                ExecuteSelected();
        }

        void OnGUI()
        {
            if (!Visible) return;
            _win = GUI.Window(_winId, _win, DrawWindow, GUIContent.none);
        }

        void DrawWindow(int id)
        {
            GUI.backgroundColor = new Color(0, 0, 0, 0.85f);
            GUI.Box(new Rect(0, 0, _win.width, _win.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            var headerRect = new Rect(0, 0, _win.width, 24);
            GUI.Label(new Rect(8, 4, _win.width - 16, 20), "Utilities (↑/↓ select, Enter to run)");
            GUI.DragWindow(headerRect);

            // area bounds & visible height for the scroll math
            float areaX = 8f, areaY = 28f, areaW = _win.width - 16f, areaH = _win.height - 68f;
            float visibleH = areaH;

            GUILayout.BeginArea(new Rect(areaX, areaY, areaW, areaH));

            // begin scroll & keep handle to the new scroll pos
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _items.Count; i++)
            {
                var b = _items[i];

                GUILayout.BeginHorizontal(GUILayout.Height(RowH));

                var descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = (i == _selected) ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = (i == _selected) ? Color.green : Color.white },
                    alignment = TextAnchor.MiddleLeft
                };
                GUILayout.Label(b.Description, descStyle, GUILayout.Width(200));

                GUI.contentColor = new Color(0.5f, 1f, 1f, 1f);
                GUILayout.Label($"({b.Key})", GUILayout.Width(80));
                GUI.contentColor = Color.white;

                GUILayout.EndHorizontal();

                // click-to-select row
                var rowRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selected = i;
                    EnsureSelectionVisible(visibleH);
                    Event.current.Use();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▲", GUILayout.Width(50))) Move(-1);
            if (GUILayout.Button("▼", GUILayout.Width(50))) Move(+1);
            GUILayout.FlexibleSpace();
            GUI.enabled = _items.Count > 0;
            if (GUILayout.Button("Execute", GUILayout.Width(120))) ExecuteSelected();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void Move(int delta)
        {
            if (_items.Count == 0) return;
            _selected = (_selected + delta + _items.Count) % _items.Count;
            // viewport height = window height minus header/footer (same as areaH above)
            float visibleH = _win.height - 68f;
            EnsureSelectionVisible(visibleH);
        }

        void EnsureSelectionVisible(float visibleH)
        {
            // where should the selected row live in content coords?
            float targetTop = _selected * RowH;
            float targetBot = targetTop + RowH;

            // clamp content scroll range first
            float contentH = _items.Count * RowH;
            float maxScroll = Mathf.Max(0f, contentH - visibleH);

            if (targetTop < _scroll.y)
                _scroll.y = targetTop;
            else if (targetBot > _scroll.y + visibleH)
                _scroll.y = targetBot - visibleH;

            _scroll.y = Mathf.Clamp(_scroll.y, 0f, maxScroll);
        }

        void ExecuteSelected()
        {
            if (_items.Count == 0) return;
            _items[_selected].Execute();
        }
    }
}
